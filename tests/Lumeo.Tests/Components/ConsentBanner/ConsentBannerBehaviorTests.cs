using AngleSharp.Dom;
using Bunit;
using Xunit;
using Lumeo.Tests.Helpers;
using Lumeo.Services;
using Microsoft.Extensions.DependencyInjection;
using L = Lumeo;

namespace Lumeo.Tests.Components.ConsentBanner;

/// <summary>
/// Behaviour/a11y coverage for the consent banner — drives the REAL buttons and
/// asserts the visible-when-undecided / hidden-once-decided contract plus the
/// ConsentService side effects.
///
/// Visibility gate: the banner only renders once <c>_ready</c> flips, which
/// happens in <c>OnAfterRenderAsync(firstRender)</c> after
/// <see cref="ConsentService.EnsureLoadedAsync"/>. Under bUnit's loose JSInterop
/// the <c>localStorage.getItem</c> call returns null (no persisted decision), so
/// <see cref="ConsentService.HasDecided"/> is false and the banner shows. bUnit
/// awaits the async first render, so the banner is present right after Render.
///
/// Clicking Accept/Reject routes through <see cref="ConsentService.AcceptAllAsync"/>
/// / <see cref="ConsentService.RejectAllAsync"/>, which flips HasDecided, fires
/// OnChange (re-rendering the banner) and persists via <c>localStorage.setItem</c>.
/// </summary>
public class ConsentBannerBehaviorTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();

    public ConsentBannerBehaviorTests()
    {
        _ctx.AddLumeoServices();
        // ConsentBanner @injects ConsentService directly by concrete type; it is
        // not part of AddLumeoServices, so register it against bUnit's fake (loose)
        // IJSRuntime — localStorage get/set calls are recorded and return defaults.
        _ctx.Services.AddScoped<ConsentService>();
    }

    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    // Find a visible button by its trimmed text (the banner labels its actions
    // with plain text, so this is more robust than brittle class selectors).
    private static IElement ButtonWithText(IRenderedComponent<L.ConsentBanner> cut, string text)
        => cut.FindAll("button").Single(b => b.TextContent.Trim() == text);

    // ── Visibility contract ──────────────────────────────────────────────────

    [Fact]
    public void Shows_Banner_Region_When_Consent_Not_Yet_Given()
    {
        var cut = _ctx.Render<L.ConsentBanner>();

        // Non-modal banner exposes role="region" with a labelled title.
        cut.WaitForAssertion(() =>
        {
            var region = cut.Find("[role='region']");
            Assert.Contains("lumeo-consent-banner", region.GetAttribute("class"));
            // aria-labelledby points at the rendered <h3> title.
            var labelId = region.GetAttribute("aria-labelledby");
            Assert.False(string.IsNullOrEmpty(labelId));
            Assert.Equal("Cookies & consent", cut.Find($"#{labelId}").TextContent.Trim());
            // The describedby paragraph is present too.
            var descId = region.GetAttribute("aria-describedby");
            Assert.NotNull(cut.Find($"#{descId}"));
        });
    }

    [Fact]
    public async Task Hides_Banner_When_Consent_Already_Decided_Before_Render()
    {
        // Pre-decide on the SAME service instance the banner will inject, so the
        // banner mounts with HasDecided == true and never shows its region.
        var svc = _ctx.Services.GetRequiredService<ConsentService>();
        await svc.AcceptAllAsync(new[] { "analytics" });
        Assert.True(svc.HasDecided);

        var cut = _ctx.Render<L.ConsentBanner>();

        cut.WaitForAssertion(() => Assert.Empty(cut.FindAll("[role='region']")));
    }

    // ── Accept button ────────────────────────────────────────────────────────

    [Fact]
    public void Accept_Button_Grants_Consent_And_Dismisses_Banner()
    {
        var svc = _ctx.Services.GetRequiredService<ConsentService>();
        var cut = _ctx.Render<L.ConsentBanner>();

        // The real "Accept all" button drives the interaction.
        cut.WaitForAssertion(() => ButtonWithText(cut, "Accept all"));
        ButtonWithText(cut, "Accept all").Click();

        cut.WaitForAssertion(() =>
        {
            // Service recorded the decision and granted the optional category…
            Assert.True(svc.HasDecided);
            Assert.True(svc.HasConsent("analytics"));
            // …and the banner region is gone.
            Assert.Empty(cut.FindAll("[role='region']"));
        });
    }

    // ── Reject button ────────────────────────────────────────────────────────

    [Fact]
    public void Reject_Button_Denies_Optional_Consent_And_Dismisses_Banner()
    {
        var svc = _ctx.Services.GetRequiredService<ConsentService>();
        var cut = _ctx.Render<L.ConsentBanner>();

        cut.WaitForAssertion(() => ButtonWithText(cut, "Reject optional"));
        ButtonWithText(cut, "Reject optional").Click();

        cut.WaitForAssertion(() =>
        {
            Assert.True(svc.HasDecided);
            // Optional category was explicitly denied (fail-closed).
            Assert.False(svc.HasConsent("analytics"));
            Assert.Empty(cut.FindAll("[role='region']"));
        });
    }

    [Fact]
    public void Reject_Persists_Decision_Via_LocalStorage()
    {
        var cut = _ctx.Render<L.ConsentBanner>();

        cut.WaitForAssertion(() => ButtonWithText(cut, "Reject optional"));
        ButtonWithText(cut, "Reject optional").Click();

        // The decision is written through to localStorage so the banner stays
        // dismissed across reloads — assert the persistence interop contract.
        cut.WaitForAssertion(() =>
            Assert.Contains(_ctx.JSInterop.Invocations,
                i => i.Identifier == "localStorage.setItem"
                     && i.Arguments.Count > 0
                     && string.Equals(i.Arguments[0]?.ToString(), "lumeo:consent:v1")));
    }

    // ── Preferences dialog (Customize) ───────────────────────────────────────

    [Fact]
    public void Customize_Button_Opens_The_Modal_Preferences_Dialog()
    {
        var cut = _ctx.Render<L.ConsentBanner>();

        // Default categories include a non-required "analytics" bucket, so the
        // Customize button is rendered.
        cut.WaitForAssertion(() => ButtonWithText(cut, "Customize"));
        ButtonWithText(cut, "Customize").Click();

        cut.WaitForAssertion(() =>
        {
            // A true aria-modal dialog opens; the non-modal region is replaced.
            var dialog = cut.Find("[role='dialog'][aria-modal='true']");
            var labelId = dialog.GetAttribute("aria-labelledby");
            Assert.Equal("Consent preferences", cut.Find($"#{labelId}").TextContent.Trim());
            Assert.Empty(cut.FindAll("[role='region']"));
        });
    }
}
