# Lumeo.DataGrid

An enterprise `DataGrid` for [Lumeo](https://www.nuget.org/packages/Lumeo): sort, filter,
inline edit, column pin, row group, drag-to-reorder, virtualization, fullscreen and
Excel/PDF/CSV export. Also ships `DataTable` (simpler, non-paginated) and the standalone
`Filter` components.

Install alongside the Lumeo core package. `IDataGridExportService` is already registered
by the core `AddLumeo()` call — no extra DI wiring for this package.

## Install

```bash
dotnet add package Lumeo
dotnet add package Lumeo.DataGrid
```

```csharp
// Program.cs
builder.Services.AddLumeo();   // also registers IDataGridExportService for this package
```

> Excel/PDF export pulls in ClosedXML (MIT) and QuestPDF (dual-licensed — free under $1M
> revenue, otherwise paid) via the lazy-loaded `Lumeo.DataGrid.Export` assembly. CSV/JSON
> export has no third-party dependency. In Blazor WebAssembly, hide PDF export
> (`ExportFormats="DataGridExportFormat.Csv | DataGridExportFormat.Excel"`) — QuestPDF
> throws `PlatformNotSupportedException` there.

## Usage

```razor
@using Lumeo

<DataGrid TItem="Employee" Items="@employees" PageSize="5" ShowPagination="true">
    <DataGridColumnDef TItem="Employee" Title="Name" Field="Name" Sortable="true" />
    <DataGridColumnDef TItem="Employee" Title="Department" Field="Department" Sortable="true" />
    <DataGridColumnDef TItem="Employee" Title="Salary" Field="Salary" Sortable="true" Format="C0" />
</DataGrid>
```

Full docs: https://lumeo.nativ.sh/components/data-grid
