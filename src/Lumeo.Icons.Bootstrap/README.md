# Lumeo.Icons.Bootstrap

Bootstrap Icons pack for Lumeo — 2,078 fill-based 16x16 icons exposed as tree-shakeable Lumeo.IconSource properties on the flat `Bootstrap` class (namespace Lumeo.Icons). The `-fill` variants keep their suffix (Bootstrap's own naming, e.g. `BellFill`). Use with `<SvgGlyph Svg="@(Bootstrap.Bell)" />` or `<Icon Svg="@(Bootstrap.HeartFill)" />`. THIRD-PARTY-NOTICES: bundles the Bootstrap Icons set under the MIT license — see THIRD-PARTY-NOTICES.txt in the package.

## Install

```bash
dotnet add package Lumeo --prerelease
dotnet add package Lumeo.Icons.Bootstrap --prerelease
```

No DI registration needed — icon packs are pure static data, referenced directly from
markup.

## Usage

```razor
@using Lumeo.Icons

<SvgGlyph Svg="@(Bootstrap.Bell)" />
```

Browse the full glyph set: https://lumeo.nativ.sh/icons
