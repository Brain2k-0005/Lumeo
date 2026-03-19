# Changelog

All notable changes to Lumeo will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [1.0.0-beta.5] - 2026-03-19

### Improved
- Updated NuGet package description (90+ → 103 components, added test count and feature list)
- Updated README with accurate component count, themes, and install command

## [1.0.0-beta.4] - 2026-03-19

### Added
- Checkbox: Label, Description parameters with auto-Id for form association
- RadioGroupItem: Description text support
- Steps: Error state per step with red X icon, custom Icon slot
- Popover: Arrow support (ShowArrow parameter matching Tooltip pattern)
- 48 new unit tests covering all upgraded component features (1,124 total)
- Form Validation guide with DataAnnotations, custom validation, and complete examples
- Contributing guide with setup, component creation, testing, and code style docs
- "When to Use" and "Related Components" sections on 62 more component pages (82 total)
- API reference tables now on all 136 component documentation pages

### Improved
- Home page stats updated (75→103 components, 7→8 themes)
- Chart patterns integrated into Patterns page with filter category
- All hardcoded colors replaced with CSS variables (Avatar, Statistic, Result, KanbanCard)

### Fixed
- MentionPage Razor escape for @user syntax
- Statistic and Result test assertions updated for CSS variable colors

## [1.0.0-beta.3] - 2026-03-19

### Added
- Dialog size variants (Sm, Default, Lg, Xl, Full) and Scrollable content mode
- Drawer multi-position support (Top, Right, Bottom, Left) with direction-aware swipe
- Alert: Title, Description, Icon slots, ShowIcon with default icons per variant, AutoDismiss timer
- Input: 3 size variants (Sm, Default, Lg) and Clearable mode with X button
- Tooltip arrow support with configurable Offset and fade animation
- Badge: Pulse ping animation on dot variant and Icon slot
- Accordion: DefaultValues for initially open items and Disabled items
- Skeleton: Wave/Shimmer animation variant
- Spinner: Dots and Bars variants, Label text, and Color override
- HoverCard: Side parameter (Top/Bottom positioning)
- Tabs: Disabled tabs with aria-disabled and TrailingContent on TabsList
- Button: FullWidth mode, LeftIcon and RightIcon slots
- Progress: Circular SVG variant with ShowValue and configurable StrokeWidth
- Avatar: Square shape option and Status indicator (Online, Offline, Away, Busy)
- Switch: Loading spinner state and OnLabel/OffLabel text
- Select: Disabled items and Placeholder text on trigger
- Combobox: EmptyText for no-results state and Creatable mode
- NumberInput: Arrow key and mouse wheel support, Prefix/Suffix text
- Textarea: Character count display, MaxLength indicator, Resize control
- Accessibility guide page with ARIA roles, keyboard patterns, and focus management docs
- Changelog page in docs site with full release history
- API reference tables for 30 additional component documentation pages (55 total)
- "When to Use", "Keyboard Interactions", and "Related Components" sections on 20 component pages

### Improved
- Animation keyframes and utility classes now ship in lumeo.css for NuGet package consumers
- Production-quality spring easing curves on all animations
- Rating colors now use themeable `--color-rating` CSS variable instead of hardcoded yellow

### Fixed
- Broken animations for NuGet package consumers (keyframes were only in docs site)
- Missing `animate-toast-in` — Toast slide-in animation was never defined
- Added aria-labels to PasswordInput toggle, TagInput close buttons, DatePicker clear button, Carousel navigation
- Rating keyboard navigation with Arrow keys and improved star labels

## [1.0.0-beta.2] - 2026-03-12

### Added
- 14 new components: Cascader, ColorPicker, DateRangePicker, DateTimePicker, Filter, ImageCompare, InplaceEditor, InputMask, Kanban, MegaMenu, Mention, NumberInput, PasswordInput, SortableList
- Keyboard shortcuts: R to shuffle themes, Ctrl+D for dark mode, Ctrl+/ for shortcuts help
- Redesigned WASM loader with animated splash screen and ripple animation
- Floating navbar and floating sidebar design for docs site
- NuGet package icon (Lumeo logo)

### Improved
- All UI corners respect CSS radius variable for zero-radius presets like Lyra
- Customizer sidebar moved to header button with Ctrl+B toggle shortcut
- CommandEmpty now always renders regardless of Command context

### Fixed
- Customizer radius bug where radius values did not apply correctly
- Mobile docs improvements and API table horizontal scrolling
- Floating nav sticky positioning
- Splash screen CSS compatibility with Tailwind CDN
- Em dash encoding issues in page titles

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
