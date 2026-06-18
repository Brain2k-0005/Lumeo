using Bunit;
using Lumeo.Services;
using Lumeo.Tests.Helpers;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using L = Lumeo;

namespace Lumeo.Tests.Components.ColorPicker;

/// <summary>
/// #191 — ColorPicker popover had no focus management: it never moved focus into
/// the popover (so Escape was dead until the user clicked inside) and never
/// trapped/restored focus. It now engages a focus trap on open (mirroring the
/// Popover Wave-1 fix) and releases it on close — the trap both moves focus in
/// (reachable Escape) and restores focus to the trigger.
/// </summary>
public class ColorPickerFocusManagementTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();
    private readonly TrackingInteropService _interop = new();

    public ColorPickerFocusManagementTests()
    {
        _ctx.AddLumeoServices();
        _ctx.Services.AddSingleton<IComponentInteropService>(_interop);
    }

    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    [Fact]
    public void Sets_Up_Focus_Trap_On_Open()
    {
        var cut = _ctx.Render<L.ColorPicker>(p => p.Add(c => c.Open, true));

        cut.WaitForAssertion(() =>
        {
            var setup = Assert.Single(_interop.FocusTrapSetups);
            Assert.StartsWith("colorpicker-content-", setup.ElementId);
        });
    }

    [Fact]
    public void Does_Not_Set_Up_Focus_Trap_When_Closed()
    {
        _ctx.Render<L.ColorPicker>(p => p.Add(c => c.Open, false));
        Assert.Empty(_interop.FocusTrapSetups);
    }

    [Fact]
    public void Removes_Focus_Trap_On_Close()
    {
        var cut = _ctx.Render<L.ColorPicker>(p => p.Add(c => c.Open, true));
        cut.WaitForAssertion(() => Assert.Single(_interop.FocusTrapSetups));

        cut.Render(p => p.Add(c => c.Open, false));

        cut.WaitForAssertion(() =>
        {
            var removed = Assert.Single(_interop.FocusTrapRemovals);
            Assert.StartsWith("colorpicker-content-", removed);
        });
    }

    [Fact]
    public void Escape_On_Content_Closes_Without_A_Preliminary_Click()
    {
        bool? openValue = null;
        var cb = EventCallback.Factory.Create<bool>(_ctx, (bool v) => openValue = v);
        var cut = _ctx.Render<L.ColorPicker>(p => p
            .Add(c => c.Open, true)
            .Add(c => c.OpenChanged, cb));

        // The content carries role=dialog + the @onkeydown Escape handler. With
        // the focus trap moving focus inside on open, a keydown on the content
        // (where a focused child's event bubbles) closes it — no click first.
        var dialog = cut.Find("[role='dialog']");
        dialog.KeyDown(new KeyboardEventArgs { Key = "Escape" });

        Assert.False(openValue);
    }
}
