namespace Lumeo;

/// <summary>Instance-bound drag state for one DataGrid&lt;TItem&gt;. Replaces the
/// static ColumnDragState holder that previously lived in DataGridHeaderCell.
/// Each DataGrid creates exactly one instance and cascades it; header cells
/// guard their drop logic with SourceGridId == OwnerGridId so cross-instance
/// drags are rejected.
/// <para>
/// Row drag state used to live here too (SourceRowIndex / StartRowDrag /
/// ResetRow / IsOwnDragRow), backing DataGridRow's native HTML5 DnD. The
/// ReUI-parity pass replaced row reorder with the unified pointer-based engine
/// (registerRowReorder in components.js, driven entirely by JS + a single
/// commit call to DataGrid.ReorderRowByKeyAsync) — there's no drag state left
/// for rows to hold here, only the column drag-to-group-panel gesture below.
/// </para></summary>
public sealed class DataGridDragState
{
    public string OwnerGridId { get; }

    public int SourceColumnIndex { get; set; } = -1;
    public string? SourceColumnId { get; set; }

    /// <summary>The grid that originated the drag. Cells MUST verify this
    /// matches their owner before applying a drop. Reset to null on
    /// ResetColumn().</summary>
    public string? SourceGridId { get; set; }

    public DataGridDragState(string ownerGridId)
    {
        OwnerGridId = ownerGridId;
    }

    public void StartColumnDrag(int sourceIndex, string sourceColumnId)
    {
        SourceColumnIndex = sourceIndex;
        SourceColumnId = sourceColumnId;
        SourceGridId = OwnerGridId;
    }

    public void ResetColumn()
    {
        SourceColumnIndex = -1;
        SourceColumnId = null;
        SourceGridId = null;
    }

    /// <summary>True only if a drag is in progress AND it originated in this
    /// grid instance. Drop handlers should bail out when this is false.</summary>
    public bool IsOwnDragColumn(int columnIndex) =>
        SourceGridId == OwnerGridId && SourceColumnIndex >= 0;
}
