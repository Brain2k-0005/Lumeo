# Lumeo.DataGrid.Export

Excel and PDF export for [Lumeo.DataGrid](https://www.nuget.org/packages/Lumeo.DataGrid).
Add this package only if you need those formats — CSV export is built into
`Lumeo.DataGrid` and needs nothing extra. The export backend self-registers with the core
`IDataGridExportService` the moment this assembly loads (eagerly, or lazy-loaded on
WebAssembly) — no `Program.cs` wiring required beyond the core `AddLumeo()` call.

## Install

```bash
dotnet add package Lumeo
dotnet add package Lumeo.DataGrid
dotnet add package Lumeo.DataGrid.Export
```

```csharp
// Program.cs
builder.Services.AddLumeo();   // IDataGridExportService is already registered by this call
```

## Usage

```razor
@using Lumeo

<DataGrid TItem="Employee" Items="@employees" ShowToolbar="true"
          ExportFormats="DataGridExportFormat.Csv | DataGridExportFormat.Excel | DataGridExportFormat.Pdf">
    <DataGridColumnDef TItem="Employee" Title="Name" Field="Name" />
</DataGrid>
```

> In Blazor WebAssembly, drop `DataGridExportFormat.Pdf` from `ExportFormats` — QuestPDF
> throws `PlatformNotSupportedException` there. Excel (ClosedXML) and CSV work on both
> hosting models.

Full docs: https://lumeo.nativ.sh/components/data-grid
