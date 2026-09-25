# Lumeo.Icons.Remix

RemixIcon pack for Lumeo — 1,539 line icons (flat `Remix` class) plus 1,539 filled icons (`RemixFilled` class), all fill-rendered 24px paths exposed as tree-shakeable Lumeo.IconSource properties (namespace Lumeo.Icons). Use with `<SvgGlyph Svg="@(Remix.Home)" />` or `<Icon Svg="@(RemixFilled.Heart)" />`. THIRD-PARTY-NOTICES: bundles the RemixIcon set under the Apache License 2.0 — see THIRD-PARTY-NOTICES.txt in the package.

## Install

```bash
dotnet add package Lumeo
dotnet add package Lumeo.Icons.Remix
```

No DI registration needed — icon packs are pure static data, referenced directly from
markup.

## Usage

```razor
@using Lumeo.Icons

<SvgGlyph Svg="@(Remix.Home)" />
```

Browse the full glyph set: https://lumeo.nativ.sh/icons
