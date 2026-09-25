# Lumeo.Icons.Tabler

Tabler icon pack for Lumeo — 5,093 outline icons (flat `Tabler` class, stroke) plus 1,053 filled icons (`TablerFilled` class) exposed as tree-shakeable Lumeo.IconSource properties (namespace Lumeo.Icons). Use with `<SvgGlyph Svg="@(Tabler.Home)" />` or `<Icon Svg="@(TablerFilled.Heart)" />`. THIRD-PARTY-NOTICES: bundles the Tabler Icons set under the MIT license — see THIRD-PARTY-NOTICES.txt in the package.

## Install

```bash
dotnet add package Lumeo
dotnet add package Lumeo.Icons.Tabler
```

No DI registration needed — icon packs are pure static data, referenced directly from
markup.

## Usage

```razor
@using Lumeo.Icons

<SvgGlyph Svg="@(Tabler.Home)" />
```

Browse the full glyph set: https://lumeo.nativ.sh/icons
