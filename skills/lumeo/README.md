# Lumeo Agent Skill

A [Claude Agent Skill](https://docs.claude.com/en/docs/agents-and-tools/agent-skills) that teaches an AI coding agent (Claude Code, Cursor, Copilot CLI, …) how to write correct Lumeo Razor: how to look components up via the `@lumeo-ui/mcp-server` MCP, and the non-negotiable conventions (theme tokens, no `dark:` prefixes, `Blazicon` icons, `ComponentInteropService`, sub-component nesting, portal `<body>` classes, …).

It pairs with the MCP server but degrades gracefully — [`references/catalog.md`](references/catalog.md) is a full offline component list for when the MCP isn't connected.

## Contents

```
skills/lumeo/
  SKILL.md                  # the skill — frontmatter + when-to-use + conventions + workflow
  references/
    conventions.md          # full coding-conventions checklist
    mcp.md                  # lumeo-mcp tool reference + example calls
    catalog.md              # all 131 components by category + 16 patterns + 58 theme tokens (offline fallback)
  gen-catalog.mjs            # regenerates references/catalog.md from components-api.json
```

## Install

### Claude Code (or any agent that reads `~/.claude/skills/`)

```bash
# from a clone of the Lumeo repo:
mkdir -p ~/.claude/skills
cp -r skills/lumeo ~/.claude/skills/lumeo
```
Or per-project: copy into `<your-project>/.claude/skills/lumeo`.

Then also connect the MCP server (`.mcp.json` or your client's MCP config):
```jsonc
{ "mcpServers": { "lumeo": { "command": "npx", "args": ["-y", "@lumeo-ui/mcp-server"] } } }
```

### Skill registries (skills.sh, etc.)

The skill is a self-contained directory — publish `skills/lumeo/` to any agent-skill registry as-is. If you're installing from a registry, follow that registry's `install` command (it just drops the directory into your skills path); the MCP-server step above is still recommended for the live API.

## Keeping it fresh

`references/catalog.md` is generated from the MCP's `components-api.json`. After a Lumeo release, regenerate:
```bash
node skills/lumeo/gen-catalog.mjs
```
`SKILL.md`, `conventions.md` and `mcp.md` are hand-maintained — update them when conventions or MCP tools change.
