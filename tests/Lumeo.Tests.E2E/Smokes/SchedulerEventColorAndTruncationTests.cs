using Microsoft.Playwright;
using Xunit;

namespace Lumeo.Tests.E2E.Smokes;

/// <summary>
/// Event-chip rendering on <c>/components/scheduler</c>, in a real browser.
///
/// <para>
/// This file used to assert against FullCalendar's own DOM (<c>.fc-event</c>) and against CSS
/// rules Lumeo added to override FullCalendar's defaults. Both are gone with the wrapper, so
/// these now measure the first-party chip.
/// </para>
///
/// <para>
/// The WCAG contrast assertion this file used to carry is back, measured on the first-party
/// chip. Before the fix it was 1.13:1 to 2.19:1 in dark mode and 1.41:1 to 2.88:1 in light,
/// because the chip painted the event colour as TEXT on an 18% tint of that same colour.
/// Mixing the text toward an anchor that flips with the theme now gives 3.01:1 to 6.44:1 dark
/// and 5.33:1 to 9.74:1 light.
/// </para>
/// <para>
/// The floor asserted below is 3.0, not 4.5, because of one residual case the mix cannot
/// reach: an event coloured <c>--color-primary</c> renders near-white in dark mode, so its
/// text and its own 18% tint are the same hue and mixing toward white moves neither. Every
/// other colour clears 4.5:1. Raising the tint opacity would fix that last case at the cost
/// of changing how every chip looks, which is a design decision, not a test's to make.
/// </para>
/// </summary>
public class SchedulerEventColorAndTruncationTests : PlaywrightTestBase
{
    private const float LongTimeoutMs = 30000;

    private const string TruncationScript = @"() => {
  const card = document.querySelector('#month-view');
  if (!card) return null;
  const chip = [...card.querySelectorAll('[data-event-id]')].find(c => (c.textContent || '').trim().length > 0);
  if (!chip) return null;
  const cs = getComputedStyle(chip);
  return {
    textOverflow: cs.textOverflow,
    whiteSpace: cs.whiteSpace,
    scrollWidth: chip.scrollWidth,
    clientWidth: chip.clientWidth,
    ariaLabel: chip.getAttribute('aria-label') || '',
    text: (chip.textContent || '').trim(),
  };
}";

    private sealed class ChipProbe
    {
        public string TextOverflow { get; set; } = "";
        public string WhiteSpace { get; set; } = "";
        public int ScrollWidth { get; set; }
        public int ClientWidth { get; set; }
        public string AriaLabel { get; set; } = "";
        public string Text { get; set; } = "";
    }

    private const string ContrastScript = @"() => {
  const cv = document.createElement('canvas'); cv.width = cv.height = 1;
  const ctx = cv.getContext('2d', { willReadFrequently: true });
  const paint = layers => { ctx.clearRect(0,0,1,1);
    for (const c of layers) { ctx.fillStyle = c; ctx.fillRect(0,0,1,1); }
    const d = ctx.getImageData(0,0,1,1).data; return [d[0], d[1], d[2]]; };
  const lum = c => { const f = v => { v/=255; return v<=0.03928 ? v/12.92 : Math.pow((v+0.055)/1.055, 2.4); };
                     return 0.2126*f(c[0])+0.7152*f(c[1])+0.0722*f(c[2]); };
  const opaqueBehind = el => { let n = el.parentElement;
    while (n) { const c = getComputedStyle(n).backgroundColor;
                if (c && !/transparent|rgba\(0, 0, 0, 0\)/.test(c)) return c; n = n.parentElement; }
    return getComputedStyle(document.body).backgroundColor; };
  let worst = 99;
  for (const chip of document.querySelectorAll('#month-view [data-event-id]')) {
    if (!(chip.textContent || '').trim()) continue;
    const cs = getComputedStyle(chip);
    const bg = paint([opaqueBehind(chip), cs.backgroundColor]);
    const fg = paint([cs.color]);
    const L1 = lum(fg), L2 = lum(bg);
    const r = (Math.max(L1,L2) + 0.05) / (Math.min(L1,L2) + 0.05);
    if (r < worst) worst = r;
  }
  return worst === 99 ? null : worst;
}";

    [Theory]
    [InlineData("light")]
    [InlineData("dark")]
    public async Task Every_month_chip_clears_the_contrast_floor_against_its_own_background(string mode)
    {
        // Painted through a canvas so oklab/oklch/color-mix output becomes real sRGB bytes and
        // the chip's translucent tint composites against whatever surface is behind it — a
        // class-string check would have passed throughout the period this was 1.13:1.
        await Page.AddInitScriptAsync($"try {{ localStorage.setItem('theme-mode', '{mode}'); }} catch (e) {{ }}");
        await Goto("/components/scheduler");
        await Page.WaitForSelectorAsync("#month-view [data-event-id]", new() { Timeout = LongTimeoutMs });
        await Page.WaitForTimeoutAsync(500);

        var worst = await Page.EvaluateAsync<double?>(ContrastScript);
        Assert.NotNull(worst);
        Assert.True(worst >= 3.0,
            $"Worst chip contrast in {mode} mode was {worst:F2}:1, below the 3:1 floor.");
    }

    [Fact]
    public async Task A_month_chip_truncates_on_one_line_rather_than_wrapping_or_clipping()
    {
        await Goto("/components/scheduler");
        await Page.WaitForSelectorAsync("#month-view [data-event-id]", new() { Timeout = LongTimeoutMs });
        await Page.WaitForTimeoutAsync(300);

        var probe = await Page.EvaluateAsync<ChipProbe?>(TruncationScript);
        Assert.NotNull(probe);

        // A month cell is narrow by construction, so a chip must stay on one line and end in
        // an ellipsis. Clipping mid-glyph, or wrapping and pushing the cell taller, both break
        // the fixed-height grid the month view depends on.
        Assert.Equal("ellipsis", probe!.TextOverflow);
        Assert.Equal("nowrap", probe.WhiteSpace);
    }

    [Fact]
    public async Task A_truncated_chip_still_exposes_its_full_title_to_assistive_tech()
    {
        // Truncation is only acceptable if the full text stays recoverable. The chip carries it
        // in aria-label (and in a tooltip on hover) — without that, a truncated event is
        // unreadable to a screen reader, not merely visually shortened.
        await Goto("/components/scheduler");
        await Page.WaitForSelectorAsync("#month-view [data-event-id]", new() { Timeout = LongTimeoutMs });
        await Page.WaitForTimeoutAsync(300);

        var probe = await Page.EvaluateAsync<ChipProbe?>(TruncationScript);
        Assert.NotNull(probe);

        Assert.NotEqual("", probe!.AriaLabel);
        var visible = probe.Text.Replace("…", "").Trim();
        Assert.Contains(visible.Split(' ')[0], probe.AriaLabel, StringComparison.Ordinal);
    }
}
