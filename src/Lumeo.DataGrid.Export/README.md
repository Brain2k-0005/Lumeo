# Lumeo.DataGrid.Export

Excel (ClosedXML) and PDF (QuestPDF) export backend for
[`Lumeo.DataGrid`](https://www.nuget.org/packages/Lumeo.DataGrid).

You normally don't reference this package directly — it ships transitively with
`Lumeo.DataGrid`. It exists as a separate assembly so its ~1.6 MB of dependencies
can be **lazy-loaded on demand** in Blazor WebAssembly, keeping them out of the
initial download.

## Lazy loading (optional, WebAssembly)

Mark the export assemblies as lazy in your WASM app's `.csproj`:

```xml
<ItemGroup>
  <BlazorWebAssemblyLazyLoad Include="Lumeo.DataGrid.Export.dll" />
  <BlazorWebAssemblyLazyLoad Include="ClosedXML.dll" />
  <BlazorWebAssemblyLazyLoad Include="ClosedXML.Parser.dll" />
  <BlazorWebAssemblyLazyLoad Include="DocumentFormat.OpenXml.dll" />
  <BlazorWebAssemblyLazyLoad Include="DocumentFormat.OpenXml.Framework.dll" />
  <BlazorWebAssemblyLazyLoad Include="ExcelNumberFormat.dll" />
  <BlazorWebAssemblyLazyLoad Include="RBush.dll" />
  <BlazorWebAssemblyLazyLoad Include="SixLabors.Fonts.dll" />
  <BlazorWebAssemblyLazyLoad Include="System.IO.Packaging.dll" />
  <BlazorWebAssemblyLazyLoad Include="QuestPDF.dll" />
</ItemGroup>
```

Then wire the loader hook in `Program.cs`:

```csharp
using Lumeo.Services;
using Microsoft.AspNetCore.Components.WebAssembly.Services;

var host = builder.Build();
var lazy = host.Services.GetRequiredService<LazyAssemblyLoader>();
DataGridExportLoader.LoadAssembliesAsync = names => lazy.LoadAssembliesAsync(names);
await host.RunAsync();
```

The DataGrid loads the required assemblies the first time a user exports. Without
this setup everything still works — the assemblies just load eagerly at startup.
