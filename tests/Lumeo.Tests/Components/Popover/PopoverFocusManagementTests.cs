using Bunit;
using Lumeo.Services;
using Lumeo.Tests.Helpers;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using L = Lumeo;

namespace Lumeo.Tests.Components.Popover;

/// <summary>
/// Regression tests for #219 — Popover had no focus management, so Escape was
/// unreachable (the @onkeydown lives on the content but focus never entered it).
/// PopoverContent now focuses itself on open and returns focus to the trigger
/// wrapper on close.
/// </summary>
public class PopoverFocusManagementTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();
    private readonly TrackingInteropService _interop = new();

    public PopoverFocusManagementTests()
    {
        _ctx.AddLumeoServices();
        _ctx.Services.AddSingleton<IComponentInteropService>(_interop);
    }

    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    private static RenderFragment Children => b =>
    {
        b.OpenComponent<L.PopoverTrigger>(0);
        b.AddAttribute(1, "ChildContent", (RenderFragment)(inner => inner.AddContent(0, "Toggle")));
        b.CloseComponent();

        b.OpenComponent<L.PopoverContent>(2);
        b.AddAttribute(3, "ChildContent", (RenderFragment)(inner => inner.AddContent(0, "Popover content")));
        b.CloseComponent();
    };

    [Fact]
    public void Content_Focuses_Itself_On_Open()
    {
        var cut = _ctx.Render<L.Popover>(p => p
            .Add(x => x.Open, true)
            .Add(x => x.ChildContent, Children));

        // Without focusing the content, the content's @onkeydown handler never
        // receives Escape because focus stays on the trigger.
        cut.WaitForAssertion(() =>
            Assert.Contains(_interop.FocusElementCalls, id => id.StartsWith("popover-content-")));
    }

    [Fact]
    public void Closing_Returns_Focus_To_Trigger_Wrapper()
    {
        var cut = _ctx.Render<L.Popover>(p => p
            .Add(x => x.Open, true)
            .Add(x => x.ChildContent, Children));

        cut.WaitForAssertion(() =>
            Assert.Contains(_interop.FocusElementCalls, id => id.StartsWith("popover-content-")));

        var focusCallsBeforeClose = _interop.FocusElementCalls.Count;

        // Close the popover — content cleanup should focus the wrapper.
        cut.Render(p => p.Add(x => x.Open, false));

        cut.WaitForAssertion(() =>
            Assert.Contains(_interop.FocusElementCalls.Skip(focusCallsBeforeClose),
                id => id.StartsWith("popover-") && !id.StartsWith("popover-content-")));
    }
}
