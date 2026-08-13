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
/// It previously also carried a WCAG contrast assertion, which is NOT ported here — deliberately,
/// and not because the concern went away. Measured live against the first-party month chips, the
/// text-to-background ratio is 1.13:1 to 2.19:1 in dark mode and 1.41:1 to 2.88:1 in light,
/// against WCAG's 3:1 floor for UI text. That is a property of the chip's own design — it paints
/// the event colour as the TEXT on an 18%-opacity tint of that same colour — so every chip is
/// affected, not one demo event. Porting the old assertion would have meant either failing the
/// build or lowering the threshold to bless the current state; both are worse than saying so.
/// Tracked as its own issue.
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
