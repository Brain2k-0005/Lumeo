# IconTrimDemo — the WASM trim proof

A minimal standalone Blazor WebAssembly app that references **exactly three** Tabler icons
(`Tabler.Home`, `Tabler.Search`, `Tabler.Settings`) as **concrete property references** and
renders them via `<SvgGlyph>`. Its only job is to **measure** — not assume — the core claim
of the Lumeo icon packs:

> You pay only for the icons you actually use.

Because every icon in `Lumeo.Icons.Tabler` is an expression-bodied `static` property (never a
`static readonly` field), the WASM ILLink trimmer keeps only the three properties this app
touches and drops the other ~6,140. No reflection / no name→icon lookup is used anywhere —
that would root the whole pack and defeat the proof.

## Measured result

| Artifact | Size |
|---|---|
| `Lumeo.Icons.Tabler.dll` — **untrimmed** (`bin/Release/net10.0`) | **3,543,552 bytes** (3.38 MiB) |
| `Lumeo.Icons.Tabler.<hash>.wasm` — **trimmed** (published `_framework`) | **7,445 bytes** (7.3 KB) |
| trimmed, Brotli (`.wasm.br`, wire size) | 2,398 bytes |
| trimmed, Gzip (`.wasm.gz`) | 2,771 bytes |

**~476× smaller** after trimming (99.79% dropped). Three icons cost ~7.3 KB on disk /
~2.4 KB on the wire — the pack is genuinely pay-for-what-you-use.

- Date measured: **2026-07-03**
- SDK: **.NET 10.0.301** (`dotnet --version`)
- Trimmer: default Blazor WASM `Release` publish (`PublishTrimmed` on by default for WASM);
  pack opts in with `<IsTrimmable>true</IsTrimmable>`.
- Published artifacts are fingerprinted, so the `<hash>` segment of the `.wasm` name varies
  between publishes; the size does not.

## Reproduce

```pwsh
# dotnet is not on PATH here; the repo SDK lives at ~/.dotnet
$env:DOTNET_ROLL_FORWARD = 'Major'
& "$HOME/.dotnet/dotnet.exe" publish samples/IconTrimDemo -c Release

# untrimmed contrast build
& "$HOME/.dotnet/dotnet.exe" build src/Lumeo.Icons.Tabler -c Release
```

The trimmed assembly lands at
`samples/IconTrimDemo/bin/Release/net10.0/publish/wwwroot/_framework/Lumeo.Icons.Tabler.<hash>.wasm`.

## Regression guard

`scripts/verify-icon-trimming.ps1` re-runs the publish and **exits 1** if the trimmed Tabler
artifact exceeds **200 KB** (a wide guard-rail vs. the ~7 KB reality — it only trips if
trimming stops engaging, e.g. `IsTrimmable` is dropped or a reflective icon lookup creeps
into a call site).

```pwsh
pwsh samples/IconTrimDemo/scripts/verify-icon-trimming.ps1
# or under Windows PowerShell:  powershell -File samples/IconTrimDemo/scripts/verify-icon-trimming.ps1
```

## Notes

This sample is **standalone** — it is intentionally NOT added to `Lumeo.slnx` (mirrors how
`samples/DataGridServerModeDemo` stays out of the solution). It uses local `ProjectReference`s
to `src/Lumeo` and `src/Lumeo.Icons.Tabler`, so it always exercises the working tree.
