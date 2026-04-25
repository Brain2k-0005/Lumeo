# Command

**Path:** `src/Lumeo/UI/Command/`
**Class:** Overlay
**Files:** Command.razor, CommandEmpty.razor, CommandGroup.razor, CommandInput.razor, CommandItem.razor, CommandList.razor, CommandSeparator.razor

## Contract — WARN
- Command is in the overlay list but `Command.razor` itself does not implement `IAsyncDisposable` — it is a pure rendering container with no JS interop.
- `CommandItem.razor` implements `IDisposable` correctly (unregisters from context on dispose).
- No `ComponentInteropService` usage (Command is a client-side filter — no JS interop needed). Consistent with design.
- All other contract checks pass.

## API — WARN
- No `Open`/`OpenChanged` or `Disabled` on root Command (it is typically embedded in a Dialog/Popover — not a standalone overlay). Acceptable design choice but diverges from overlay API spec.

## Bugs — OK
- No findings.

## Docs — OK
- Page: `docs/Lumeo.Docs/Pages/Components/CommandPage.razor` (exists)
- 4 ComponentDemo blocks
- API Reference: present
- Indexed in ComponentsIndex.razor: yes

## CLI — OK
- Registry entry: present (key `command`)
- Files declared: 7 of 7
- Missing from registry: none
- Component deps declared: `kbd` declared; OK
