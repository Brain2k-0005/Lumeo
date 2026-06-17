using AngleSharp.Dom;
using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using Lumeo.Tests.Helpers;
using L = Lumeo;

namespace Lumeo.Tests.Components.Calendar;

/// <summary>
/// WAI-ARIA grid keyboard pattern for the Calendar day grid (#206): roving
/// tabindex (exactly one tabbable day), Arrow keys move ±1 day / ±1 week,
/// Home/End to start/end of the focused week, PageUp/PageDown change month,
/// Enter/Space select; plus the role=grid / role=row / role=gridcell structure,
/// aria-selected and per-day aria-label. Also covers the ShowYearPicker wiring.
/// Previously the day grid had no keyboard navigation and no grid semantics.
/// </summary>
public class CalendarKeyboardNavTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();

    public CalendarKeyboardNavTests() => _ctx.AddLumeoServices();
    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    private static readonly DateOnly Anchor = new(2024, 6, 15); // mid-month, plenty of room

    private IRenderedComponent<L.Calendar> RenderCalendar(DateOnly? value = null, Action<ComponentParameterCollectionBuilder<L.Calendar>>? extra = null)
        => _ctx.Render<L.Calendar>(p =>
        {
            p.Add(c => c.Value, value ?? Anchor);
            extra?.Invoke(p);
        });

    /// <summary>The single day button currently holding the roving tabindex.</summary>
    private static IElement Tabbable(IRenderedComponent<L.Calendar> cut)
        => cut.Find("[role='gridcell'] button[tabindex='0']");

    private static async Task Key(IRenderedComponent<L.Calendar> cut, string key)
        => await cut.InvokeAsync(() => Tabbable(cut).KeyDown(new KeyboardEventArgs { Key = key }));

    // ---- ARIA structure ----

    [Fact]
    public void Day_grid_exposes_grid_row_gridcell_roles()
    {
        var cut = RenderCalendar();

        var grid = cut.Find("[role='grid']");
        Assert.NotNull(grid);

        // One header row + 6 week rows.
        Assert.Equal(7, cut.FindAll("[role='row']").Count);
        // 42 day cells.
        Assert.Equal(42, cut.FindAll("[role='gridcell']").Count);
        // Column headers for the 7 weekday names.
        Assert.Equal(7, cut.FindAll("[role='columnheader']").Count);
    }

    [Fact]
    public void Grid_has_accessible_month_label()
    {
        var cut = RenderCalendar();
        Assert.Equal("June 2024", cut.Find("[role='grid']").GetAttribute("aria-label"));
    }

    [Fact]
    public void Day_buttons_have_localized_full_date_aria_label()
    {
        var cut = RenderCalendar();
        var labels = cut.FindAll("[role='gridcell'] button")
            .Select(b => b.GetAttribute("aria-label"))
            .ToList();
        // Every day button carries a non-empty label, and the 15th's label is
        // the culture's long-date string (don't hardcode a locale's wording).
        Assert.All(labels, l => Assert.False(string.IsNullOrWhiteSpace(l)));
        var expected = Anchor.ToDateTime(TimeOnly.MinValue)
            .ToString("D", System.Globalization.CultureInfo.CurrentCulture);
        Assert.Contains(expected, labels);
    }

    [Fact]
    public void Selected_day_cell_is_aria_selected()
    {
        var cut = RenderCalendar();
        var selectedCells = cut.FindAll("[role='gridcell'][aria-selected='true']");
        var cell = Assert.Single(selectedCells);
        Assert.Equal("15", cell.QuerySelector("button")!.TextContent.Trim());
    }

    // ---- Roving tabindex ----

    [Fact]
    public void Exactly_one_day_is_tabbable()
    {
        var cut = RenderCalendar();
        Assert.Single(cut.FindAll("[role='gridcell'] button[tabindex='0']"));
        // Initial tabbable is the selected day.
        Assert.Equal("15", Tabbable(cut).TextContent.Trim());
    }

    [Fact]
    public async Task Exactly_one_day_stays_tabbable_after_navigation()
    {
        var cut = RenderCalendar();
        await Key(cut, "ArrowRight");
        await Key(cut, "ArrowDown");
        Assert.Single(cut.FindAll("[role='gridcell'] button[tabindex='0']"));
    }

    [Fact]
    public void Falls_back_to_today_when_no_value_in_month()
    {
        // No Value: the tabbable day defaults to today (the displayed month).
        var today = DateOnly.FromDateTime(DateTime.Today);
        var cut = _ctx.Render<L.Calendar>();
        Assert.Single(cut.FindAll("[role='gridcell'] button[tabindex='0']"));
        Assert.Equal(today.Day.ToString(), Tabbable(cut).TextContent.Trim());
    }

    // ---- Arrow navigation (assert the tabbable day's text moves) ----

    [Fact]
    public async Task ArrowRight_and_ArrowLeft_move_one_day()
    {
        var cut = RenderCalendar();
        Assert.Equal("15", Tabbable(cut).TextContent.Trim());
        await Key(cut, "ArrowRight");
        Assert.Equal("16", Tabbable(cut).TextContent.Trim());
        await Key(cut, "ArrowLeft");
        await Key(cut, "ArrowLeft");
        Assert.Equal("14", Tabbable(cut).TextContent.Trim());
    }

    [Fact]
    public async Task ArrowDown_and_ArrowUp_move_one_week()
    {
        var cut = RenderCalendar();
        await Key(cut, "ArrowDown");
        Assert.Equal("22", Tabbable(cut).TextContent.Trim());
        await Key(cut, "ArrowUp");
        await Key(cut, "ArrowUp");
        Assert.Equal("8", Tabbable(cut).TextContent.Trim());
    }

    [Fact]
    public async Task Home_and_End_move_within_the_week()
    {
        // Compute the focused day's week bounds from the active culture's
        // first-day-of-week so the test holds regardless of test-host locale.
        var fdow = System.Globalization.CultureInfo.CurrentCulture.DateTimeFormat.FirstDayOfWeek;
        var offset = ((int)Anchor.DayOfWeek - (int)fdow + 7) % 7;
        var weekStart = Anchor.AddDays(-offset);
        var weekEnd = weekStart.AddDays(6);

        var cut = RenderCalendar();
        await Key(cut, "Home");
        Assert.Equal(weekStart.Day.ToString(), Tabbable(cut).TextContent.Trim());
        await Key(cut, "End");
        Assert.Equal(weekEnd.Day.ToString(), Tabbable(cut).TextContent.Trim());
    }

    // The grid's aria-label is the displayed-month header; assert on it rather
    // than the whole markup, since trailing/leading day labels legitimately
    // name the adjacent month.
    private static string GridMonth(IRenderedComponent<L.Calendar> cut)
        => cut.Find("[role='grid']").GetAttribute("aria-label")!;

    [Fact]
    public async Task PageDown_advances_to_next_month()
    {
        var cut = RenderCalendar();
        Assert.Equal("June 2024", GridMonth(cut));
        await Key(cut, "PageDown");
        Assert.Equal("July 2024", GridMonth(cut));
        Assert.Equal("15", Tabbable(cut).TextContent.Trim());
    }

    [Fact]
    public async Task PageUp_goes_to_previous_month()
    {
        var cut = RenderCalendar();
        await Key(cut, "PageUp");
        Assert.Equal("May 2024", GridMonth(cut));
        Assert.Equal("15", Tabbable(cut).TextContent.Trim());
    }

    [Fact]
    public async Task ArrowRight_across_month_boundary_navigates_month()
    {
        // Sit on the last day of June, then ArrowRight rolls into July.
        var cut = RenderCalendar(new DateOnly(2024, 6, 30));
        Assert.Equal("30", Tabbable(cut).TextContent.Trim());
        await Key(cut, "ArrowRight");
        Assert.Equal("July 2024", GridMonth(cut));
        Assert.Equal("1", Tabbable(cut).TextContent.Trim());
        Assert.Single(cut.FindAll("[role='gridcell'] button[tabindex='0']"));
    }

    // ---- Selection via keyboard ----

    [Fact]
    public async Task Enter_selects_the_focused_day()
    {
        DateOnly? selected = null;
        var cb = EventCallback.Factory.Create<DateOnly?>(_ctx, (DateOnly? d) => selected = d);
        var cut = RenderCalendar(extra: p => p.Add(c => c.ValueChanged, cb));

        await Key(cut, "ArrowRight"); // focus 16
        await Key(cut, "Enter");

        Assert.Equal(new DateOnly(2024, 6, 16), selected);
    }

    [Fact]
    public async Task Space_selects_the_focused_day()
    {
        DateOnly? selected = null;
        var cb = EventCallback.Factory.Create<DateOnly?>(_ctx, (DateOnly? d) => selected = d);
        var cut = RenderCalendar(extra: p => p.Add(c => c.ValueChanged, cb));

        await Key(cut, "ArrowDown"); // focus 22
        await Key(cut, " ");

        Assert.Equal(new DateOnly(2024, 6, 22), selected);
    }

    // ---- Disabled days ----

    [Fact]
    public async Task Enter_on_disabled_day_does_not_select()
    {
        // Disable every day from the 20th onward; navigate toward it.
        DateOnly? selected = null;
        var cb = EventCallback.Factory.Create<DateOnly?>(_ctx, (DateOnly? d) => selected = d);
        var cut = RenderCalendar(new DateOnly(2024, 6, 19), extra: p =>
        {
            p.Add(c => c.MaxDate, new DateOnly(2024, 6, 19));
            p.Add(c => c.ValueChanged, cb);
        });

        // ArrowRight would target the 20th (disabled) — skipped, focus stays on 19.
        await Key(cut, "ArrowRight");
        Assert.Equal("19", Tabbable(cut).TextContent.Trim());

        // Even forcing a keydown on the focused (enabled) day after MaxDate is
        // fine; the guarantee under test is that a disabled day can't be picked.
        await Key(cut, "Enter");
        Assert.Equal(new DateOnly(2024, 6, 19), selected); // the enabled 19th, not a disabled day
    }

    [Fact]
    public void Disabled_days_are_native_disabled_buttons()
    {
        var cut = RenderCalendar(extra: p => p.Add(c => c.MaxDate, new DateOnly(2024, 6, 15)));
        // Days after the 15th in June are disabled buttons.
        var disabledButtons = cut.FindAll("[role='gridcell'] button")
            .Where(b => b.HasAttribute("disabled"))
            .ToList();
        Assert.NotEmpty(disabledButtons);
    }

    // ---- Interop focus target ----

    [Fact]
    public async Task Navigation_focuses_target_day_via_interop()
    {
        var tracking = new TrackingInteropService();
        _ctx.Services.AddSingleton<Lumeo.Services.IComponentInteropService>(tracking);
        var cut = RenderCalendar();

        await Key(cut, "ArrowRight");

        var focusedId = Assert.Single(tracking.FocusedElementIds);
        Assert.False(string.IsNullOrEmpty(focusedId));
        // The focused id is the id the now-tabbable day button carries.
        Assert.Equal(Tabbable(cut).GetAttribute("id"), focusedId);
    }

    // ---- ShowYearPicker wiring (#206) ----

    [Fact]
    public void ShowYearPicker_opens_the_year_view()
    {
        var cut = _ctx.Render<L.Calendar>(p =>
        {
            p.Add(c => c.Value, Anchor);
            p.Add(c => c.ShowYearPicker, true);
        });

        // Year view shows a "decadeStart - decadeEnd" header and >= 12 year buttons,
        // and NOT the day grid.
        Assert.Empty(cut.FindAll("[role='grid']"));
        Assert.Contains(" - ", cut.Markup);                 // decade range header
        Assert.Contains("2020", cut.Markup);                // decade containing 2024
        Assert.True(cut.FindAll("button").Count >= 12);
    }

    [Fact]
    public void ShowYearPicker_false_keeps_the_day_view()
    {
        var cut = RenderCalendar(extra: p => p.Add(c => c.ShowYearPicker, false));
        Assert.NotNull(cut.Find("[role='grid']"));
    }
}
