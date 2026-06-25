using AngleSharp.Dom;
using Bunit;
using Xunit;
using Lumeo.Tests.Helpers;
using Lumeo.Services;
using Microsoft.Extensions.DependencyInjection;
using L = Lumeo;

namespace Lumeo.Tests.Components.ConsentBanner;

/// <summary>
/// Regression for triage #13 (high / edge-data): opening the preferences dialog
/// must not throw — and tear down the circuit — when two <see cref="ConsentCategory"/>
/// entries share a Key case-insensitively.
///
/// Root cause: <c>OpenPreferences()</c> seeded the draft via
/// <c>Categories.ToDictionary(c => c.Key, ..., StringComparer.OrdinalIgnoreCase)</c>.
/// <c>ToDictionary</c> throws <see cref="ArgumentException"/> ("An item with the
/// same key has already been added") the moment two keys collide under the
/// OrdinalIgnoreCase comparer, so the very click that opens the dialog blows up.
///
/// The fix builds the draft with a defensive last-wins loop. These tests
/// reproduce the exact state sequence: render → click Customize (which calls
/// OpenPreferences) → assert the modal dialog actually opened. Without the fix
/// the OpenPreferences call throws and the dialog is never rendered.
/// </summary>
public class ConsentBannerDuplicateKeyTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();

    public ConsentBannerDuplicateKeyTests()
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

    // Two optional categories whose keys differ only by case → collide under the
    // OrdinalIgnoreCase draft dictionary.
    private static IReadOnlyList<ConsentCategory> CaseColidingCategories() => new[]
    {
        new ConsentCategory("necessary", "Strictly necessary", "Required.", Required: true),
        new ConsentCategory("Analytics", "Analytics (caps)", "First analytics bucket."),
        new ConsentCategory("analytics", "analytics (lower)", "Second, case-colliding bucket."),
    };

    [Fact]
    public void Customize_Opens_Dialog_Even_When_Two_Categories_Share_A_Key_CaseInsensitively()
    {
        var cut = _ctx.Render<L.ConsentBanner>(p => p
            .Add(c => c.Categories, CaseColidingCategories()));

        // The non-required buckets mean the Customize button renders.
        cut.WaitForAssertion(() => ButtonWithText(cut, "Customize"));

        // Clicking Customize runs OpenPreferences(). Pre-fix this threw an
        // ArgumentException (duplicate key) inside the click handler, tearing the
        // circuit down before any dialog could render.
        ButtonWithText(cut, "Customize").Click();

        cut.WaitForAssertion(() =>
        {
            // The aria-modal preferences dialog opened successfully.
            var dialog = cut.Find("[role='dialog'][aria-modal='true']");
            var labelId = dialog.GetAttribute("aria-labelledby");
            Assert.Equal("Consent preferences", cut.Find($"#{labelId}").TextContent.Trim());
        });
    }

    [Fact]
    public void Reopen_Request_Does_Not_Throw_With_Case_Colliding_Keys()
    {
        var svc = _ctx.Services.GetRequiredService<ConsentService>();
        var cut = _ctx.Render<L.ConsentBanner>(p => p
            .Add(c => c.Categories, CaseColidingCategories()));

        // Wait until the banner is mounted (so OnRequestOpenPreferences is wired).
        cut.WaitForAssertion(() => ButtonWithText(cut, "Customize"));

        // The public reopen path (a "Manage cookie preferences" link) also routes
        // through OpenPreferences — it must not throw on case-colliding keys.
        var ex = Record.Exception(() => svc.RequestOpenPreferences());
        Assert.Null(ex);

        cut.WaitForAssertion(() =>
            Assert.NotEmpty(cut.FindAll("[role='dialog'][aria-modal='true']")));
    }
}
