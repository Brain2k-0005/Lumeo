#!/bin/bash
set -euo pipefail

# Lumeo session bootstrap for Claude Code on the web.
# Local machines are expected to have the .NET SDK + node already.
if [ "${CLAUDE_CODE_REMOTE:-}" != "true" ]; then
  exit 0
fi

# --- .NET 10 SDK ------------------------------------------------------------
# The remote container ships node but no dotnet. Install the pinned channel
# the repo targets (TargetFramework net10.0) into ~/.dotnet — idempotent:
# dotnet-install skips work when the requested SDK is already present.
if ! command -v dotnet >/dev/null 2>&1 && [ ! -x "$HOME/.dotnet/dotnet" ]; then
  curl -fsSL https://dot.net/v1/dotnet-install.sh -o /tmp/dotnet-install.sh
  bash /tmp/dotnet-install.sh --channel 10.0 --install-dir "$HOME/.dotnet"
  rm -f /tmp/dotnet-install.sh
fi

if [ -x "$HOME/.dotnet/dotnet" ]; then
  {
    echo 'export DOTNET_ROOT="$HOME/.dotnet"'
    echo 'export PATH="$HOME/.dotnet:$PATH"'
    # Faster, quieter dotnet in ephemeral containers.
    echo 'export DOTNET_CLI_TELEMETRY_OPTOUT=1'
    echo 'export DOTNET_NOLOGO=1'
  } >> "$CLAUDE_ENV_FILE"
fi

# --- Tailwind CLI (npm) -----------------------------------------------------
# Needed for `npm run build:css` whenever component classes change — the
# pre-compiled wwwroot/css/lumeo-utilities.css ships in the NuGet package.
cd "$CLAUDE_PROJECT_DIR"
npm install --no-audit --no-fund
