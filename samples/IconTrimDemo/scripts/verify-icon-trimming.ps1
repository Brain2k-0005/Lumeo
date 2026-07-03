#!/usr/bin/env pwsh
#
# verify-icon-trimming.ps1 - local regression guard for the WASM "pay-for-what-you-use"
# claim. Re-publishes samples/IconTrimDemo (which references exactly 3 concrete Tabler.*
# icons) and FAILS (exit 1) if the trimmed Lumeo.Icons.Tabler artifact in the published
# _framework exceeds the size ceiling. If the trimmer ever stops engaging (e.g. IsTrimmable
# dropped from the pack csproj, or a reflective icon lookup sneaks into a call site), the
# artifact balloons back toward its ~3.4 MB untrimmed size and this check goes red.
#
# dotnet is not assumed on PATH: falls back to $HOME/.dotnet/dotnet.exe with a Major
# roll-forward, matching this repo's toolchain. ASCII-only on purpose so Windows
# PowerShell 5.1 parses it correctly.

$ErrorActionPreference = 'Stop'

# Ceiling: 200 KB (KiB). Trimmed reality is ~7 KB, so this is a wide guard-rail that only
# trips on a genuine trimming regression, not on normal icon-content drift.
$MaxBytes = 200 * 1024

$SampleDir = Split-Path -Parent $PSScriptRoot          # samples/IconTrimDemo
$FrameworkDir = Join-Path $SampleDir 'bin/Release/net10.0/publish/wwwroot/_framework'

# Resolve dotnet: prefer the repo's off-PATH SDK (~/.dotnet) because it carries the exact
# SDK Lumeo's global.json pins (10.0.301); the machine-wide dotnet on PATH may not. Fall
# back to PATH only if the private SDK is absent.
$dotnet = Join-Path $HOME '.dotnet/dotnet.exe'
if (-not (Test-Path $dotnet)) {
    $onPath = Get-Command 'dotnet' -ErrorAction SilentlyContinue
    if ($null -eq $onPath) {
        Write-Error "dotnet not found at $dotnet or on PATH"
        exit 1
    }
    $dotnet = $onPath.Source
}
$env:DOTNET_ROLL_FORWARD = 'Major'

# Run from inside the Lumeo tree so SDK/global.json discovery (which walks up from the
# current directory) resolves Lumeo's global.json, not whatever pins the shell's cwd.
Write-Host "Publishing $SampleDir (Release) using $dotnet ..."
Push-Location $SampleDir
try {
    & $dotnet publish $SampleDir -c Release
    $publishExit = $LASTEXITCODE
}
finally {
    Pop-Location
}
if ($publishExit -ne 0) {
    Write-Error "publish failed (exit $publishExit)"
    exit 1
}

# Blazor fingerprints + renames assemblies to <name>.<hash>.wasm; match the raw .wasm only
# (never the .br/.gz compressed siblings).
$artifact = Get-ChildItem -Path $FrameworkDir -Filter 'Lumeo.Icons.Tabler*.wasm' |
    Where-Object { $_.Name -notmatch '\.(br|gz)$' } |
    Select-Object -First 1

if ($null -eq $artifact) {
    Write-Error "Trimmed Lumeo.Icons.Tabler artifact not found in $FrameworkDir"
    exit 1
}

$size = $artifact.Length
$kb = [math]::Round($size / 1024, 1)
Write-Host "Trimmed Tabler artifact: $($artifact.Name) = $size bytes ($kb KB)"

if ($size -gt $MaxBytes) {
    Write-Error "TRIM REGRESSION: $size bytes exceeds ceiling of $MaxBytes bytes (200 KB). Trimming did not engage - check IsTrimmable in src/Lumeo.Icons.Tabler and that call sites use concrete Tabler.* references, not reflection."
    exit 1
}

Write-Host "OK - trimmed Tabler artifact is under the 200 KB ceiling."
exit 0
