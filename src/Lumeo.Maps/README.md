# Lumeo.Maps

`Map` — modern WebGL maps for [Lumeo](https://www.nuget.org/packages/Lumeo), powered by
MapLibre GL with free CARTO basemaps (no API key required). Markers, routes, polygons,
circles, arcs, clustering and shadcn-style controls included.

Install alongside the Lumeo core package — it registers no services of its own.

## Install

```bash
dotnet add package Lumeo --prerelease
dotnet add package Lumeo.Maps --prerelease
```

```csharp
// Program.cs
builder.Services.AddLumeo();   // the only DI call — core and every satellite share it
```

## Usage

```razor
@using Lumeo

<Map Center="(48.8566, 2.3522)" Zoom="12" Height="420px" />
```

Full docs: https://lumeo.nativ.sh/components/map
