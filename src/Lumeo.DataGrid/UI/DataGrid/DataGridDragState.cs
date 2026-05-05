namespace Lumeo;

/// <summary>Instance-bound drag state for one DataGrid&lt;TItem&gt;. Replaces the
/// static ColumnDragState / DragState holders that previously lived in
/// DataGridHeaderCell + DataGridRow. Each DataGrid creates exactly one
/// instance and cascades it; cells / rows guard their drop logic with
/// SourceGridId == OwnerGridId so cross-instance drags are rejected.</summary>
public sealed class DataGridDragState
{
    public string OwnerGridId { get; }

    public int SourceColumnIndex { get; set; } = -1;
    public string? SourceColumnId { get; set; }

    public int SourceRowIndex { get; set; } = -1;

    /// <summary>The grid that originated the drag. Cells / rows MUST
    /// verify this matches their owner before applying a drop. Reset to
    /// null on Reset().</summary>
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

    public void StartRowDrag(int sourceIndex)
    {
        SourceRowIndex = sourceIndex;
        SourceGridId = OwnerGridId;
    }

    public void ResetColumn()
    {
        SourceColumnIndex = -1;
        SourceColumnId = null;
        if (SourceRowIndex < 0) SourceGridId = null;
    }

    public void ResetRow()
    {
        SourceRowIndex = -1;
        if (SourceColumnIndex < 0) SourceGridId = null;
    }

    /// <summary>True only if a drag is in progress AND it originated in this
    /// grid instance. Drop handlers should bail out when this is false.</summary>
    public bool IsOwnDragColumn(int columnIndex) =>
        SourceGridId == OwnerGridId && SourceColumnIndex >= 0;

    public bool IsOwnDragRow() =>
        SourceGridId == OwnerGridId && SourceRowIndex >= 0;
}
