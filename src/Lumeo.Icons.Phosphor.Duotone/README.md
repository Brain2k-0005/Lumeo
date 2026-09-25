# Lumeo.Icons.Phosphor.Duotone

Phosphor icon pack (Duotone weight) for Lumeo — 1,248 two-tone fill icons exposed as tree-shakeable Lumeo.IconSource properties on the flat `PhosphorDuotone` class (namespace Lumeo.Icons). The two-tone shading lives in each icon's inner markup (per-path opacity), so no special renderer support is needed. Use with `<SvgGlyph Svg="@(PhosphorDuotone.House)" />` or `<Icon Svg="@(PhosphorDuotone.House)" />`. THIRD-PARTY-NOTICES: bundles the Phosphor Icons set under the MIT license — see THIRD-PARTY-NOTICES.txt in the package.

## Install

```bash
dotnet add package Lumeo
dotnet add package Lumeo.Icons.Phosphor.Duotone
```

No DI registration needed — icon packs are pure static data, referenced directly from
markup.

## Usage

```razor
@using Lumeo.Icons

<SvgGlyph Svg="@(PhosphorDuotone.House)" />
```

Browse the full glyph set: https://lumeo.nativ.sh/icons
