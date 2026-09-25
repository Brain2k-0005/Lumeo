# Lumeo.Icons.MaterialSymbols.Sharp

Material Symbols (Sharp) icon pack for Lumeo — the weight-400 standard cut: 3,892 sharp icons (flat `MaterialSymbolsSharp` class) plus 3,892 filled icons (`MaterialSymbolsSharpFilled` class) exposed as tree-shakeable Lumeo.IconSource properties (namespace Lumeo.Icons, native 0 -960 960 960 viewBox, fill-rendered). Use with `<SvgGlyph Svg="@(MaterialSymbolsSharp.Home)" />` or `<Icon Svg="@(MaterialSymbolsSharpFilled.Favorite)" />`. THIRD-PARTY-NOTICES: bundles the Material Symbols set under the Apache License 2.0 — see THIRD-PARTY-NOTICES.txt in the package.

## Install

```bash
dotnet add package Lumeo
dotnet add package Lumeo.Icons.MaterialSymbols.Sharp
```

No DI registration needed — icon packs are pure static data, referenced directly from
markup.

## Usage

```razor
@using Lumeo.Icons

<SvgGlyph Svg="@(MaterialSymbolsSharp.Home)" />
```

Browse the full glyph set: https://lumeo.nativ.sh/icons
