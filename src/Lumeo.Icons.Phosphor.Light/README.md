# Lumeo.Icons.Phosphor.Light

Phosphor icon pack (Light weight) for Lumeo — 1,248 fill-style icons exposed as tree-shakeable Lumeo.IconSource properties on the flat `PhosphorLight` class (namespace Lumeo.Icons). Use with `<SvgGlyph Svg="@(PhosphorLight.House)" />` or `<Icon Svg="@(PhosphorLight.House)" />`. THIRD-PARTY-NOTICES: bundles the Phosphor Icons set under the MIT license — see THIRD-PARTY-NOTICES.txt in the package.

## Install

```bash
dotnet add package Lumeo --prerelease
dotnet add package Lumeo.Icons.Phosphor.Light --prerelease
```

No DI registration needed — icon packs are pure static data, referenced directly from
markup.

## Usage

```razor
@using Lumeo.Icons

<SvgGlyph Svg="@(PhosphorLight.House)" />
```

Browse the full glyph set: https://lumeo.nativ.sh/icons
