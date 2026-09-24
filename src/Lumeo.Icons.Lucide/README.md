# Lumeo.Icons.Lucide

Lucide icon pack for Lumeo — 1,746 tree-shakeable Lucide outline icons exposed as first-party Lumeo.IconSource properties on the flat `Lucide` class (namespace Lumeo.Icons). Use with `<SvgGlyph Svg="@(Lucide.House)" />` or `<Icon Svg="@(Lucide.House)" />`. THIRD-PARTY-NOTICES: bundles the Lucide icon set under the ISC license — see THIRD-PARTY-NOTICES.txt in the package.

## Install

```bash
dotnet add package Lumeo --prerelease
dotnet add package Lumeo.Icons.Lucide --prerelease
```

No DI registration needed — icon packs are pure static data, referenced directly from
markup.

## Usage

```razor
@using Lumeo.Icons

<SvgGlyph Svg="@(Lucide.House)" />
```

Browse the full glyph set: https://lumeo.nativ.sh/icons
