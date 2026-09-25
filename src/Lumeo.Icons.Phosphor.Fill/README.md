# Lumeo.Icons.Phosphor.Fill

Phosphor icon pack (Fill weight) for Lumeo — 1,248 fill-style icons exposed as tree-shakeable Lumeo.IconSource properties on the flat `PhosphorFill` class (namespace Lumeo.Icons). Use with `<SvgGlyph Svg="@(PhosphorFill.House)" />` or `<Icon Svg="@(PhosphorFill.House)" />`. THIRD-PARTY-NOTICES: bundles the Phosphor Icons set under the MIT license — see THIRD-PARTY-NOTICES.txt in the package.

## Install

```bash
dotnet add package Lumeo
dotnet add package Lumeo.Icons.Phosphor.Fill
```

No DI registration needed — icon packs are pure static data, referenced directly from
markup.

## Usage

```razor
@using Lumeo.Icons

<SvgGlyph Svg="@(PhosphorFill.House)" />
```

Browse the full glyph set: https://lumeo.nativ.sh/icons
