# Lumeo.Icons.Iconoir

Iconoir pack for Lumeo — 1,383 stroke-based 24px icons (stroke width 1.5) exposed as tree-shakeable Lumeo.IconSource properties on the flat `Iconoir` class (namespace Lumeo.Icons). Use with `<SvgGlyph Svg="@(Iconoir.Home)" />` or `<Icon Svg="@(Iconoir.Heart)" />`. THIRD-PARTY-NOTICES: bundles the Iconoir set under the MIT license — see THIRD-PARTY-NOTICES.txt in the package.

## Install

```bash
dotnet add package Lumeo --prerelease
dotnet add package Lumeo.Icons.Iconoir --prerelease
```

No DI registration needed — icon packs are pure static data, referenced directly from
markup.

## Usage

```razor
@using Lumeo.Icons

<SvgGlyph Svg="@(Iconoir.Home)" />
```

Browse the full glyph set: https://lumeo.nativ.sh/icons
