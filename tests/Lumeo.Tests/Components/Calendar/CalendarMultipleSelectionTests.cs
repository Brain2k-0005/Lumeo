using Bunit;
using Microsoft.AspNetCore.Components;
using Xunit;
using Lumeo.Tests.Helpers;
using L = Lumeo;

namespace Lumeo.Tests.Components.Calendar;

/// <summary>
/// #207 — Calendar multiple-selection mode (IsMultiple + Values/ValuesChanged):
/// clicking a day toggles it in/out of the selected set, selected days carry the
/// primary highlight and aria-pressed, and the bound list stays sorted.
/// </summary>
public class CalendarMultipleSelectionTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();

    public CalendarMultipleSelectionTests() => _ctx.AddLumeoServices();
    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    private static readonly DateOnly Anchor = new(2024, 6, 15);

    private IRenderedComponent<L.Calendar> Render(
        List<DateOnly>? values = null,
        EventCallback<List<DateOnly>?>? changed = null)
        => _ctx.Render<L.Calendar>(p =>
        {
            p.Add(c => c.IsMultiple, true);
            p.Add(c => c.Value, Anchor); // anchors the displayed month to June 2024
            if (values is not null) p.Add(c => c.Values, values);
            if (changed.HasValue) p.Add(c => c.ValuesChanged, changed.Value);
        });

    // Current-month day cell for the given day-of-month (a leading/trailing copy
    // of the same number can appear; the current-month one is not muted).
    private static AngleSharp.Dom.IElement Day(IRenderedComponent<L.Calendar> cut, int day)
        => cut.FindAll("button")
            .First(b => b.TextContent.Trim() == day.ToString()
                        && !(b.GetAttribute("class") ?? "").Contains("muted-foreground/50"));

    [Fact]
    public void Clicking_Days_Accumulates_Selection()
    {
        var captured = new List<DateOnly>();
        var cb = EventCallback.Factory.Create<List<DateOnly>?>(this, v => captured = v ?? new());
        // Seed with the 15th so the displayed month is fixed to June 2024
        // (multiple mode anchors off Values, not Value).
        var cut = Render(values: new List<DateOnly> { Anchor }, changed: cb);

        Day(cut, 10).Click(); // adds the 10th → [10, 15]
        cut.Render(p => p.Add(c => c.Values, captured)); // echo bound value back
        Day(cut, 20).Click(); // adds the 20th → [10, 15, 20]

        Assert.Equal(
            new[] { new DateOnly(2024, 6, 10), new DateOnly(2024, 6, 15), new DateOnly(2024, 6, 20) },
            captured);
    }

    [Fact]
    public void Clicking_A_Selected_Day_Toggles_It_Off()
    {
        var captured = new List<DateOnly>();
        var cb = EventCallback.Factory.Create<List<DateOnly>?>(this, v => captured = v ?? new());
        var cut = Render(values: new List<DateOnly> { new(2024, 6, 10), new(2024, 6, 12) }, changed: cb);

        Day(cut, 10).Click();

        Assert.Equal(new[] { new DateOnly(2024, 6, 12) }, captured);
    }

    [Fact]
    public void Result_List_Is_Sorted()
    {
        var captured = new List<DateOnly>();
        var cb = EventCallback.Factory.Create<List<DateOnly>?>(this, v => captured = v ?? new());
        // Pre-seed with the 20th, then click the 5th — result must be [5, 20].
        var cut = Render(values: new List<DateOnly> { new(2024, 6, 20) }, changed: cb);

        Day(cut, 5).Click();

        Assert.Equal(new[] { new DateOnly(2024, 6, 5), new DateOnly(2024, 6, 20) }, captured);
    }

    [Fact]
    public void Selected_Days_Carry_Primary_Highlight_And_AriaPressed()
    {
        var cut = Render(values: new List<DateOnly> { new(2024, 6, 10) });

        var ten = Day(cut, 10);
        Assert.Contains("bg-primary", ten.ClassList);
        Assert.Equal("true", ten.GetAttribute("aria-pressed"));

        var eleven = Day(cut, 11);
        Assert.Equal("false", eleven.GetAttribute("aria-pressed"));
        Assert.DoesNotContain("bg-primary", eleven.ClassList);
    }

    private static string MonthHeader(DateOnly d) => $"{d.ToString("MMMM")} {d.Year}";

    [Fact]
    public void Deselecting_Earliest_Day_Does_Not_Teleport_Displayed_Month()
    {
        // #53 — multiple mode: CurrentAnchor is Values.Min(). Two selected days in
        // DIFFERENT months ([March 10, June 20]) anchor the display to March. The
        // user deselects the earliest (March 10) while looking at March; the parent
        // echoes the resulting [June 20] back. The display must STAY on March (where
        // the click happened), not teleport to June 20's month.
        //
        // Pre-fix: SelectDay's multiple branch never updated _lastSeenAnchor, so the
        // echoed June 20 read as a brand-new external anchor in OnParametersSet and
        // yanked the month to June.
        var captured = new List<DateOnly>();
        var cb = EventCallback.Factory.Create<List<DateOnly>?>(this, v => captured = v ?? new());
        var cut = _ctx.Render<L.Calendar>(p =>
        {
            p.Add(c => c.IsMultiple, true);
            p.Add(c => c.Values, new List<DateOnly> { new(2024, 3, 10), new(2024, 6, 20) });
            p.Add(c => c.ValuesChanged, cb);
        });

        Assert.Contains(MonthHeader(new DateOnly(2024, 3, 1)), cut.Markup);

        Day(cut, 10).Click(); // deselect the earliest → [June 20]
        Assert.Equal(new[] { new DateOnly(2024, 6, 20) }, captured);

        // Parent echoes the new bound list back (controlled usage).
        cut.Render(p => p.Add(c => c.Values, captured));

        // The display must remain on March (where the user clicked), not jump to June.
        Assert.Contains(MonthHeader(new DateOnly(2024, 3, 1)), cut.Markup);
        Assert.DoesNotContain(MonthHeader(new DateOnly(2024, 6, 1)), cut.Markup);
    }

    [Fact]
    public void Disabled_Days_Cannot_Be_Toggled()
    {
        var captured = new List<DateOnly>();
        var cb = EventCallback.Factory.Create<List<DateOnly>?>(this, v => captured = v ?? new());
        var cut = _ctx.Render<L.Calendar>(p =>
        {
            p.Add(c => c.IsMultiple, true);
            p.Add(c => c.Value, Anchor);
            p.Add(c => c.MaxDate, new DateOnly(2024, 6, 15));
            p.Add(c => c.Values, new List<DateOnly>());
            p.Add(c => c.ValuesChanged, cb);
        });

        // The 20th is past MaxDate → its button is disabled; clicking is a no-op.
        var twenty = cut.FindAll("button").First(b => b.TextContent.Trim() == "20" && b.HasAttribute("disabled"));
        twenty.Click();
        Assert.Empty(captured);
    }
}
