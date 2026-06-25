using AngleSharp.Dom;
using Bunit;
using Xunit;
using Lumeo.Tests.Helpers;
using Lumeo.Services;
using Microsoft.Extensions.DependencyInjection;
using L = Lumeo;

namespace Lumeo.Tests.Components.ConsentBanner;

/// <summary>
/// Regression for triage #202 (low / edge-data): explicitly passing
/// <c>Categories=null</c> NRE'd on every render.
///
/// Root cause: <c>Categories</c> is declared non-nullable
/// (<c>IReadOnlyList&lt;ConsentCategory&gt;</c>) and defaults to the built-in
/// set, but a consumer can still push <c>null</c> through the parameter setter
/// (e.g. an unresolved binding). Every dereference then threw a
/// <see cref="System.NullReferenceException"/>:
/// the banner render's <c>Categories.Any(c =&gt; !c.Required)</c>, the dialog
/// <c>@foreach (var cat in Categories)</c>, <c>OpenPreferences()</c>'s draft
/// loop, and <c>AcceptAll</c>/<c>RejectAll</c>'s <c>Categories.Where(...)</c>.
///
/// The fix routes every enumeration through a null-coalescing
/// <c>EffectiveCategories =&gt; Categories ?? DefaultCategories</c> getter, so a
/// transiently-null binding renders the default set instead of crashing.
///
/// These tests reproduce the exact edge input (Categories explicitly null) and
/// assert (a) rendering does not throw, (b) the banner still shows using the
/// defaults, and (c) the preferences dialog still opens. Without the fix step
/// (a) throws an NRE during the very first render.
/// </summary>
public class ConsentBannerNullCategoriesTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();

    public ConsentBannerNullCategoriesTests()
    {
        _ctx.AddLumeoServices();
        // ConsentBanner @injects ConsentService directly by concrete type; it is
        // not part of AddLumeoServices, so register it explicitly against bUnit's
        // loose IJSRuntime (localStorage get/set return defaults → banner shows).
        _ctx.Services.AddScoped<ConsentService>();
    }

    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    private static IElement ButtonWithText(IRenderedComponent<L.ConsentBanner> cut, string text)
        => cut.FindAll("button").Single(b => b.TextContent.Trim() == text);

    [Fact]
    public void Render_With_Null_Categories_Does_Not_Throw_And_Shows_Banner()
    {
        IRenderedComponent<L.ConsentBanner>? cut = null;

        // Explicitly pushing Categories=null is the exact edge input. Pre-fix the
        // banner render's `Categories.Any(...)` NRE'd during this first render.
        var ex = Record.Exception(() =>
            cut = _ctx.Render<L.ConsentBanner>(p => p
                .Add(c => c.Categories, (IReadOnlyList<ConsentCategory>?)null!)));

        Assert.Null(ex);
        Assert.NotNull(cut);

        // The banner falls back to the default category set (which includes a
        // non-required "Analytics" bucket), so the region renders normally.
        cut!.WaitForAssertion(() =>
        {
            var region = cut.Find("[role='region']");
            Assert.Contains("lumeo-consent-banner", region.GetAttribute("class"));
            // Default set has an optional bucket → the Customize button is shown.
            Assert.NotNull(ButtonWithText(cut, "Customize"));
        });
    }

    [Fact]
    public void Customize_Opens_Dialog_When_Categories_Is_Null()
    {
        var cut = _ctx.Render<L.ConsentBanner>(p => p
            .Add(c => c.Categories, (IReadOnlyList<ConsentCategory>?)null!));

        // Wait for the banner (rendered from defaults) and click Customize, which
        // runs OpenPreferences(). Pre-fix the draft loop `foreach (var c in
        // Categories)` NRE'd on the null parameter, tearing down the circuit.
        cut.WaitForAssertion(() => ButtonWithText(cut, "Customize"));
        ButtonWithText(cut, "Customize").Click();

        cut.WaitForAssertion(() =>
        {
            // The aria-modal preferences dialog opened successfully, populated from
            // the default categories rather than crashing on the null parameter.
            var dialog = cut.Find("[role='dialog'][aria-modal='true']");
            var labelId = dialog.GetAttribute("aria-labelledby");
            Assert.Equal("Consent preferences", cut.Find($"#{labelId}").TextContent.Trim());
            // Default categories rendered at least one toggle row.
            Assert.NotEmpty(cut.FindAll("input[type='checkbox']"));
        });
    }
}
