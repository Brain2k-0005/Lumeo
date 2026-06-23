using Microsoft.Playwright;
using Xunit;

namespace Lumeo.Tests.E2E.Smokes;

/// <summary>
/// B5 — the native range Slider must paint its thumb/track in the theme's primary
/// colour (via the <c>accent-primary</c> utility → <c>accent-color: var(--color-primary)</c>),
/// not the browser default. This needs a real browser because <c>accent-color</c> only
/// resolves against the native control at render time; bUnit can only assert the class
/// is present. Verified by resolving both the slider's computed <c>accent-color</c> and
/// the theme's <c>--color-primary</c> to the same rgb space and comparing.
/// </summary>
public class SliderAccentColorTests : PlaywrightTestBase
{
    [Fact]
    public async Task Slider_Thumb_Uses_The_Theme_Primary_Accent_Not_The_Browser_Default()
    {
        await Goto("/components/slider");
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        var input = Page.Locator("input[type='range']").First;
        await input.WaitForAsync();

        var result = await Page.EvaluateAsync<AccentProbe>(@"() => {
            const input = document.querySelector(""input[type='range']"");
            const accent = getComputedStyle(input).accentColor;
            // Resolve --color-primary through a probe element so both values are in
            // the browser's normalized rgb form — independent of how the token is
            // authored (oklch / hsl / hex).
            const probe = document.createElement('span');
            probe.style.color = 'var(--color-primary)';
            document.body.appendChild(probe);
            const primary = getComputedStyle(probe).color;
            probe.remove();
            return { Accent: accent, Primary: primary };
        }");

        // The slider must not fall back to the UA default accent...
        Assert.NotEqual("auto", result.Accent);
        Assert.False(string.IsNullOrWhiteSpace(result.Accent));
        // ...and it must be the theme's primary colour.
        Assert.Equal(result.Primary, result.Accent);
    }

    private sealed class AccentProbe
    {
        public string Accent { get; set; } = "";
        public string Primary { get; set; } = "";
    }
}
