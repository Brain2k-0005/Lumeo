using AngleSharp.Dom;
using Bunit;
using Xunit;
using Lumeo.Tests.Helpers;
using Lumeo.Services;
using Microsoft.Extensions.DependencyInjection;
using L = Lumeo;

namespace Lumeo.Tests.Components.ConsentBanner;

/// <summary>
/// Keyboard coverage for ConsentBanner:
///   - the preferences modal's own HandlePrefKeyDown: Escape closes WITHOUT
///     saving the in-progress draft (mirrors the discard-on-cancel behavior of
///     the Cancel button, just reachable without a pointer);
///   - Accept/Reject/Customize and the dialog's Cancel/Reject/Save are all
///     native &lt;button type="button"&gt; elements, so Enter/Space activation
///     and Tab reachability are the browser's native semantics for free — these
///     tests assert the underlying wiring (click invokes the same handler) and
///     the DOM tab order, which is what a native button actually gives you.
/// </summary>
public class ConsentBannerKeyboardTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();

    public ConsentBannerKeyboardTests()
    {
        _ctx.AddLumeoServices();
        _ctx.Services.AddScoped<ConsentService>();
    }

    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    private static IElement ButtonWithText(IRenderedComponent<L.ConsentBanner> cut, string text)
        => cut.FindAll("button").Single(b => b.TextContent.Trim() == text);

    private static IElement OptionalCheckbox(IRenderedComponent<L.ConsentBanner> cut)
        => cut.FindAll("input[type='checkbox']:not([disabled])").Single();

    private static bool IsBoxChecked(IRenderedComponent<L.ConsentBanner> cut)
        => OptionalCheckbox(cut).HasAttribute("checked");

    private static void OpenPreferencesDialog(IRenderedComponent<L.ConsentBanner> cut)
    {
        cut.WaitForAssertion(() => ButtonWithText(cut, "Customize"));
        ButtonWithText(cut, "Customize").Click();
        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll("[role='dialog'][aria-modal='true']")));
    }

    // --- Escape closes the preferences modal WITHOUT saving ---

    [Fact]
    public void Escape_In_Preferences_Modal_Closes_The_Dialog()
    {
        var cut = _ctx.Render<L.ConsentBanner>();
        OpenPreferencesDialog(cut);

        var dialog = cut.Find("[role='dialog'][aria-modal='true']");
        dialog.KeyDown("Escape");

        cut.WaitForAssertion(() => Assert.Empty(cut.FindAll("[role='dialog'][aria-modal='true']")));
    }

    [Fact]
    public void Escape_In_Preferences_Modal_Discards_The_InProgress_Draft()
    {
        var svc = _ctx.Services.GetRequiredService<ConsentService>();
        var cut = _ctx.Render<L.ConsentBanner>();
        OpenPreferencesDialog(cut);

        // In-progress edit: turn the optional bucket ON.
        Assert.False(IsBoxChecked(cut));
        OptionalCheckbox(cut).Change(true);
        cut.WaitForAssertion(() => Assert.True(IsBoxChecked(cut)));

        // Escape must discard it — no SetManyAsync, HasDecided stays false —
        // exactly like the Cancel button, just via the keyboard.
        cut.Find("[role='dialog'][aria-modal='true']").KeyDown("Escape");

        cut.WaitForAssertion(() => Assert.Empty(cut.FindAll("[role='dialog'][aria-modal='true']")));
        Assert.False(svc.HasDecided);

        // Reopening re-seeds from the (unchanged, un-consented) persisted state —
        // the discarded edit does not resurface.
        OpenPreferencesDialog(cut);
        Assert.False(IsBoxChecked(cut));
    }

    [Fact]
    public void Escape_On_An_Unrelated_Key_Does_Not_Close_The_Dialog()
    {
        var cut = _ctx.Render<L.ConsentBanner>();
        OpenPreferencesDialog(cut);

        cut.Find("[role='dialog'][aria-modal='true']").KeyDown("a");

        Assert.NotEmpty(cut.FindAll("[role='dialog'][aria-modal='true']"));
    }

    // --- Native <button> activation on the banner (Accept/Reject/Customize) ---

    [Fact]
    public void Accept_Is_A_Native_Button_And_Its_Click_Invokes_AcceptAll()
    {
        var svc = _ctx.Services.GetRequiredService<ConsentService>();
        var cut = _ctx.Render<L.ConsentBanner>();
        cut.WaitForAssertion(() => ButtonWithText(cut, "Accept all"));

        var accept = ButtonWithText(cut, "Accept all");
        Assert.Equal("button", accept.TagName.ToLowerInvariant());
        Assert.Equal("button", accept.GetAttribute("type"));

        accept.Click();

        // AcceptAll flips HasDecided — the browser's native Enter/Space
        // activation on this <button> reaches the exact same handler a click does.
        Assert.True(svc.HasDecided);
    }

    [Fact]
    public void Reject_Is_A_Native_Button_And_Its_Click_Invokes_RejectAll()
    {
        var svc = _ctx.Services.GetRequiredService<ConsentService>();
        var cut = _ctx.Render<L.ConsentBanner>();
        cut.WaitForAssertion(() => ButtonWithText(cut, "Reject optional"));

        var reject = ButtonWithText(cut, "Reject optional");
        Assert.Equal("button", reject.TagName.ToLowerInvariant());
        Assert.Equal("button", reject.GetAttribute("type"));

        reject.Click();

        Assert.True(svc.HasDecided);
    }

    // --- Tab order inside the preferences dialog ---

    [Fact]
    public void Tab_Order_Inside_The_Dialog_Is_Category_Toggles_Then_Cancel_Reject_Save()
    {
        var cut = _ctx.Render<L.ConsentBanner>();
        OpenPreferencesDialog(cut);

        var dialog = cut.Find("[role='dialog'][aria-modal='true']");
        var focusables = dialog.QuerySelectorAll("input[type='checkbox'], button").ToList();

        // DOM order (default Categories: necessary [disabled checkbox], analytics
        // [enabled checkbox]) then the footer buttons Cancel / Reject / Save,
        // exactly the source order in ConsentBanner.razor — no roving tabindex,
        // so native Tab order IS DOM order.
        Assert.Equal(5, focusables.Count);
        Assert.Equal("input", focusables[0].TagName.ToLowerInvariant());
        Assert.Equal("input", focusables[1].TagName.ToLowerInvariant());
        Assert.Equal("Cancel", focusables[2].TextContent.Trim());
        Assert.Equal("Reject optional", focusables[3].TextContent.Trim());
        Assert.Equal("Save preferences", focusables[4].TextContent.Trim());
    }
}
