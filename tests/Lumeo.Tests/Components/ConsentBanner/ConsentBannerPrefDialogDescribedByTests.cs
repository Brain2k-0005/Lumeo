using AngleSharp.Dom;
using Bunit;
using Xunit;
using Lumeo.Tests.Helpers;
using Lumeo.Services;
using Microsoft.Extensions.DependencyInjection;
using L = Lumeo;

namespace Lumeo.Tests.Components.ConsentBanner;

/// <summary>
/// Regression for triage #201 (low / keyboard-a11y): the preferences modal
/// dialog was missing <c>aria-describedby</c>, and its description paragraph had
/// no id — so screen readers announced the dialog's accessible name but never
/// its description, unlike the banner region which wires both
/// <c>aria-labelledby</c> and <c>aria-describedby</c>.
///
/// Fix: a generated <c>_prefDescId</c> is placed on the description &lt;p&gt;
/// (line 65) and referenced from the dialog via <c>aria-describedby</c> (line 58),
/// mirroring the banner region's <c>_descId</c> wiring.
///
/// These tests reproduce the open-dialog state (render → click Customize →
/// OpenPreferences) and assert the OBSERVABLE markup mechanism: the dialog's
/// aria-describedby resolves to the paragraph carrying the PreferencesDescription
/// text. Without the fix aria-describedby is absent (null) and the description
/// paragraph has no id, so the wiring assertion fails.
/// </summary>
public class ConsentBannerPrefDialogDescribedByTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();

    public ConsentBannerPrefDialogDescribedByTests()
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

    // One required + one optional bucket so the Customize button renders and
    // OpenPreferences can be reached.
    private static IReadOnlyList<ConsentCategory> Categories() => new[]
    {
        new ConsentCategory("necessary", "Strictly necessary", "Required.", Required: true),
        new ConsentCategory("analytics", "Analytics", "Anonymous aggregate stats."),
    };

    [Fact]
    public void Pref_Dialog_Has_AriaDescribedBy_Pointing_At_Description_Paragraph()
    {
        const string prefDescription = "Choose exactly which trackers you allow.";

        var cut = _ctx.Render<L.ConsentBanner>(p => p
            .Add(c => c.Categories, Categories())
            .Add(c => c.PreferencesDescription, prefDescription));

        // The non-required bucket means the Customize button renders.
        cut.WaitForAssertion(() => ButtonWithText(cut, "Customize"));

        // Clicking Customize runs OpenPreferences() and renders the modal dialog.
        ButtonWithText(cut, "Customize").Click();

        cut.WaitForAssertion(() =>
        {
            var dialog = cut.Find("[role='dialog'][aria-modal='true']");

            // Pre-fix: aria-describedby was absent (null). The fix wires it to the
            // description paragraph's generated id.
            var describedBy = dialog.GetAttribute("aria-describedby");
            Assert.False(string.IsNullOrEmpty(describedBy));

            // The referenced element must exist and carry the description copy.
            var describedEl = cut.Find($"#{describedBy}");
            Assert.Equal(prefDescription, describedEl.TextContent.Trim());
        });
    }
}
