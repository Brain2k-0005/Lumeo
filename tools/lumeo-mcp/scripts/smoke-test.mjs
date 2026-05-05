#!/usr/bin/env node
// Spawns the built MCP server, sends an initialize + tools/call request via
// stdio, and prints the JSON response. Used to verify the auto-generated
// components-api.json is wired up end-to-end.
import { spawn } from "node:child_process";
import { dirname, resolve } from "node:path";
import { fileURLToPath } from "node:url";

const here = dirname(fileURLToPath(import.meta.url));
const serverPath = resolve(here, "../dist/index.js");
const target = process.argv[2] ?? "DataGrid";

const child = spawn(process.execPath, [serverPath], {
  stdio: ["pipe", "pipe", "inherit"],
});

let buf = "";
child.stdout.on("data", (chunk) => {
  buf += chunk.toString();
  let idx;
  while ((idx = buf.indexOf("\n")) >= 0) {
    const line = buf.slice(0, idx).trim();
    buf = buf.slice(idx + 1);
    if (!line) continue;
    try {
      const msg = JSON.parse(line);
      if (msg.id === 2) {
        const text = msg.result?.content?.[0]?.text ?? "(no text)";
        const parsed = JSON.parse(text);
        console.log(JSON.stringify(parsed, null, 2));
        child.kill();
        process.exit(0);
      }
    } catch (e) {
      console.error("[smoke] non-JSON line:", line);
    }
  }
});

const send = (obj) => child.stdin.write(JSON.stringify(obj) + "\n");

send({ jsonrpc: "2.0", id: 1, method: "initialize", params: { protocolVersion: "2024-11-05", capabilities: {}, clientInfo: { name: "smoke", version: "1.0.0" } } });
send({ jsonrpc: "2.0", id: 2, method: "tools/call", params: { name: "lumeo_get_component", arguments: { name: target } } });

setTimeout(() => { console.error("[smoke] timeout"); child.kill(); process.exit(1); }, 10000);
