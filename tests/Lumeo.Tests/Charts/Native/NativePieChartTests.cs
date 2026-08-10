using Bunit;
using Microsoft.AspNetCore.Components.Web;
using Xunit;
using L = Lumeo;

namespace Lumeo.Tests.Charts.Native;

public class NativePieChartTests
{
    private readonly BunitContext _ctx = new();

    private static List<L.NativePieChart.PieChartData> TwoSlices() => new()
    {
        new() { Name = "A", Value = 30 },
        new() { Name = "B", Value = 70 },
    };

    // Independently re-derives the expected arc path from the SAME core primitive
    // the component itself must call (ChartArcPath.Build) but with angles computed
    // by hand from the SplitByValue formula — this catches wrong-total, wrong-
    // start-angle, or wrong-index bugs, not just "did it call the function".
    [Fact]
    public void Two_Slices_Sweep_Angles_Match_Value_Proportional_Split()
    {
        var cut = _ctx.Render<L.NativePieChart>(p => p.Add(b => b.Data, TwoSlices()));

        const double cx = 210, cy = 140, r = 92;
        var sweepA = 30.0 / 100 * Math.PI * 2; // 0.6π — 30% of the 30/70 split
        var sweepB = 70.0 / 100 * Math.PI * 2; // 1.4π
        var a0A = -Math.PI / 2;
        var a0B = a0A + sweepA;
        var expectedA = L.ChartArcPath.Build(cx, cy, 0, r, a0A, a0A + sweepA);
        var expectedB = L.ChartArcPath.Build(cx, cy, 0, r, a0B, a0B + sweepB);

        var paths = cut.FindAll("path");
        Assert.Equal(2, paths.Count);
        Assert.Equal(expectedA, paths[0].GetAttribute("d"));
        Assert.Equal(expectedB, paths[1].GetAttribute("d"));
    }

    [Fact]
    public void Hiding_A_Segment_Via_Legend_Recomputes_The_Remaining_Segment_As_A_Full_Circle()
    {
        var cut = _ctx.Render<L.NativePieChart>(p => p.Add(b => b.Data, TwoSlices()));

        // Toggle off segment "A" via its legend button.
        var legendButtons = cut.FindAll(".lumeo-chart-legend-item");
        Assert.Equal(2, legendButtons.Count);
        cut.InvokeAsync(() => legendButtons[0].Click());

        var paths = cut.FindAll("path");
        Assert.Single(paths); // only "B" remains, now 100% of the (new) total
        const double cx = 210, cy = 140, r = 92;
        var expected = L.ChartArcPath.Build(cx, cy, 0, r, -Math.PI / 2, -Math.PI / 2 + Math.PI * 2 - 0.0001);
        Assert.Equal(expected, paths[0].GetAttribute("d"));
    }

    [Fact]
    public void Legend_Guards_Against_Hiding_The_Last_Visible_Segment()
    {
        var cut = _ctx.Render<L.NativePieChart>(p => p.Add(b => b.Data, new List<L.NativePieChart.PieChartData> { new() { Name = "Only", Value = 10 } }));

        var legendButton = cut.Find(".lumeo-chart-legend-item");
        cut.InvokeAsync(() => legendButton.Click());

        // ChartLegendState.Toggle refuses to hide the last visible series — the
        // slice must still be rendered, not vanish into an empty chart.
        Assert.Single(cut.FindAll("path"));
    }

    [Fact]
    public void Accessibility_Table_Has_One_Row_Per_Data_Point_And_A_Summarizing_Caption()
    {
        var cut = _ctx.Render<L.NativePieChart>(p => p.Add(b => b.Data, TwoSlices()));

        var rows = cut.FindAll("table.sr-only tbody tr");
        Assert.Equal(2, rows.Count);
        var caption = cut.Find("table.sr-only caption");
        Assert.Contains("2 data points", caption.TextContent);
    }

    [Fact]
    public void Focus_Host_Is_Keyboard_Reachable_With_A_Meaningful_Aria_Label()
    {
        var cut = _ctx.Render<L.NativePieChart>(p => p.Add(b => b.Data, TwoSlices()));

        var host = cut.Find("svg rect[tabindex]");
        Assert.Equal("0", host.GetAttribute("tabindex"));
        Assert.False(string.IsNullOrWhiteSpace(host.GetAttribute("aria-label")));
    }

    [Fact]
    public async Task ArrowRight_On_The_Focus_Host_Activates_The_First_Segment_And_Announces_Its_Value()
    {
        var cut = _ctx.Render<L.NativePieChart>(p => p.Add(b => b.Data, TwoSlices()));

        var host = cut.Find("svg rect[tabindex]");
        await cut.InvokeAsync(() => host.KeyDown("ArrowRight"));

        var status = cut.Find("[role='status']");
        Assert.Contains("A", status.TextContent);
        Assert.Contains("30", status.TextContent);
        // 30/100 = 30% exactly — a total-sum bug (e.g. off-by-one denominator)
        // would shift this to a different number without affecting the raw
        // "30" value assertion above, so this closes that gap.
        Assert.Contains("(30%)", status.TextContent);
    }

    [Fact]
    public async Task ArrowRight_Twice_Moves_To_The_Second_Segment()
    {
        var cut = _ctx.Render<L.NativePieChart>(p => p.Add(b => b.Data, TwoSlices()));

        var host = cut.Find("svg rect[tabindex]");
        await cut.InvokeAsync(() => host.KeyDown("ArrowRight"));
        await cut.InvokeAsync(() => host.KeyDown("ArrowRight"));

        var status = cut.Find("[role='status']");
        Assert.Contains("B", status.TextContent);
        Assert.Contains("70", status.TextContent);
    }

    [Fact]
    public void ShowTooltip_False_Suppresses_The_Status_Readout_On_Hover()
    {
        var cut = _ctx.Render<L.NativePieChart>(p => p
            .Add(b => b.Data, TwoSlices())
            .Add(b => b.ShowTooltip, false));

        var segment = cut.Find("svg path");
        cut.InvokeAsync(() => segment.PointerEnter(new PointerEventArgs()));

        var status = cut.Find("[role='status']");
        Assert.Equal(string.Empty, status.TextContent.Trim());
    }

    [Fact]
    public void No_Legend_When_ShowLegend_Is_False()
    {
        var cut = _ctx.Render<L.NativePieChart>(p => p
            .Add(b => b.Data, TwoSlices())
            .Add(b => b.ShowLegend, false));

        Assert.Empty(cut.FindAll(".lumeo-chart-legend"));
    }
}
