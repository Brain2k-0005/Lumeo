namespace Lumeo;

/// <summary>
/// Single source of truth for <c>aria-rowindex</c> sequencing inside a single
/// <see cref="DataGridBody{TItem}"/> render pass.
///
/// Contract: <c>aria-rowindex</c> is 1-based over EVERY rendered <c>&lt;tr&gt;</c> in the
/// table, in DOM order — the header row(s) (see <see cref="DataGridContext{TItem}.HeaderRowOffset"/>),
/// group header rows (<see cref="DataGridGroupRow{TItem}"/>), item rows
/// (<see cref="DataGridRow{TItem}"/>), and expanded detail rows (<see cref="DataGridDetailRow{TItem}"/>)
/// alike — per ARIA grid semantics (a row's index is its position among ALL rows the
/// table renders, not just data rows). Before this type existed, each
/// <see cref="DataGridBody{TItem}"/> render branch kept its own local <c>tableRowIdx</c>/
/// <c>globalIdx</c> counters, duplicated near-identically across the multi-level-grouping,
/// single-level-grouping (rendered from two separate branches), and flat/tree-grid
/// branches — the same class of bug (group headers excluded from aria-rowcount; detail
/// rows not counted at all, silently under-reporting every row after an expanded one) had
/// to be independently rediscovered and re-fixed in more than one of those copies across
/// review rounds. Every row type that occupies a real <c>&lt;tr&gt;</c> now allocates its
/// slot from ONE indexer instance, in DOM order, so the counting rule lives in exactly one
/// place and the fix can't drift out of sync between copies again.
///
/// Kept 0-based / offset-free for group and item rows — matching the existing
/// <see cref="DataGridGroupRow{TItem}.RowIndex"/> / <see cref="DataGridRow{TItem}.TableRowIndex"/>
/// contracts, which each add <see cref="DataGridContext{TItem}.HeaderRowOffset"/> themselves —
/// so this is a drop-in replacement for the ad-hoc counters those parameters already
/// accepted. Detail rows have no such existing contract (they never had an
/// <c>aria-rowindex</c> at all before this type), so <see cref="NextItemRow"/> returns their
/// index already offset — the FINAL 1-based <c>aria-rowindex</c> — ready to hand straight to
/// <see cref="DataGridDetailRow{TItem}.AriaRowIndex"/>.
///
/// Not used by the virtualized render branches (<c>UseVirtualization</c> / server
/// <c>ItemsProvider</c> mode): those never render group rows, and a virtualized window's
/// item position can't cheaply account for how many not-currently-rendered items ahead of
/// it have an expanded detail row without an O(n) scan per row — a separate, pre-existing
/// limitation this type does not attempt to solve.
/// </summary>
internal sealed class DataGridRowIndexer
{
    private readonly int _headerRowOffset;
    private int _tableRowIndex;
    private int _itemIndex;

    public DataGridRowIndexer(int headerRowOffset)
    {
        _headerRowOffset = headerRowOffset;
    }

    /// <summary>Allocates a <see cref="DataGridGroupRow{TItem}"/>'s slot. Returns its
    /// 0-based table-wide position — feed directly to <see cref="DataGridGroupRow{TItem}.RowIndex"/>,
    /// which applies <see cref="DataGridContext{TItem}.HeaderRowOffset"/> itself. Does not
    /// advance the item-only counter — a group header is not an item.</summary>
    public int NextGroupRow() => _tableRowIndex++;

    /// <summary>Allocates a <see cref="DataGridRow{TItem}"/>'s slot, and — when
    /// <paramref name="hasDetailRow"/> is true — the <see cref="DataGridDetailRow{TItem}"/>
    /// slot immediately following it (an expanded detail row occupies a real table row
    /// between this item and the next one, so every subsequent allocation must account for
    /// it). Returns the 0-based item-only index (<see cref="DataGridRow{TItem}.RowIndex"/>),
    /// this row's 0-based table-wide position (<see cref="DataGridRow{TItem}.TableRowIndex"/>),
    /// and — only when a detail row was reserved — that detail row's FINAL 1-based
    /// <c>aria-rowindex</c> (<see cref="DataGridDetailRow{TItem}.AriaRowIndex"/>).</summary>
    public (int ItemIndex, int TableRowIndex, int? DetailAriaRowIndex) NextItemRow(bool hasDetailRow)
    {
        var itemIndex = _itemIndex++;
        var tableRowIndex = _tableRowIndex++;
        int? detailAriaRowIndex = null;
        if (hasDetailRow)
        {
            detailAriaRowIndex = _headerRowOffset + _tableRowIndex;
            _tableRowIndex++;
        }
        return (itemIndex, tableRowIndex, detailAriaRowIndex);
    }
}
