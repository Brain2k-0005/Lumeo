using Lumeo;
using Xunit;

namespace Lumeo.Tests.Components.DataGrid;

/// <summary>
/// Pure unit tests for the width-distribution algorithm behind
/// <see cref="DataGridColumnSizing.FitWithMinimum"/> — no rendering, no JS, just
/// <see cref="DataGridColumnFit.Compute"/> against hand-built specs. See
/// DataGridFitWithMinimumWidthsTests for the end-to-end bUnit coverage (JSInvokable
/// callback wired into a rendered DataGrid, header style strings, resize/reset/autosize
/// interplay).
///
/// DocFlow field report against 5.11.0: with ColumnSizing="FitWithMinimum",
/// DataGridHeaderCell.StyleString emitted ONLY `min-width` under `table-layout: auto` —
/// never a `width` — so (1) a nowrap cell's content-driven minimum width always won,
/// blowing a 12-column, 1310px-container search grid out to 1512/2035px, and (2) a
/// user-resized or LayoutStorageKey-restored width was never reflected in any CSS
/// property at all (MinWidth ?? Width unconditionally preferred MinWidth), so a drag to
/// 90px "jumped back" on reload. This class exercises the replacement mechanism: compute
/// an explicit pixel width per column from the measured container width, rendered with
/// table-layout: fixed (a real ceiling, since fixed layout uses only given widths, never
/// content).
/// </summary>
public class DataGridColumnFitTests
{
    private static DataGridColumnFit.ColumnFitSpec Spec(
        string id, double desired, double? min = null, double? max = null, bool fillWidth = false) =>
        new(id, desired, min ?? 0, max, fillWidth);

    // --- Fit case: sum(desired) <= container, no FillWidth column — surplus is
    // distributed proportionally so the table fills the container exactly. ---

    [Fact]
    public void Fits_Exactly_Distributes_Surplus_Proportionally_When_Room()
    {
        var specs = new[]
        {
            Spec("a", desired: 150, min: 100),
            Spec("b", desired: 150, min: 100),
        };

        var result = DataGridColumnFit.Compute(specs, containerWidth: 400);

        // 100px surplus over 300 desired, split 50/50 (equal desired weights).
        Assert.Equal(200, result["a"], 3);
        Assert.Equal(200, result["b"], 3);
        Assert.Equal(400, result.Values.Sum(), 3);
    }

    [Fact]
    public void Twelve_Column_Search_Grid_Fills_Exactly_1310px()
    {
        // Mirrors the DocFlow repro: 12 short nowrap columns, no FillWidth, that used to
        // render at their nowrap content width (1512px) instead of filling the container.
        var specs = new DataGridColumnFit.ColumnFitSpec[12];
        for (var i = 0; i < 12; i++)
            specs[i] = Spec($"c{i}", desired: 100, min: 80);

        var result = DataGridColumnFit.Compute(specs, containerWidth: 1310);

        Assert.Equal(1310, result.Values.Sum(), 3);
        Assert.All(result.Values, w => Assert.True(w >= 80 - 0.01));
    }

    [Fact]
    public void FillWidth_Column_Absorbs_Surplus_Before_Other_Columns_Grow()
    {
        var specs = new[]
        {
            Spec("name", desired: 200, min: 120, fillWidth: true),
            Spec("city", desired: 150, min: 100),
        };

        var result = DataGridColumnFit.Compute(specs, containerWidth: 500);

        // 150px surplus (500 - 350) goes entirely to the FillWidth column.
        Assert.Equal(350, result["name"], 3);
        Assert.Equal(150, result["city"], 3);
    }

    [Fact]
    public void FillWidth_Column_Clamped_At_MaxWidth_Redistributes_Remainder()
    {
        var specs = new[]
        {
            Spec("name", desired: 200, min: 120, max: 260, fillWidth: true),
            Spec("city", desired: 150, min: 100),
        };

        var result = DataGridColumnFit.Compute(specs, containerWidth: 500);

        // FillWidth column can only take 60px (200 -> 260 ceiling); the remaining 90px
        // surplus falls through to the only other flexible column.
        Assert.Equal(260, result["name"], 3);
        Assert.Equal(240, result["city"], 3);
        Assert.Equal(500, result.Values.Sum(), 3);
    }

