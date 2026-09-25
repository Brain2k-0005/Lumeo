# Lumeo.Icons.Fluent

Fluent UI System Icons pack for Lumeo — the 24px standard cut: 2,449 regular icons (flat `Fluent` class) plus 2,485 filled icons (`FluentFilled` class) exposed as tree-shakeable Lumeo.IconSource properties (namespace Lumeo.Icons, 0 0 24 24 viewBox, fill-rendered). Use with `<SvgGlyph Svg="@(Fluent.Home)" />` or `<Icon Svg="@(FluentFilled.Heart)" />`. THIRD-PARTY-NOTICES: bundles the Fluent UI System Icons set under the MIT license — see THIRD-PARTY-NOTICES.txt in the package.

## Install

```bash
dotnet add package Lumeo
dotnet add package Lumeo.Icons.Fluent
```

No DI registration needed — icon packs are pure static data, referenced directly from
markup.

## Usage

```razor
@using Lumeo.Icons

<SvgGlyph Svg="@(Fluent.Home)" />
```

Browse the full glyph set: https://lumeo.nativ.sh/icons
