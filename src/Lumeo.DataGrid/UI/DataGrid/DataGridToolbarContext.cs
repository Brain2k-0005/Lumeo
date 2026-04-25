using Microsoft.AspNetCore.Components;
using Lumeo.Services;

namespace Lumeo;

/// <summary>State + callbacks shared with the extracted toolbar tool components
/// (<see cref="DataGridToolbarFullscreen{TItem}"/>, <see cref="DataGridToolbarExport{TItem}"/>, etc.)
/// via <see cref="CascadingValue{T}"/>. The generic
/// parameter matches the grid's row type so column/selection tools stay type-safe.</summary>
public sealed class DataGridToolbarContext<TItem>
{
    public bool IsExpanded { get; set; }
    public bool Expandable { get; set; }
    public EventCallback OnToggleExpanded { get; set; }

    public List<TItem>? SelectedItems { get; set; }
    public IReadOnlyList<DataGridColumn<TItem>>? EffectiveColumns { get; set; }
    public EventCallback<DataGridColumn<TItem>> OnColumnToggle { get; set; }
    public EventCallback<ColumnReorderEventArgs> OnColumnReorder { get; set; }

    public DataGridExportFormat ExportFormats { get; set; } = DataGridExportFormat.All;
    public EventCallback<string> OnExport { get; set; }

    public bool EnableLayoutPersistence { get; set; }
    public List<DataGridNamedLayout>? GlobalLayouts { get; set; }
    public EventCallback OnSaveLayout { get; set; }
    public EventCallback OnResetLayout { get; set; }
    public EventCallback<DataGridNamedLayout> OnSaveNamedLayout { get; set; }
    public EventCallback<string> OnDeleteNamedLayout { get; set; }
    public EventCallback<DataGridNamedLayout> OnApplyNamedLayout { get; set; }
    public string? LayoutStorageKey { get; set; }
    public EventCallback<Exception> OnError { get; set; }

    public ComponentInteropService Interop { get; set; } = default!;
    public Services.Localization.ILumeoLocalizer Localizer { get; set; } = default!;
}
