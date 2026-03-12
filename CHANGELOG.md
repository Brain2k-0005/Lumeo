# Changelog

All notable changes to Lumeo will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [1.0.0-beta.1] - 2026-03-12

### Added
- 90+ Blazor components built on Tailwind CSS v4
- Layout primitives: Stack, Flex, Grid, Container, Center, Spacer
- Typography primitives: Text, Heading, Link, Code
- 30 chart types via ECharts integration (Bar, Line, Area, Pie, Donut, Radar, Scatter, Heatmap, TreeMap, Sankey, Funnel, Gauge, WordCloud, GeoMap, and more)
- DataGrid with sorting, filtering, column resize, inline editing, row selection, and CSV/JSON export
- Programmatic OverlayService for opening Dialog, Sheet, Drawer, AlertDialog from C# code with awaitable results
- ToastService with success, error, warning, info variants and promise support
- ThemeService for runtime theme and dark mode switching
- KeyboardShortcutService for global keyboard shortcuts
- 7 color themes: Zinc (default), Blue, Green, Rose, Orange, Violet, Amber, Teal
- Dark mode via CSS variable swaps
- Comprehensive documentation site with live demos and API reference
- 45+ pattern examples showing real-world component compositions
- GitHub Pages deployment at lumeo.nativ.sh

### Fixed
- Chart color resolution for modern CSS color formats (oklch, hsl, color())
- WordCloud extension race condition causing render failures
- Bar chart rendering broken by NaN borderRadius from CSS variable parsing
- Chart label text stroke artifacts on Sankey, Graph, Area, and Funnel charts
