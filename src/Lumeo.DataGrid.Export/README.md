# Lumeo.DataGrid.Export

**Internal helper project — not a published NuGet package.**

The compiled `Lumeo.DataGrid.Export.dll` is bundled into `Lumeo.DataGrid.nupkg`
(see `Lumeo.DataGrid.csproj` → `BundleExportAssembly` target). Consumers install
the single `Lumeo.DataGrid` package and receive two DLLs:

- `Lumeo.DataGrid.dll` — components, eager.
- `Lumeo.DataGrid.Export.dll` — Excel (ClosedXML) + PDF (QuestPDF) backend.
  Lazy-loadable on Blazor WebAssembly so its ~1.6 MB of dependencies stay out
  of the initial download.

## Enabling lazy loading (Blazor WebAssembly, optional)

In your WASM app's `.csproj`:

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

In `Program.cs`:

```csharp
using Lumeo.Services;
using Microsoft.AspNetCore.Components.WebAssembly.Services;

var host = builder.Build();
var lazy = host.Services.GetRequiredService<LazyAssemblyLoader>();
DataGridExportLoader.LoadAssembliesAsync = names => lazy.LoadAssembliesAsync(names);
await host.RunAsync();
```

The DataGrid loads the required assemblies the first time a user exports.
Without this setup everything still works — the assemblies just load eagerly
at startup, same behaviour as before.
