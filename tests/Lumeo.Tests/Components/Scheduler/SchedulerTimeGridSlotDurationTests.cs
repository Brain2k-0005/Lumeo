using System.Globalization;
using AngleSharp.Dom;
using Bunit;
using Lumeo.Services;
using Lumeo.Tests.Helpers;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using L = Lumeo;

namespace Lumeo.Tests.Components.Scheduler;

/// <summary>
/// ReUI parity — its event calendar exposes "grid interval and snap duration"
/// (https://reui.io/components/event-calendar, Display Controls). Lumeo's first-party time
/// grid was fixed at one hour per row and snapped at a hardcoded 15 minutes, and
/// <c>Scheduler.SlotDuration</c> was documented as ignored on this engine.
///
/// The risk here is alignment, not rendering: the hour labels live in their own gutter
/// column, and the chips drawn on top of the grid are positioned from a
/// pixels-per-minute constant. If rows stop tiling an hour exactly, the labels drift away
/// from the lines they name and every chip lands on the wrong line. Most of what follows
/// pins that rather than the markup.
/// </summary>
public class SchedulerTimeGridSlotDurationTests : IAsyncLifetime
{
    private readonly BunitContext _ctx = new();
    private readonly TrackingInteropService _interop = new();

    public Task InitializeAsync()
    {
        _ctx.AddLumeoServices();
        _ctx.Services.AddSingleton<IComponentInteropService>(_interop);
        return Task.CompletedTask;
    }

    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    private static DateTime D(int y, int m, int d) => new(y, m, d);

    private IRenderedComponent<L.SchedulerTimeGridView> Render(
        TimeSpan? slot = null, TimeSpan? snap = null, TimeOnly? min = null, TimeOnly? max = null) =>
        _ctx.Render<L.SchedulerTimeGridView>(p =>
        {
            p.Add(c => c.AnchorDate, D(2026, 3, 11));
            p.Add(c => c.Days, 1);
            if (slot is not null) p.Add(c => c.SlotDuration, slot);
            if (snap is not null) p.Add(c => c.SnapDuration, snap);
            if (min is not null) p.Add(c => c.SlotMinTime, min);
            if (max is not null) p.Add(c => c.SlotMaxTime, max);
        });

    private static int[] SlotMinutes(IRenderedComponent<L.SchedulerTimeGridView> cut) =>
        cut.FindAll("[data-slot-minute]")
           .Select(c => int.Parse(c.GetAttribute("data-slot-minute")!, CultureInfo.InvariantCulture))
           .ToArray();

    [Fact]
    public void Unset_The_Grid_Is_The_Hour_Grid_It_Has_Always_Been()
    {
        var cut = Render();

        var minutes = SlotMinutes(cut);
        Assert.Equal(24, minutes.Length);
        Assert.Equal(Enumerable.Range(0, 24).Select(h => h * 60), minutes);
        // The older attribute is still the one JS hit-tests on and E2E selects by.
        Assert.Equal(24, cut.FindAll("[data-slot-hour]").Count);
    }

    [Fact]
    public void A_Half_Hour_Grid_Doubles_The_Rows_And_Keeps_Them_On_The_Half_Hour()
    {
        var cut = Render(slot: TimeSpan.FromMinutes(30));

        var minutes = SlotMinutes(cut);
        Assert.Equal(48, minutes.Length);
        Assert.Equal(0, minutes[0]);
        Assert.Equal(30, minutes[1]);
        Assert.Equal(1410, minutes[^1]);
        Assert.All(minutes, m => Assert.Equal(0, m % 30));
    }

    [Fact]
    public void The_Hour_Gutter_Stays_One_Label_Per_Hour()
    {
        // The gutter is what makes a sub-hour grid readable; if it subdivided too, a
        // 15-minute grid would print 96 timestamps down the side.
        var cut = Render(slot: TimeSpan.FromMinutes(15));

        Assert.Equal(96, SlotMinutes(cut).Length);

        var gutter = cut.Find("[role='grid']").PreviousElementSibling!;
        var labels = gutter.Children.Where(c => c.LocalName == "div").ToList();
        Assert.Equal(24, labels.Count);
        Assert.Contains("00:00", labels[0].TextContent);
        Assert.Contains("23:00", labels[^1].TextContent);
    }

    [Fact]
    public void Rows_Tile_An_Hour_Exactly_So_A_Ragged_Value_Rounds_Up()
    {
        // 25 does not divide 60. Rounding UP to 30 keeps the gutter aligned; honouring it
        // literally would leave every hour label 5 minutes adrift by the end of the day.
        var cut = Render(slot: TimeSpan.FromMinutes(25));

        var minutes = SlotMinutes(cut);
        Assert.Equal(48, minutes.Length);
        Assert.Equal(new[] { 0, 30, 60 }, minutes.Take(3));
    }

