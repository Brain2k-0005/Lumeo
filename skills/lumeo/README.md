# Lumeo Agent Skill

[![Agent Skill on skills.sh](https://img.shields.io/badge/skills.sh-lumeo-000?logo=vercel&logoColor=white)](https://skills.sh/Brain2k-0005/Lumeo/lumeo)

A portable [agent skill](https://docs.claude.com/en/docs/agents-and-tools/agent-skills) that teaches an AI coding agent (Claude Code, Cursor, Codex, Gemini CLI, OpenCode, Antigravity, Copilot CLI, …) how to write correct Lumeo Razor: how to look components up via the `@lumeo-ui/mcp-server` MCP, and the non-negotiable conventions (theme tokens, no `dark:` prefixes, `SvgGlyph` icons, `ComponentInteropService`, sub-component nesting, portal `<body>` classes, …).

It pairs with the MCP server but degrades gracefully — [`references/catalog.md`](references/catalog.md) is a full offline component list for when the MCP isn't connected.

## Contents

```
skills/lumeo/
  SKILL.md                  # the skill — frontmatter + when-to-use + conventions + workflow
  references/
    conventions.md          # full coding-conventions checklist
    mcp.md                  # lumeo-mcp tool reference + example calls
    catalog.md              # all 164 components by category + 16 patterns + 58 theme tokens (offline fallback)
  gen-catalog.mjs            # regenerates references/catalog.md from components-api.json
```

## Install

### Recommended — via the `skills` CLI (Vercel Labs)

Works with Claude Code, Cursor, Codex, Gemini CLI, OpenCode, Antigravity, and 50+ other AI agents:

```bash
npx skills add github.com/Brain2k-0005/Lumeo/skills/lumeo
```

Installs into `<your-project>/.agents/skills/lumeo/` with symlinks for every supported agent. Once installed the skill auto-activates whenever you mention Lumeo or one of its components. Discoverable at [skills.sh](https://skills.sh).

### Manual install (per-agent fallback)

If you don't want the `skills` CLI:

```bash
# Claude Code — global
mkdir -p ~/.claude/skills
cp -r skills/lumeo ~/.claude/skills/lumeo

# Or per-project
cp -r skills/lumeo <your-project>/.claude/skills/lumeo
```

### Connect the MCP server

The skill drives `@lumeo-ui/mcp-server` for live API lookups. Add to your `.mcp.json`:

```jsonc
{ "mcpServers": { "lumeo": { "command": "npx", "args": ["-y", "@lumeo-ui/mcp-server"] } } }
```

Without the MCP the skill falls back to [`references/catalog.md`](references/catalog.md) — usable but no per-parameter API.

## Keeping it fresh

`references/catalog.md` is auto-generated from the MCP's `components-api.json`. A GitHub Actions workflow ([`.github/workflows/sync-skill-catalog.yml`](../../.github/workflows/sync-skill-catalog.yml)) regenerates and commits it whenever the API JSON changes, so the offline reference stays in lockstep with the live MCP.

To regenerate manually:
```bash
node skills/lumeo/gen-catalog.mjs
```

`SKILL.md`, `conventions.md` and `mcp.md` are hand-maintained — update them when conventions or MCP tools change.
