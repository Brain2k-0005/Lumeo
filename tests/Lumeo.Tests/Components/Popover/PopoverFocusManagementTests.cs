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
    public void Closing_Does_Not_Unconditionally_Focus_The_Wrapper()
    {
        // battle-wave2 #87 (keyboard-a11y): the close path used to unconditionally
        // call FocusElement(WrapperId), which stole focus back to the trigger even
        // on a programmatic/external close when the user's focus had moved on. The
        // fix routes the restore through SaveFocus/RestoreFocus (the non-modal
        // idiom shared with Select/DropdownMenu/ContextMenu) — RestoreFocus no-ops
        // when the saved element is gone, so it never force-focuses the wrapper.
        var cut = _ctx.Render<L.Popover>(p => p
            .Add(x => x.Open, true)
            .Add(x => x.ChildContent, Children));

        cut.WaitForAssertion(() =>
            Assert.Contains(_interop.FocusElementCalls, id => id.StartsWith("popover-content-")));

        var focusCallsBeforeClose = _interop.FocusElementCalls.Count;

        // Close the popover.
        cut.Render(p => p.Add(x => x.Open, false));

        // No new FocusElement(WrapperId) call: the wrapper (a non-content popover-*
        // id) must NOT be force-focused on close. Before the fix this assertion
        // failed because Cleanup() always called FocusElement(WrapperId).
        cut.WaitForState(() => !cut.Markup.Contains("Popover content"));
        Assert.DoesNotContain(_interop.FocusElementCalls.Skip(focusCallsBeforeClose),
            id => id.StartsWith("popover-") && !id.StartsWith("popover-content-"));
    }
}