    [Fact]
    public void Row_Height_Follows_The_Same_Pixels_Per_Minute_The_Chips_Use()
    {
        // 48px per hour is the constant the drag maths and the now-indicator share. A
        // 30-minute row that is not exactly 24px puts every chip on the wrong line.
        var cut = Render(slot: TimeSpan.FromMinutes(30));

        var style = cut.FindAll("[data-slot-minute]")[0].GetAttribute("style")!;
        Assert.Contains("height:24px", style.Replace(" ", ""), StringComparison.Ordinal);
    }

    [Fact]
    public void Hour_Boundaries_Keep_A_Stronger_Rule_Than_Lines_Inside_An_Hour()
    {
        var cut = Render(slot: TimeSpan.FromMinutes(15));

        var cells = cut.FindAll("[data-slot-minute]");
        var onTheHour = cells.First(c => c.GetAttribute("data-slot-minute") == "60");
        var quarterPast = cells.First(c => c.GetAttribute("data-slot-minute") == "75");

        Assert.Contains("border-border/30", onTheHour.GetAttribute("class")!);
        // Must be a class the CSS bundle actually generates — an ungenerated opacity
        // utility leaves border-b on its default colour and the sub-hour lines come out
        // DARKER than the hour rules. This assertion passes on the class name alone, so
        // the generated-CSS side is pinned separately below.
        Assert.Contains("border-border/20", quarterPast.GetAttribute("class")!);
    }

    [Fact]
    public void The_Sub_Hour_Border_Class_Is_One_The_Css_Bundle_Generates()
    {
        // Tailwind emits an opacity utility only for literals it has seen. The view may
        // only use border-border/N values that already exist in the compiled bundle,
        // otherwise the rule is silently absent at runtime.
        var css = File.ReadAllText(Path.Combine(RepoRoot(), "docs", "Lumeo.Docs", "wwwroot", "css", "tailwind.out.css"));
        var cut = Render(slot: TimeSpan.FromMinutes(15));

        foreach (var cell in cut.FindAll("[data-slot-minute]").Take(8))
        {
            var cls = cell.GetAttribute("class")!;
            var token = cls.Split(' ').First(t => t.StartsWith("border-border/", StringComparison.Ordinal));
            var escaped = token.Replace("/", @"\/");
            Assert.True(css.Contains(escaped, StringComparison.Ordinal),
                $"'{token}' is not in the compiled CSS bundle, so it renders as no rule at all.");
        }
    }

