using AngleSharp.Dom;
using Bunit;
using Xunit;
using Lumeo.Tests.Helpers;
using Lumeo.Services;
using Microsoft.Extensions.DependencyInjection;
using L = Lumeo;

namespace Lumeo.Tests.Components.ConsentBanner;

/// <summary>
/// Regression for triage #112 (medium / state-on-data-change): reopening the
/// preferences dialog while it is ALREADY open must not discard the user's
/// in-progress (unsaved) toggle edits.
///
/// Root cause: <c>OpenPreferences()</c> re-seeded <c>_draft</c> from current
/// consent unconditionally. Both the public <c>ShowPreferences()</c> entry point
/// and the <c>ConsentService.RequestOpenPreferences()</c> event route through it,
/// so a redundant reopen request fired while the dialog was open clobbered the
/// draft back to the persisted state — silently wiping pending toggle edits.
///
/// The fix only seeds the draft on a genuine closed-&gt;open transition
/// (<c>if (!_preferencesOpen) { ...seed... }</c>). This test reproduces the exact
/// sequence: open dialog → toggle the optional bucket ON → fire a reopen request
/// while still open → assert the toggle survived. Without the fix the second
/// OpenPreferences re-seeds and the checkbox reverts to its persisted (unchecked)
/// state.
/// </summary>
public class ConsentBannerReopenPreservesDraftTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();

    public ConsentBannerReopenPreservesDraftTests()
    {
        _ctx.AddLumeoServices();
        // ConsentBanner @injects ConsentService directly by concrete type; it is
        // not part of AddLumeoServices, so register it explicitly against bUnit's
        // loose IJSRuntime (localStorage get/set return defaults).
        _ctx.Services.AddScoped<ConsentService>();
    }

    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    private static IElement ButtonWithText(IRenderedComponent<L.ConsentBanner> cut, string text)
        => cut.FindAll("button").Single(b => b.TextContent.Trim() == text);

    // The optional "analytics" checkbox inside the dialog: the only enabled
    // (non-disabled) checkbox, since "necessary" is Required and rendered disabled.
    // Re-query each time — re-renders replace the element instance.
    private static IElement OptionalCheckbox(IRenderedComponent<L.ConsentBanner> cut)
        => cut.FindAll("input[type='checkbox']:not([disabled])").Single();

    // The component renders checked="@checkedValue"; a true bool emits the
    // `checked` attribute, false omits it — so attribute presence is the draft.
    private static bool IsBoxChecked(IRenderedComponent<L.ConsentBanner> cut)
        => OptionalCheckbox(cut).HasAttribute("checked");

    [Fact]
    public void Reopen_Request_While_Open_Preserves_InProgress_Toggle_Edits()
    {
        var svc = _ctx.Services.GetRequiredService<ConsentService>();
        var cut = _ctx.Render<L.ConsentBanner>();

        // Open the preferences dialog via the real Customize button.
        cut.WaitForAssertion(() => ButtonWithText(cut, "Customize"));
        ButtonWithText(cut, "Customize").Click();
        cut.WaitForAssertion(() =>
            Assert.NotEmpty(cut.FindAll("[role='dialog'][aria-modal='true']")));

        // Optional "analytics" starts unchecked (no persisted consent yet).
        Assert.False(IsBoxChecked(cut));

        // User makes an in-progress edit: toggle the optional bucket ON.
        OptionalCheckbox(cut).Change(true);
        cut.WaitForAssertion(() => Assert.True(IsBoxChecked(cut)));

        // A redundant reopen request arrives while the dialog is STILL open
        // (e.g. a "Manage cookie preferences" link clicked again). This routes
        // through OpenPreferences(). Pre-fix it re-seeded the draft from the
        // persisted (un-consented) state, wiping the pending edit.
        svc.RequestOpenPreferences();

        // The unsaved toggle edit must survive the reopen — the draft is only
        // seeded on a real closed->open transition.
        cut.WaitForAssertion(() => Assert.True(IsBoxChecked(cut)));
    }

    [Fact]
    public void Public_ShowPreferences_While_Open_Preserves_InProgress_Toggle_Edits()
    {
        var cut = _ctx.Render<L.ConsentBanner>();

        cut.WaitForAssertion(() => ButtonWithText(cut, "Customize"));
        ButtonWithText(cut, "Customize").Click();
        cut.WaitForAssertion(() =>
            Assert.NotEmpty(cut.FindAll("[role='dialog'][aria-modal='true']")));

        Assert.False(IsBoxChecked(cut));
        OptionalCheckbox(cut).Change(true);
        cut.WaitForAssertion(() => Assert.True(IsBoxChecked(cut)));

        // The public reopen entry point must also preserve the pending edit when
        // invoked while the dialog is already open. Call it through the component
        // instance so it runs on the renderer's sync context (StateHasChanged).
        cut.InvokeAsync(() => cut.Instance.ShowPreferences()).GetAwaiter().GetResult();

        cut.WaitForAssertion(() => Assert.True(IsBoxChecked(cut)));
    }
}
