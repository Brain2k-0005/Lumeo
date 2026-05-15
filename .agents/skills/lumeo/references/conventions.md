# Lumeo coding conventions — full checklist

Use this when writing or reviewing Lumeo Razor. Items marked **(enforced)** will fail review or render incorrectly if violated.

## Every `.razor` component (when authoring new Lumeo components)

- `@namespace Lumeo` as the first line.
- `[Parameter] public string? Class { get; set; }` for custom CSS classes.
- `[Parameter(CaptureUnmatchedValues = true)] public Dictionary<string, object>? AdditionalAttributes { get; set; }`.
- `@attributes="AdditionalAttributes"` on the root element.
- Combine classes: `$"{BaseClasses} {Class}".Trim()` — never hardcode colours into `BaseClasses`.

## Colours & theming **(enforced)**

- **Never** raw hex/hsl/rgb in markup or component CSS. Use theme tokens as Tailwind utilities:
  - Surfaces: `bg-background`, `bg-card`, `bg-popover`, `bg-muted`, `bg-accent`
  - Text: `text-foreground`, `text-muted-foreground`, `text-card-foreground`, `text-primary-foreground`, …
  - Brand/status: `bg-primary` / `text-primary`, `bg-destructive` / `text-destructive-text`, `bg-positive`, `bg-info`, `bg-rating`
  - Borders & rings: `border-border`, `border-border/40` (the user prefers subtle `/40`–`/60` borders, never chunky), `ring-ring`
  - Radii: `rounded-[var(--radius)]`, `rounded-[var(--radius-sm)]`, `rounded-[var(--radius-lg)]`, `rounded-[var(--radius-xl)]`
  - Full list: `lumeo_get_theme_tokens` MCP tool.
- **No `dark:` prefixes.** Dark mode swaps the CSS-variable *values* on the `dark` class (on `<html>`); the tokens above automatically resolve correctly. `dark:bg-zinc-900` is a bug.
- Theme colour packs live in `src/Lumeo/wwwroot/css/themes/` (blue, green, rose, zinc, violet, amber, teal) — they only redefine the variables.

## Icons **(enforced)**

- `<Blazicon Svg="Lucide.X" />` from `Blazicons.Lucide`. Browse names at blazicons.com. Not inline `<svg>`, not Heroicons/FontAwesome/etc.

## State & binding

- Mutable shared state: `CascadingValue` with a context `record`, `IsFixed="false"`.
- Context records are nested `public record` inside the parent component.
- Two-way binding: a `Property` + `PropertyChanged` `EventCallback<T>` pair → consumers use `@bind-Property`.
- Enums: define inside the component's `@code` block (e.g. `Button.ButtonVariant`, `Tabs.TabsRenderMode`). Reference them fully-qualified in markup: `Variant="Button.ButtonVariant.Outline"`.

## JS interop **(enforced)**

- Go through `ComponentInteropService` — never inject `IJSRuntime` into a component directly.
- Common helpers: `RegisterClickOutside`, `LockScroll`/`UnlockScroll`, `SetupFocusTrap`/`RemoveFocusTrap`, `FocusElement`, `PositionFixed`.

## Overlay / portal components **(enforced)**

Dialog, Sheet, Drawer, Toast, Popover, Tooltip, AlertDialog, HoverCard, ContextMenu, DropdownMenu, Command, PopConfirm, Tour, DatePicker, DateTimePicker, TimePicker, Combobox, Cascader, Mention, Select — these render content into a portal outside the normal tree.

- The page `<body>` (or an ancestor element of the portal/overlay root) must carry `bg-background text-foreground`, or portal content renders outside the theme cascade and looks unstyled. (This is a common "why is my Dialog white-on-white" gotcha.)
- For the **service-driven** API (`ToastService.Success(...)`, `OverlayService.Open<T>(...)`) add a single `<OverlayProvider />` once in the app layout.
- When authoring an overlay component: use `RegisterClickOutside` for dismiss-on-outside, `LockScroll`/`UnlockScroll` for modals, `SetupFocusTrap`/`RemoveFocusTrap` for focus management, implement `IAsyncDisposable`, and handle `JSDisconnectedException` in cleanup.

## Composition

- Sub-components read their parent's `CascadingValue` and must be nested inside it: `<TabsContent>` in `<Tabs>`, `<DialogContent>`/`<DialogHeader>`/`<DialogTrigger>` in `<Dialog>`, `<SelectItem>` in `<Select>`, `<AccordionItem>` in `<Accordion>`, `<DataGridColumnDef>` in `<DataGrid>`, etc. `lumeo_validate_markup` checks this.
- Match a value between trigger and content: `<TabsTrigger Value="x">` ↔ `<TabsContent Value="x">`.

## Forms

- Lumeo inputs work with Blazor's `<EditForm>` / `EditContext` and DataAnnotations. `Form` component wraps common patterns. Validation messages via the standard Blazor pipeline.

## Tabs render modes (rc.29+)

- `<Tabs RenderMode="...">`: `Active` (default — only active panel in DOM, unmount on switch), `Lazy` (render on first activation, then keep mounted/hidden — state survives), `Eager` (render all up-front, inactive hidden). Per-panel override: `<TabsContent ForceMount="true">`. Same idea applies conceptually wherever state-loss-on-hide matters.

## Don't

- Don't `mkdir`/touch files outside the established `src/Lumeo/UI/{Component}/` layout for new components.
- Don't introduce new colours, fonts, or spacing scales outside the theme system.
- Don't add `Co-Authored-By` lines to commits in this repo.
