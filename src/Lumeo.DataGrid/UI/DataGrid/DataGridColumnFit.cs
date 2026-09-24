namespace Lumeo;

/// <summary>
/// Pure width-distribution algorithm behind <see cref="DataGridColumnSizing.FitWithMinimum"/>.
///
/// Non-generic (a plain (Id, Desired, Floor, Ceiling, FillWidth) tuple per column) so it can be
/// unit-tested directly without a rendered <see cref="DataGrid{TItem}"/> — see
/// DataGridColumnFitTests. <see cref="DataGrid{TItem}"/> projects its own
/// <see cref="DataGridColumn{TItem}"/> list into <see cref="ColumnFitSpec"/> and calls
/// <see cref="Compute"/> once per render (DataGrid.ComputeFitColumnWidths).
///
/// The result always assigns EVERY column an explicit pixel width — unlike the CSS-only
/// approach this replaced (min-width alone under table-layout: auto), which left growth
/// entirely up to the browser's content-based auto-layout algorithm. Under table-layout:
/// auto a nowrap cell's minimum content width is its full rendered text width regardless
/// of any width/min-width declared on the cell, so that approach could never actually cap
/// growth (DocFlow field report, 5.11.0). Computing exact widths here and rendering with
/// table-layout: fixed (DataGrid.TableStyle / DataGridHeaderCell.StyleString) gives a real
/// ceiling: fixed layout uses ONLY the widths handed to it, never content.
/// </summary>
internal static class DataGridColumnFit
{
    /// <summary>A column's sizing inputs for one distribution pass.</summary>
    /// <param name="Id">DataGridColumn.Id — the dictionary key in the result.</param>
    /// <param name="Desired">
    /// The column's preferred width: the live <c>Column.Width</c> (already holds a
    /// user-resized or LayoutStorageKey-restored value once either has happened — see
    /// DataGrid.ApplyColumnWidth) falling back to <c>Column.MinWidth</c>, falling back to
    /// <see cref="DefaultDesiredWidth"/> for a column that declares neither.
    /// </param>
    /// <param name="Floor">
    /// The column's hard minimum: <c>Column.MinWidth</c>, falling back to <c>Column.Width</c>
    /// when MinWidth isn't set (a column with only Width declared still never shrinks below
    /// it — the pre-existing Auto-mode contract for a single-Width column).
    /// </param>
    /// <param name="Ceiling">Optional <c>Column.MaxWidth</c> — never exceeded.</param>
    /// <param name="FillWidth">
    /// When true and there's surplus room, this column absorbs it before any other column
    /// grows — mirrors <see cref="DataGridColumn{TItem}.FillWidth"/>'s Auto-mode meaning.
    /// </param>
    internal readonly record struct ColumnFitSpec(string Id, double Desired, double Floor, double? Ceiling, bool FillWidth);

    /// <summary>Desired width assumed for a column that declares neither Width nor MinWidth.</summary>
    internal const double DefaultDesiredWidth = 150;

    private const double Epsilon = 0.01;

    internal static readonly IReadOnlyDictionary<string, double> Empty = new Dictionary<string, double>();

    /// <summary>
    /// Computes each column's negotiated pixel width for a container of
    /// <paramref name="containerWidth"/>px:
    ///
    /// - Sum of floors ≥ container: every column takes exactly its own floor. The table
    ///   (sum of the returned widths) then exceeds the container and the grid's existing
    ///   horizontal scrollbar takes over — never a column below its declared MinWidth.
    /// - Sum of desired widths ≤ container: every column starts at its desired width and the
    ///   remaining space is distributed as growth (a FillWidth column first, proportionally to
    ///   desired width otherwise), clamped to Ceiling — so the table exactly fills the
    ///   container when there's room and at least one column can still grow.
    /// - Otherwise: every column starts at its desired width and the shortfall is removed
    ///   proportionally to each column's headroom (desired − floor), clamped at Floor — the
    ///   table again exactly fills the container, with no column pushed below its floor.
    /// </summary>
    internal static IReadOnlyDictionary<string, double> Compute(IReadOnlyList<ColumnFitSpec> columns, double containerWidth)
    {
        if (columns.Count == 0) return Empty;
        containerWidth = Math.Max(containerWidth, 0);

        // Normalize: Desired always within [Floor, Ceiling] so the two distribution passes
        // below never have to special-case a misconfigured column (e.g. MinWidth > Width).
        var specs = new ColumnFitSpec[columns.Count];
        for (var i = 0; i < columns.Count; i++)
        {
            var c = columns[i];
            var ceiling = c.Ceiling.HasValue ? Math.Max(c.Ceiling.Value, c.Floor) : (double?)null;
            var desired = Math.Max(c.Desired, c.Floor);
            if (ceiling.HasValue) desired = Math.Min(desired, ceiling.Value);
            specs[i] = c with { Desired = desired, Ceiling = ceiling };
        }

        var result = new Dictionary<string, double>(specs.Length);
        var sumFloor = 0.0;
        var sumDesired = 0.0;
        foreach (var c in specs) { sumFloor += c.Floor; sumDesired += c.Desired; }

        if (sumFloor >= containerWidth - Epsilon)
        {
            foreach (var c in specs) result[c.Id] = c.Floor;
            return result;
        }

        foreach (var c in specs) result[c.Id] = c.Desired;

        if (sumDesired <= containerWidth + Epsilon)
        {
            DistributeGrowth(specs, result, containerWidth - sumDesired);
        }
        else
        {
            DistributeShrink(specs, result, sumDesired - containerWidth);
        }
        return result;
    }