    [Fact]
    public void No_FillWidth_And_Every_Column_At_MaxWidth_Table_Stays_Narrower_Than_Container()
    {
        var specs = new[]
        {
            Spec("a", desired: 100, min: 80, max: 100),
            Spec("b", desired: 100, min: 80, max: 100),
        };

        var result = DataGridColumnFit.Compute(specs, containerWidth: 400);

        // Both columns are already at their ceiling — nowhere for the 200px surplus to
        // go. Matches Auto mode's own no-FillWidth contract: the table may sit narrower
        // than its container.
        Assert.Equal(100, result["a"], 3);
        Assert.Equal(100, result["b"], 3);
    }

    // --- Overflow case: sum(MinWidth) > container — every column pinned to its floor,
    // table grows past the container and the grid's horizontal scrollbar takes over. ---

    [Fact]
    public void Sum_Of_MinWidth_Exceeds_Container_Every_Column_Takes_Its_Floor()
    {
        var specs = new[]
        {
            Spec("a", desired: 300, min: 200),
            Spec("b", desired: 300, min: 200),
        };

        var result = DataGridColumnFit.Compute(specs, containerWidth: 300);

        Assert.Equal(200, result["a"], 3);
        Assert.Equal(200, result["b"], 3);
        // Table (400) exceeds the container (300) — the caller's existing
        // overflow-x-auto scroll wrapper is what makes this scroll.
        Assert.True(result.Values.Sum() > 300);
    }

    // --- Shrink case: container between sum(MinWidth) and sum(desired) — deficit removed
    // proportionally to headroom, clamped at each column's own floor. ---

    [Fact]
    public void Shrink_Distributes_Deficit_By_Headroom_Never_Below_Floor()
    {
        var specs = new[]
        {
            Spec("a", desired: 300, min: 100), // 200 headroom
            Spec("b", desired: 100, min: 100), // 0 headroom — must not move
        };

        var result = DataGridColumnFit.Compute(specs, containerWidth: 300);

        // Deficit is 100 (400 desired - 300 container). Only "a" has headroom, so it
        // absorbs the whole deficit; "b" stays exactly at its declared width == floor.
        Assert.Equal(200, result["a"], 3);
        Assert.Equal(100, result["b"], 3);
        Assert.Equal(300, result.Values.Sum(), 3);
    }

    [Fact]
    public void Shrink_Repro_Two_Columns_300px_Container_Matches_DocFlow_Repro()
    {
        // The DocFlow repro's exact numbers: Width=150/MinWidth=100 and
        // Width=100/MinWidth=80, 300px container, content longer than either MinWidth.
        var specs = new[]
        {
            Spec("wide", desired: 150, min: 100),
            Spec("narrow", desired: 100, min: 80),
        };

        var result = DataGridColumnFit.Compute(specs, containerWidth: 300);

        Assert.Equal(300, result.Values.Sum(), 3);
        Assert.True(result["wide"] >= 100 - 0.01);
        Assert.True(result["narrow"] >= 80 - 0.01);
    }

    // --- A resized/layout-restored Desired must win over the declared value, still
    // clamped to Floor/Ceiling by the DataGrid.ComputeFitColumnWidths caller (which is
    // what feeds Desired here — this only asserts the algorithm respects whatever it's
    // given as Desired ahead of any redistribution). ---

    [Fact]
    public void Resized_Desired_Wins_When_There_Is_Room_No_Redistribution_Needed()
    {
        var specs = new[]
        {
            // A drag to 90px, clamped to its own MinWidth of 80 by the caller already.
            Spec("resized", desired: 90, min: 80),
            Spec("other", desired: 100, min: 80),
        };

        var result = DataGridColumnFit.Compute(specs, containerWidth: 190);

        Assert.Equal(90, result["resized"], 3);
        Assert.Equal(100, result["other"], 3);
    }

    [Fact]
    public void Desired_Below_Floor_Is_Normalized_Up_To_Floor()
    {
        // Defensive: a misconfigured column (Desired < Floor) must never render below
        // its own floor.
        var specs = new[] { Spec("a", desired: 50, min: 100) };

        var result = DataGridColumnFit.Compute(specs, containerWidth: 100);

        Assert.Equal(100, result["a"], 3);
    }

    [Fact]
    public void Zero_Columns_Returns_Empty()
    {
        var result = DataGridColumnFit.Compute(Array.Empty<DataGridColumnFit.ColumnFitSpec>(), 500);
        Assert.Empty(result);
    }
}
