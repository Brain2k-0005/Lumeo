# Changelog

All notable changes to Lumeo will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [3.13.0] - 2026-06-11

Component-audit hardening release: a full-library audit benchmarked against shadcn/ui, Blueprint, MudBlazor and Ant Design, fixing keyboard/ARIA, lifecycle and culture defects across ~25 components, plus a consumer-reported DataGrid grouping regression.

> Changelog entries between `1.0.0-beta.5` and this release were tracked via git history and GitHub Releases rather than this file.

### Fixed
- **DataGrid**: `GroupBy`/`GroupByFields` silently rendered a flat grid with declarative `<DataGridColumnDef>` children (regression since 3.10.0) — the grouping seed validated against an empty column list before the children registered. It now re-seeds when the matching column arrives, and warns when a group field matches no column.
- **Select / Combobox**: data-bound keyboard navigation (Arrow/Home/End/Enter) was dead; search cleared registrations and showed a spurious empty state; the trigger click could not reliably close the popup.
- **Menus** (Menubar / DropdownMenu / ContextMenu / MegaMenu): Enter/Space double-fire made keyboard activation a no-op; click-outside couldn't dismiss via the trigger; ContextMenu key handlers were unreachable; Menubar gained full WAI-ARIA navigation.
- **DatePicker**: typed input bypassed `MinDate`/`MaxDate`/`IsDateDisabled`; range presets set only the start date. Calendar now follows external value changes, DateTimePicker keeps a pending time, and wheel pickers resync on external change.
- **TreeView**: selection two-way binding never fired, children were keyboard-unreachable, and check state corrupted under search.
- **Tabs**: arrow keys activated disabled tabs and navigated to removed (closable) tabs; Delete now closes a closable tab.
- **Stepper**: blank first render, duplicated steps on re-render, ghost steps after removal; `KeepMounted` now works.
- **RadioGroup**: arrow keys selected disabled radios; removed radios stayed keyboard targets.
- **Accordion / Collapsible**: collapsed content kept focusable children in the tab order; `div`-button triggers scrolled the page on Space.
- **Overlays** (Dialog / AlertDialog / Sheet / Drawer): focus is restored to the trigger on close; Escape no longer closes all nested overlays at once; AlertDialog focuses Cancel first.
- **FileUpload** drop now adds files; **Window** drag/resize uses pointer capture; **Resizable** seeds from panel default sizes and supports keyboard resize; **Command** has full keyboard navigation.
- **Barcode**: the `Format` parameter is honored — real Code 39 and EAN-13 encoders (previously rendered as Code 128 regardless); encoding errors are now visible instead of blank.
- **Culture / locale**: AspectRatio and Watermark emitted invalid CSS/SVG on comma-decimal cultures; Statistic mis-parsed localized decimals; Progress now clamps negative values; Grid/Container shipped the missing `grid-cols-9..12` / `max-w-*` utilities.
- Mention no longer throws on empty results or mangles inserted text; QRCode logo scales to the code; Tour scrolls off-screen targets into view; Carousel and Splitter keyboard traps removed; Input `Clearable` no longer drops focus while typing; Form can submit again after a fixed validation error.

### Added
- `IComponentInteropService.RegisterPreventDefaultKeys` — key-selective, IME-safe `preventDefault` applied synchronously in the native event dispatch (replaces the render-time `@onkeydown:preventDefault` flag pattern).
- Localization keys for ConfirmButton, PickList, FileManager, AudioPlayer, ThemeSwitcher, Stepper and Breadcrumb strings across all 14 locales.
- Wired previously-inert parameters: Tooltip `Offset`, HoverCard `Side` (Left/Right), PopConfirm `Placement`, SpeedDial `Icon`/`Variant`, Highlighter `Tag`.
- DevX: a `SessionStart` hook installs the .NET SDK + npm dependencies in Claude Code remote containers.

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
