# @lumeo/mcp-server

A [Model Context Protocol](https://modelcontextprotocol.io) server that
exposes the [Lumeo](https://github.com/Brain2k-0005/Lumeo) Blazor component
library to MCP-compatible LLM clients (Claude Desktop, Cursor, GitHub Copilot,
Zed, etc.).

Once installed, your LLM can look up real Lumeo parameters, slots, and usage
examples — so "build me a sign-in page with Lumeo" produces markup that
actually compiles.

## What it exposes

### Tools

- `lumeo_list_components({ category?, query? })` — list the catalog
- `lumeo_get_component({ name })` — full reference (params, slots, example, CSS variables)
- `lumeo_search({ query })` — fuzzy search over names, categories, descriptions

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

The server is ~300 lines total. The component catalog lives in
`src/components.ts` and is hand-curated — add entries there as Lumeo grows.
If `src/Lumeo/registry/registry.json` exists, it is also loaded as a
supplementary index so `lumeo_search` can surface components that don't yet
have curated docs.

## License

MIT
