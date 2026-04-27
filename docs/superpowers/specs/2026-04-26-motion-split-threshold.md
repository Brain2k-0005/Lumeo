# Decision — When (Not) To Split Motion Into a Satellite Package

**Status:** Decided 2026-04-26
**Context:** During the Lumeo 2.0.0-rc.15 package split (`Lumeo`, `Lumeo.Charts`, `Lumeo.DataGrid`, `Lumeo.Editor`, `Lumeo.Scheduler`, `Lumeo.Gantt`), the question came up whether the 7 Motion components (`Marquee`, `NumberTicker`, `BlurFade`, `BorderBeam`, `ShimmerButton`, `Sparkles`, `TextReveal`) should also be a satellite package.

## Decision

**Motion stays in core `Lumeo` for now.** Don't preemptively split.

## Rationale

The split criterion across the existing 6 packages is *"does this component family carry a heavy bundled dependency that consumers without it shouldn't pay for?"* — not *"does this category make sense as a standalone surface?"*

| Satellite | What's heavy about it |
|---|---|
| `Lumeo.Charts` | ECharts (hundreds of KB) |
| `Lumeo.DataGrid` | ClosedXML + QuestPDF for Excel/PDF export (≈400 KB combined) |
| `Lumeo.Editor` | Mammoth (≈80 KB) + TipTap loaded from CDN |
| `Lumeo.Scheduler` | FullCalendar from CDN |
| `Lumeo.Gantt` | Custom SVG only — kept as satellite for parity, not size |

Motion today is ≈50 KB of pure CSS animations + minimal JS in the shared `components.js`. There's nothing heavy to opt out of.

Splitting now would mean:

- An 8th NuGet package consumers have to think about
- Extra friction for the common case (most apps want at least one motion primitive — ShimmerButton on a CTA, BlurFade on hero text)
- ~50 KB of "savings" that doesn't actually move the needle on first-paint

## When to Revisit

Split into `Lumeo.Motion` as soon as ANY of these hits:

| Trigger | Why |
|---|---|
| Total motion code exceeds **150 KB** | At that point, motion alone is ≈25% of the current core; opt-out becomes meaningful |
| Motion pulls a **heavy JS dep** (GSAP, Motion One, Lottie, Three.js, etc.) | Heavy deps are the actual reason satellites exist |
| **≥10 motion components** | Coherent enough surface area to be a standalone offering |
| You ship a **Lottie / 3D / particles** surface | Each ~100+ KB on its own — automatically qualifies |

## How To Execute When The Time Comes

The split is mechanical because the architecture supports it:

1. Create `src/Lumeo.Motion/` project, mirror the structure of `Lumeo.Gantt` (smallest existing satellite).
2. `git mv src/Lumeo/UI/{Marquee,NumberTicker,BlurFade,BorderBeam,ShimmerButton,Sparkles,TextReveal}/` → `src/Lumeo.Motion/UI/`.
3. Add to `Lumeo.slnx`.
4. Add to `tools/Lumeo.RegistryGen/Program.cs` `componentToPackage` map.
5. Re-run RegistryGen — registry entries get `nugetPackage: "Lumeo.Motion"`.
6. CLI auto-prompts on `lumeo add marquee` etc. No code changes needed.
7. Add to `.github/workflows/publish.yml` matrix.

Total effort once it's time: ~2 hours.

## Non-Goals

- Don't preemptively split because "it might grow." Splitting is reversible only by deprecation; reversing wastes consumer trust.
- Don't split for naming aesthetics. `Lumeo.Charts` makes sense because Charts are heavy. `Lumeo.Motion` would be aesthetic-only today.
