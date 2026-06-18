using Bunit;
using Lumeo.Services;
using Lumeo.Tests.Helpers;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using L = Lumeo;

namespace Lumeo.Tests.Components.Tour;

/// <summary>
/// Regression tests for #233 — Tour had no Escape-to-skip, no focus into the
/// step, and no keyboard next/prev, leaving keyboard users stranded behind the
/// overlay. It now focuses the step dialog, is aria-modal, and handles
/// Escape / arrow navigation.
/// </summary>
public class TourKeyboardTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();
    private readonly TrackingInteropService _interop = new();

    public TourKeyboardTests()
    {
        _ctx.AddLumeoServices();
        _ctx.Services.AddSingleton<IComponentInteropService>(_interop);
    }

    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    private static List<L.Tour.TourStepConfig> Steps() => new()
    {
        new(null, "Step One", "First"),
        new(null, "Step Two", "Second"),
    };

    [Fact]
    public void Step_Dialog_Is_Modal_And_Focusable()
    {
        var cut = _ctx.Render<L.Tour>(p => p.Add(c => c.Steps, Steps()).Add(c => c.Open, true));
        var dialog = cut.Find("[role='dialog']");
        Assert.Equal("true", dialog.GetAttribute("aria-modal"));
        Assert.Equal("-1", dialog.GetAttribute("tabindex"));
    }

    [Fact]
    public void Opening_Focuses_The_Step_Dialog()
    {
        var cut = _ctx.Render<L.Tour>(p => p.Add(c => c.Steps, Steps()).Add(c => c.Open, true));
        cut.WaitForAssertion(() =>
            Assert.Contains(_interop.FocusElementCalls, id => id.StartsWith("tour-tooltip-")));
    }

    [Fact]
    public void Escape_Skips_The_Tour()
    {
        var skipped = false;
        var cut = _ctx.Render<L.Tour>(p => p
            .Add(c => c.Steps, Steps())
            .Add(c => c.Open, true)
            .Add(c => c.OnSkip, EventCallback.Factory.Create(this, () => skipped = true)));

        cut.Find("[role='dialog']").KeyDown(new KeyboardEventArgs { Key = "Escape" });

        Assert.True(skipped);
    }

    [Fact]
    public void ArrowRight_Advances_To_Next_Step()
    {
        var step = 0;
        var cut = _ctx.Render<L.Tour>(p => p
            .Add(c => c.Steps, Steps())
            .Add(c => c.Open, true)
            .Add(c => c.CurrentStep, 0)
            .Add(c => c.CurrentStepChanged, EventCallback.Factory.Create<int>(this, v => step = v)));

        cut.Find("[role='dialog']").KeyDown(new KeyboardEventArgs { Key = "ArrowRight" });

        Assert.Equal(1, step);
        Assert.Contains("Step Two", cut.Markup);
    }

    [Fact]
    public void ArrowLeft_Goes_To_Previous_Step()
    {
        var step = 1;
        var cut = _ctx.Render<L.Tour>(p => p
            .Add(c => c.Steps, Steps())
            .Add(c => c.Open, true)
            .Add(c => c.CurrentStep, 1)
            .Add(c => c.CurrentStepChanged, EventCallback.Factory.Create<int>(this, v => step = v)));

        cut.Find("[role='dialog']").KeyDown(new KeyboardEventArgs { Key = "ArrowLeft" });

        Assert.Equal(0, step);
    }
}
