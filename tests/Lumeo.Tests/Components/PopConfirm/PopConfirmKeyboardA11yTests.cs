using Bunit;
using Lumeo.Services;
using Lumeo.Tests.Helpers;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using L = Lumeo;

namespace Lumeo.Tests.Components.PopConfirm;

/// <summary>
/// Regression tests for the battle-wave2 keyboard/a11y findings on PopConfirm:
///  • #84 — the trigger advertised no aria-haspopup / aria-expanded / aria-controls,
///    so assistive tech could not tell it opened a dialog nor that it was expanded.
///  • #182 — on close the shared PopoverContent returned focus to the Popover
///    wrapper rather than the actual trigger, dropping keyboard users off-target.
///
/// These assert the OBSERVABLE mechanism (rendered ARIA + the FocusElement interop
/// call) rather than real DOM focus, which bUnit cannot move.
/// </summary>
public class PopConfirmKeyboardA11yTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();
    private readonly TrackingInteropService _interop = new();

    public PopConfirmKeyboardA11yTests()
    {
        _ctx.AddLumeoServices();
        // Override the interop with a tracking double so we can assert the
        // focus-restore call (#182) without needing real DOM focus.
        _ctx.Services.AddSingleton<IComponentInteropService>(_interop);
    }

    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    private IRenderedComponent<L.PopConfirm> RenderPopConfirm()
        => _ctx.Render<L.PopConfirm>(p =>
        {
            p.Add(c => c.Title, "Are you sure?");
            p.Add(c => c.ChildContent, (RenderFragment)(b =>
            {
                b.OpenElement(0, "span");
                b.AddContent(1, "Delete");
                b.CloseElement();
            }));
        });

    // --- #84: trigger advertises the popover ARIA relationship ---

    [Fact]
    public void Trigger_Advertises_Haspopup_And_Collapsed_State_When_Closed()
    {
        var cut = RenderPopConfirm();
        var trigger = cut.Find("span[role='button']");

        Assert.Equal("dialog", trigger.GetAttribute("aria-haspopup"));
        Assert.Equal("false", trigger.GetAttribute("aria-expanded"));
        // No dialog is open, so the trigger must not claim to control one.
        Assert.True(string.IsNullOrEmpty(trigger.GetAttribute("aria-controls")));
    }

    [Fact]
    public void Trigger_Reports_Expanded_And_Controls_Open_Dialog()
    {
        var cut = RenderPopConfirm();
        cut.Find("span[role='button']").Click();

        var trigger = cut.Find("span[role='button']");
        Assert.Equal("true", trigger.GetAttribute("aria-expanded"));

        // aria-controls must point at the actual alertdialog surface so AT can
        // navigate from the trigger to the confirmation it just opened.
        var controls = trigger.GetAttribute("aria-controls");
        Assert.False(string.IsNullOrEmpty(controls));
        var dialog = cut.Find("[role='alertdialog']");
        Assert.Equal(controls, dialog.GetAttribute("id"));
    }

    // --- #182: closing returns focus to the trigger itself ---

    [Fact]
    public void Confirm_Restores_Focus_To_The_Trigger()
    {
        var cut = RenderPopConfirm();
        cut.Find("span[role='button']").Click();

        var triggerId = cut.Find("span[role='button']").GetAttribute("id");
        Assert.False(string.IsNullOrEmpty(triggerId));

        var focusCallsBefore = _interop.FocusElementCalls.Count;

        // Click the confirm (last) button inside the alertdialog.
        var confirmButton = cut.FindAll("[role='alertdialog'] button").Last();
        confirmButton.Click();

        // Focus must be returned to the trigger span, not just the Popover wrapper.
        Assert.Contains(_interop.FocusElementCalls.Skip(focusCallsBefore),
            id => id == triggerId);
    }

    [Fact]
    public void Cancel_Restores_Focus_To_The_Trigger()
    {
        var cut = RenderPopConfirm();
        cut.Find("span[role='button']").Click();

        var triggerId = cut.Find("span[role='button']").GetAttribute("id");
        var focusCallsBefore = _interop.FocusElementCalls.Count;

        // The cancel button is the first of the two action buttons.
        var cancelButton = cut.FindAll("[role='alertdialog'] button").First();
        cancelButton.Click();

        Assert.Contains(_interop.FocusElementCalls.Skip(focusCallsBefore),
            id => id == triggerId);
    }
}
