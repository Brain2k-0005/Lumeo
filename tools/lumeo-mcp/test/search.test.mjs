import { test } from "node:test";
import assert from "node:assert/strict";
import { searchComponents, tokenize } from "../dist/search.js";

// Small synthetic catalog. FlowCanvas's description mirrors the real one shipped in
// components-api.json (Lumeo.Flow, 5.11.0) — see tools/Lumeo.RegistryGen's category/
// description map for FlowCanvas.
const catalog = [
  {
    name: "FlowCanvas",
    category: "Data Display",
    description:
      "Node/flow editor canvas — templated nodes, SVG edges, pan, pointer-anchored wheel zoom, " +
      "node drag with snapping, background grid and zoom controls.",
    subComponents: {
      FlowHandle: { componentName: "FlowHandle" },
      FlowMiniMap: { componentName: "FlowMiniMap" },
    },
  },
  {
    name: "Gantt",
    category: "Data Display",
    description: "Gantt chart — tasks, dependencies, drag-to-reschedule, critical path.",
    subComponents: {},
  },
  {
    name: "Select",
    category: "Form",
    description: "A dropdown select with search, grouping and multi-select.",
    subComponents: {
      SelectItem: { componentName: "SelectItem" },
    },
  },
];

test("LU-15 repro: 'flow diagram nodes edges' finds FlowCanvas even though that exact " +
  "phrase never appears verbatim in its description", () => {
  const results = searchComponents(catalog, "flow diagram nodes edges");
  assert.ok(results.length > 0, "expected at least one result, got []");
  assert.equal(results[0].name, "FlowCanvas");
});

test("a query word that matches nothing does not zero out the other words' hits", () => {
  // "diagram" matches nothing in the synthetic catalog; "gantt" matches Gantt by name.
  const results = searchComponents(catalog, "diagram gantt");
  assert.ok(results.some((c) => c.name === "Gantt"));
});

test("single-word queries behave exactly as before (name/category/description/sub-component)", () => {
  assert.equal(searchComponents(catalog, "flowcanvas")[0].name, "FlowCanvas");
  assert.equal(searchComponents(catalog, "gantt")[0].name, "Gantt");
  // Sub-component name match surfaces the parent.
  assert.equal(searchComponents(catalog, "selectitem")[0].name, "Select");
});

test("category filter narrows the pool before scoring", () => {
  const results = searchComponents(catalog, "", "Form");
  assert.equal(results.length, 1);
  assert.equal(results[0].name, "Select");
});

test("empty query with no category returns the whole pool, unscored", () => {
  assert.equal(searchComponents(catalog, "").length, catalog.length);
});

test("a query with no matches at all returns []", () => {
  assert.deepEqual(searchComponents(catalog, "totally-unrelated-xyz"), []);
});

test("tokenize splits on whitespace, lowercases, and drops empty tokens", () => {
  assert.deepEqual(tokenize("  Flow   Diagram Nodes "), ["flow", "diagram", "nodes"]);
  assert.deepEqual(tokenize(""), []);
});

test("exact name match still outranks a partial/description-only match", () => {
  const results = searchComponents(catalog, "gantt");
  assert.equal(results[0].name, "Gantt");
});