    private static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "Lumeo.slnx"))) dir = dir.Parent;
        return dir?.FullName ?? throw new InvalidOperationException("repo root not found");
    }

    [Fact]
    public void A_Sub_Hour_Row_Names_Its_Own_Minute_In_Its_Aria_Label()
    {
        var cut = Render(slot: TimeSpan.FromMinutes(30));

        var half = cut.FindAll("[data-slot-minute]").First(c => c.GetAttribute("data-slot-minute") == "570");
        Assert.Contains("09:30", half.GetAttribute("aria-label")!, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ArrowDown_Moves_One_ROW_Not_One_Hour()
    {
        // The regression this exists for: navigation stepped by hour index, so on a
        // 30-minute grid every arrow press would skip the half-hour row entirely.
        var cut = Render(slot: TimeSpan.FromMinutes(30));
        var first = cut.FindAll("[data-slot-minute]")[0];

        await cut.InvokeAsync(() => first.KeyDown(new KeyboardEventArgs { Key = "ArrowDown" }));

        var focused = cut.FindAll("[data-slot-minute]").First(c => c.GetAttribute("tabindex") == "0");
        Assert.Equal("30", focused.GetAttribute("data-slot-minute"));
    }

    [Fact]
    public async Task Double_Clicking_A_Row_Selects_That_Row()
    {
        L.SchedulerDateRange? selected = null;
        var cut = _ctx.Render<L.SchedulerTimeGridView>(p => p
            .Add(c => c.AnchorDate, D(2026, 3, 11))
            .Add(c => c.Days, 1)
            .Add(c => c.SlotDuration, TimeSpan.FromMinutes(30))
            .Add(c => c.OnDateSelect, (L.SchedulerDateRange r) => selected = r));

        var half = cut.FindAll("[data-slot-minute]").First(c => c.GetAttribute("data-slot-minute") == "570");
        await cut.InvokeAsync(() => half.DoubleClick());

        Assert.NotNull(selected);
        Assert.Equal(new DateTime(2026, 3, 11, 9, 30, 0), selected!.Start);
        Assert.Equal(new DateTime(2026, 3, 11, 10, 0, 0), selected.End);
    }

    [Fact]
    public async Task Without_A_Grid_A_Double_Click_Still_Produces_The_Same_30_Minutes_As_Before()
    {
        // Deliberately unchanged: callers who never asked for a grid interval should not
        // silently start creating hour-long events.
        L.SchedulerDateRange? selected = null;
        var cut = _ctx.Render<L.SchedulerTimeGridView>(p => p
            .Add(c => c.AnchorDate, D(2026, 3, 11))
            .Add(c => c.Days, 1)
            .Add(c => c.OnDateSelect, (L.SchedulerDateRange r) => selected = r));

        await cut.InvokeAsync(() => cut.Find("[data-slot-hour='10']").DoubleClick());

        Assert.NotNull(selected);
        Assert.Equal(new DateTime(2026, 3, 11, 10, 0, 0), selected!.Start);
        Assert.Equal(new DateTime(2026, 3, 11, 10, 30, 0), selected.End);
    }

    [Fact]
    public void Snap_Follows_The_Grid_So_Drags_Land_On_Lines_That_Exist()
    {
        var cut = Render(slot: TimeSpan.FromMinutes(30));

        var options = Assert.IsType<Dictionary<string, object?>>(_interop.LastSchedulerViewsTimeGridDragOptions);
        Assert.Equal(30, options["snapMinutes"]);
    }

    [Fact]
    public void Snap_Can_Still_Be_Finer_Than_The_Grid_When_Asked()
    {
        var cut = Render(slot: TimeSpan.FromMinutes(60), snap: TimeSpan.FromMinutes(5));

        var options = Assert.IsType<Dictionary<string, object?>>(_interop.LastSchedulerViewsTimeGridDragOptions);
        Assert.Equal(5, options["snapMinutes"]);
    }

    [Fact]
    public void Without_A_Grid_Snap_Stays_At_The_15_Minutes_It_Always_Was()
    {
        var cut = Render();

        var options = Assert.IsType<Dictionary<string, object?>>(_interop.LastSchedulerViewsTimeGridDragOptions);
        Assert.Equal(15, options["snapMinutes"]);
    }

    [Fact]
    public void Changing_The_Grid_Re_Registers_The_Drag_Options()
    {
        // Registration is hash-gated. If snap were left out of that hash the grid would
        // redraw at the new interval while JS kept snapping to the old one.
        var cut = Render(slot: TimeSpan.FromMinutes(30));
        Assert.Equal(30, Assert.IsType<Dictionary<string, object?>>(
            _interop.LastSchedulerViewsTimeGridDragOptions)["snapMinutes"]);

        cut.Render(p => p
            .Add(c => c.AnchorDate, D(2026, 3, 11))
            .Add(c => c.Days, 1)
            .Add(c => c.SlotDuration, TimeSpan.FromMinutes(15)));

        Assert.Equal(15, Assert.IsType<Dictionary<string, object?>>(
            _interop.LastSchedulerViewsTimeGridDragOptions)["snapMinutes"]);
    }

    [Fact]
    public async Task Clicking_The_First_Row_Of_A_Shifted_Grid_Focuses_That_Row()
    {
        // Pre-existing bug this change had to fix to work at all: the click handler passed
        // the clock HOUR where a row index was expected, so with SlotMinTime at 08:00 the
        // roving tabindex jumped eight rows down from the cell actually clicked.
        var cut = Render(min: new TimeOnly(8, 0), max: new TimeOnly(18, 0));

        var cells = cut.FindAll("[data-slot-minute]");
        Assert.Equal("480", cells[0].GetAttribute("data-slot-minute"));

        await cut.InvokeAsync(() => cells[0].Click());

        var focused = cut.FindAll("[data-slot-minute]").First(c => c.GetAttribute("tabindex") == "0");
        Assert.Equal("480", focused.GetAttribute("data-slot-minute"));
    }

    [Fact]
    public void A_Shifted_Grid_Still_Starts_And_Ends_On_Whole_Hours()
    {
        // The gutter is one label per hour; rows that began mid-hour would shear the two
        // columns apart from the very first row.
        var cut = Render(slot: TimeSpan.FromMinutes(20), min: new TimeOnly(8, 30), max: new TimeOnly(17, 15));

        var minutes = SlotMinutes(cut);
        Assert.Equal(480, minutes[0]);          // floors to 08:00
        Assert.Equal(0, minutes[0] % 60);
        Assert.Equal(0, (minutes[^1] + 20) % 60); // last row closes a whole hour (18:00)
    }
}