    /// <summary>Grows FillWidth columns first (if any). Once every FillWidth column has
    /// saturated at its Ceiling (or there is none), any remaining surplus falls through to
    /// every OTHER column, weighted by its own desired width — so a FillWidth column with a
    /// low MaxWidth doesn't strand surplus space nobody else can claim.</summary>
    private static void DistributeGrowth(ColumnFitSpec[] specs, Dictionary<string, double> result, double surplus)
    {
        if (surplus <= Epsilon) return;

        var fillTargets = Array.FindAll(specs, c => c.FillWidth);
        if (fillTargets.Length > 0)
        {
            surplus = GrowFlexible(fillTargets, result, surplus);
            if (surplus <= Epsilon) return;
            var rest = Array.FindAll(specs, c => !c.FillWidth);
            if (rest.Length == 0) return; // nowhere else for the leftover surplus to go
            GrowFlexible(rest, result, surplus);
            return;
        }

        GrowFlexible(specs, result, surplus);
    }

    /// <summary>Distributes <paramref name="surplus"/> across <paramref name="candidates"/>,
    /// weighted by each column's own desired width. Iterative water-filling: a column that
    /// hits its Ceiling drops out and the remaining surplus is re-distributed among the rest.
    /// Returns whatever surplus couldn't be placed (every candidate saturated).</summary>
    private static double GrowFlexible(ColumnFitSpec[] candidates, Dictionary<string, double> result, double surplus)
    {
        var flexible = new List<ColumnFitSpec>(candidates);
        while (surplus > Epsilon && flexible.Count > 0)
        {
            var weightSum = 0.0;
            foreach (var c in flexible) weightSum += Math.Max(c.Desired, 1);

            var stillFlexible = new List<ColumnFitSpec>(flexible.Count);
            var consumed = 0.0;
            foreach (var c in flexible)
            {
                var share = surplus * (Math.Max(c.Desired, 1) / weightSum);
                var proposed = result[c.Id] + share;
                if (c.Ceiling is double max && proposed >= max - Epsilon)
                {
                    consumed += Math.Max(max - result[c.Id], 0);
                    result[c.Id] = max;
                }
                else
                {
                    result[c.Id] = proposed;
                    consumed += share;
                    stillFlexible.Add(c);
                }
            }

            surplus -= consumed;
            // Nobody was clamped this round but surplus remains (rounding) — stop rather
            // than spin forever.
            if (stillFlexible.Count == flexible.Count) break;
            flexible = stillFlexible;
        }
        return surplus;
    }

    /// <summary>Shrinks every column with headroom (desired &gt; floor), weighted by that
    /// headroom. Iterative water-filling: a column that hits its Floor drops out and the
    /// remaining deficit is re-distributed among the rest.</summary>
    private static void DistributeShrink(ColumnFitSpec[] specs, Dictionary<string, double> result, double deficit)
    {
        if (deficit <= Epsilon) return;

        var flexible = new List<ColumnFitSpec>();
        foreach (var c in specs)
            if (c.Desired - c.Floor > Epsilon) flexible.Add(c);

        while (deficit > Epsilon && flexible.Count > 0)
        {
            var weightSum = 0.0;
            foreach (var c in flexible) weightSum += (c.Desired - c.Floor);

            var stillFlexible = new List<ColumnFitSpec>(flexible.Count);
            var absorbed = 0.0;
            foreach (var c in flexible)
            {
                var headroom = c.Desired - c.Floor;
                var share = deficit * (headroom / weightSum);
                var proposed = result[c.Id] - share;
                if (proposed <= c.Floor + Epsilon)
                {
                    absorbed += Math.Max(result[c.Id] - c.Floor, 0);
                    result[c.Id] = c.Floor;
                }
                else
                {
                    result[c.Id] = proposed;
                    absorbed += share;
                    stillFlexible.Add(c);
                }
            }

            deficit -= absorbed;
            if (stillFlexible.Count == flexible.Count) break;
            flexible = stillFlexible;
        }
    }
}
