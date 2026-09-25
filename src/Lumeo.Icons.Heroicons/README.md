# Lumeo.Icons.Heroicons

Heroicons pack for Lumeo — all four variants as tree-shakeable Lumeo.IconSource properties (namespace Lumeo.Icons): 324 outline icons (`Heroicons`, stroke 1.5), 324 solid (`HeroiconsSolid`, fill), 324 mini 20x20 (`HeroiconsMini`, fill) and 316 micro 16x16 (`HeroiconsMicro`, fill). Use with `<SvgGlyph Svg="@(Heroicons.Home)" />` or `<Icon Svg="@(HeroiconsSolid.Heart)" />`. THIRD-PARTY-NOTICES: bundles the Heroicons set under the MIT license — see THIRD-PARTY-NOTICES.txt in the package.

## Install

```bash
dotnet add package Lumeo
dotnet add package Lumeo.Icons.Heroicons
```

No DI registration needed — icon packs are pure static data, referenced directly from
markup.

## Usage

```razor
@using Lumeo.Icons

<SvgGlyph Svg="@(Heroicons.Home)" />
```

Browse the full glyph set: https://lumeo.nativ.sh/icons
