# @lumeo/mcp-server

A [Model Context Protocol](https://modelcontextprotocol.io) server that
exposes the [Lumeo](https://github.com/Brain2k-0005/Lumeo) Blazor component
library to MCP-compatible LLM clients (Claude Desktop, Cursor, GitHub Copilot,
Zed, etc.).

Once installed, your LLM can look up real Lumeo parameters, slots, and usage
examples — so "build me a sign-in page with Lumeo" produces markup that
actually compiles.

## What it exposes

The server covers **all 125 components** from Lumeo's generated
`registry.json`. The top ~35 most-used components ship with rich, hand-curated
schemas (parameters, slots, ready-to-use Razor examples, CSS variables); the
remaining ~90 are still discoverable and returned with category / description /
files / dependencies / CSS variables plus a link back to the docs site for full
reference. As more components get curated, the rich count grows automatically.

### Tools

- `lumeo_list_components({ category?, query? })` — list all 125 components
- `lumeo_get_component({ name })` — rich schema if curated; thin + docs link otherwise
- `lumeo_search({ query })` — fuzzy search across all 125

### Resources

- `lumeo://component/{Name}` — markdown reference per component
- `lumeo://category/{Name}` — overview of all components in a category

## Install

```bash
cd tools/lumeo-mcp
npm install
npm run build
```

This produces `dist/index.js`, a Node ESM entrypoint.

> Future: `npx -y @lumeo/mcp-server` once published to npm.

## Configure your MCP client

### Claude Desktop

Edit `claude_desktop_config.json` (Settings → Developer → Edit Config):

```json
{
  "mcpServers": {
    "lumeo": {
      "command": "node",
      "args": ["C:/Users/bemi/RiderProjects/Lumeo/tools/lumeo-mcp/dist/index.js"]
    }
  }
}
```

Restart Claude Desktop. You should see the Lumeo tools/resources under the
connectors panel.

### Cursor

Add to `.cursor/mcp.json`:

```json
{
  "mcpServers": {
    "lumeo": {
      "command": "node",
      "args": ["/absolute/path/to/lumeo-mcp/dist/index.js"]
    }
  }
}
```

### VS Code (GitHub Copilot)

Add to `.vscode/mcp.json`:

```json
{
  "servers": {
    "lumeo": {
      "type": "stdio",
      "command": "node",
      "args": ["/absolute/path/to/lumeo-mcp/dist/index.js"]
    }
  }
}
```

## Try it

After installing, ask your LLM:

> Build me a dashboard with three KpiCards using Lumeo.

or

> Show me how to build a sign-in page with Lumeo — email, password, and a submit button with validation.

The model will call `lumeo_search` + `lumeo_get_component` under the hood and
produce markup that uses the correct parameter names, two-way bindings, and
slot conventions.

## Development

```bash
npm run dev   # tsc --watch
npm start     # run the built server
```

The component catalog is built at startup by merging two sources:

- `src/components.ts` — hand-curated rich entries (top ~35) with full
  `params`, `slots`, and `example` fields
- `src/registry.json` — the full 125-component registry, copied from
  `src/Lumeo/registry/registry.json` at `prebuild` time by
  `scripts/sync-registry.mjs`

To enrich a thin entry, add a full entry for it in `src/components.ts` — the
merge layer will automatically upgrade it to the rich schema.

## License

MIT
