# Lumeo.Icons.Phosphor

Phosphor icon pack (Regular weight) for Lumeo — 1,248 fill-style icons exposed as tree-shakeable Lumeo.IconSource properties on the flat `Phosphor` class (namespace Lumeo.Icons). Use with `<SvgGlyph Svg="@(Phosphor.House)" />` or `<Icon Svg="@(Phosphor.House)" />`. Other weights ship as Lumeo.Icons.Phosphor.Bold/Fill/Duotone/Light/Thin. THIRD-PARTY-NOTICES: bundles the Phosphor Icons set under the MIT license — see THIRD-PARTY-NOTICES.txt in the package.

## Install

```bash
dotnet add package Lumeo
dotnet add package Lumeo.Icons.Phosphor
```

No DI registration needed — icon packs are pure static data, referenced directly from
markup.

## Usage

```razor
@using Lumeo.Icons

<SvgGlyph Svg="@(Phosphor.House)" />
```

Browse the full glyph set: https://lumeo.nativ.sh/icons
