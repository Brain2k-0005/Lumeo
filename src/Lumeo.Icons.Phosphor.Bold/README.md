# Lumeo.Icons.Phosphor.Bold

Phosphor icon pack (Bold weight) for Lumeo — 1,248 fill-style icons exposed as tree-shakeable Lumeo.IconSource properties on the flat `PhosphorBold` class (namespace Lumeo.Icons). Use with `<SvgGlyph Svg="@(PhosphorBold.House)" />` or `<Icon Svg="@(PhosphorBold.House)" />`. THIRD-PARTY-NOTICES: bundles the Phosphor Icons set under the MIT license — see THIRD-PARTY-NOTICES.txt in the package.

## Install

```bash
dotnet add package Lumeo
dotnet add package Lumeo.Icons.Phosphor.Bold
```

No DI registration needed — icon packs are pure static data, referenced directly from
markup.

## Usage

```razor
@using Lumeo.Icons

<SvgGlyph Svg="@(PhosphorBold.House)" />
```

Browse the full glyph set: https://lumeo.nativ.sh/icons
