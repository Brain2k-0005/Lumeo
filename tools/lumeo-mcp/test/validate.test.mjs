import { test } from "node:test";
import assert from "node:assert/strict";
import { createValidator } from "../dist/validate.js";

// Small synthetic catalog mirroring the real ApiComponent shape, so the validator
// is exercised independent of the (large, churning) live registry. Params now carry a
// `type` (so enums bind by type, not substring guessing) and sub-component params carry
// `isCascading` (so the parent-child rule only fires for true CascadingValue readers).
const catalog = [
  {
    name: "Select",
    parameters: [
      { name: "Value", type: "string?" },
      { name: "Disabled", type: "bool" },
      { name: "Side", type: "Side" },
    ],
    enums: [{ name: "Side", values: ["Top", "Bottom", "Left", "Right"] }],
    subComponents: {
      // Reads Select.SelectContext via a [CascadingParameter] → requires <Select>.
      SelectItem: {
        componentName: "SelectItem",
        parameters: [
          { name: "Value", type: "string" },
          { name: "Context", type: "Select.SelectContext", isCascading: true },
        ],
        enums: [],
      },
    },
  },
  {
    name: "Button",
    parameters: [
      { name: "Variant", type: "ButtonVariant" },
      { name: "Disabled", type: "bool" },
    ],
    enums: [{ name: "ButtonVariant", values: ["Default", "Outline", "Ghost"] }],
    subComponents: {},
  },
  {
    // Uses the shared/global Lumeo.Size enum (passed in via sharedEnums, not on the component).
    name: "Alert",
    parameters: [{ name: "Size", type: "Lumeo.Size" }],
    enums: [],
    subComponents: {},
  },
  {
    name: "Dialog",
    parameters: [{ name: "Open", type: "bool" }],
    enums: [],
    subComponents: {
      // Purely presentational — a styled div with no [CascadingParameter]. Must be
      // usable standalone (no "must be used inside <Dialog>" false positive).
      DialogHeader: { componentName: "DialogHeader", parameters: [{ name: "Class", type: "string?" }], enums: [] },
    },
  },
];

const sharedEnums = [{ name: "Size", values: ["Xs", "Sm", "Md", "Lg", "Xl"] }];
const validate = createValidator(catalog, sharedEnums);

test("valid markup passes with no issues", () => {
  const r = validate('<Select Value="x" Disabled="true" />');
  assert.equal(r.ok, true);
  assert.equal(r.issues.length, 0);
});

test("unknown PascalCase parameter is a warning (not a hard error)", () => {
  const r = validate('<Select Bogus="x" />');
  assert.equal(r.ok, true); // warnings don't fail validation
  assert.equal(r.issues.length, 1);
  assert.equal(r.issues[0].severity, "warning");
  assert.match(r.issues[0].message, /Unknown parameter/);
});

test("invalid enum value (qualified) is an error", () => {
  const r = validate('<Button Variant="ButtonVariant.Sideways" />');
  assert.equal(r.ok, false);
  assert.ok(r.issues.some((i) => i.severity === "error" && /not a valid ButtonVariant/.test(i.message)));
});

test("invalid enum value (bare) is an error — bound by the param's type", () => {
  const r = validate('<Select Side="Sideways" />');
  assert.equal(r.ok, false);
  assert.ok(r.issues.some((i) => /not a valid Side value/.test(i.message)));
});

test('shared/global enum (Lumeo.Size) is bound by type — Size="Large" is caught', () => {
  // Regression: the old substring heuristic never flagged this because the Size enum
  // values weren't reachable. Now Size binds via its `Lumeo.Size` type to the shared enum.
  const r = validate('<Alert Size="Large" />');
  assert.equal(r.ok, false);
  assert.ok(r.issues.some((i) => /not a valid Size value/.test(i.message) && /Xs, Sm, Md, Lg, Xl/.test(i.message)));
});

test("a value that merely shares a substring with another enum is not mis-flagged", () => {
  // "Outline" is a valid ButtonVariant; type binding means each param checks only its
  // own enum, so no cross-enum false positive.
  assert.equal(validate('<Button Variant="Outline" />').ok, true);
});

test("valid enum value passes — bare and qualified", () => {
  assert.equal(validate('<Button Variant="Outline" />').ok, true);
  assert.equal(validate('<Button Variant="ButtonVariant.Ghost" />').ok, true);
  assert.equal(validate('<Select Side="Bottom" />').ok, true);
  assert.equal(validate('<Alert Size="Md" />').ok, true);
});

test("sub-component that reads a CascadingValue requires its parent", () => {
  const r = validate('<SelectItem Value="x" />');
  assert.equal(r.ok, false);
  assert.ok(r.issues.some((i) => /must be used inside <Select>/.test(i.message)));
});

test("presentational sub-component (no [CascadingParameter]) is allowed standalone", () => {
  // DialogHeader is a styled div — using it without <Dialog> must NOT error.
  const r = validate('<DialogHeader Class="pb-2">Title</DialogHeader>');
  assert.equal(r.ok, true);
  assert.ok(!r.issues.some((i) => /must be used inside/.test(i.message)));
});

test("sub-component inside its parent passes", () => {
  const r = validate('<Select><SelectItem Value="x" /></Select>');
  assert.equal(r.ok, true);
  assert.equal(r.issues.length, 0);
});

test("pass-through HTML attributes are never flagged", () => {
  const r = validate('<Select class="w-full" style="color:red" id="s1" data-test="x" aria-label="y" />');
  assert.equal(r.issues.length, 0);
});

test("dynamic @-expressions are not statically checked", () => {
  assert.equal(validate('<Button Variant="@dynamicVariant" />').ok, true);
  assert.equal(validate('<Select Value="@x" Side="@s" />').ok, true);
});

test("Blazor directives (@bind-, @on*, @ref, @key) are ignored", () => {
  const r = validate('<Select @bind-Value="v" @onclick="Foo" @ref="el" @key="k" />');
  assert.equal(r.issues.length, 0);
});

test("non-Lumeo / lowercase HTML tags are ignored", () => {
  const r = validate('<div Foo="x"><span Bar="y">text</span></div>');
  assert.equal(r.ok, true);
  assert.equal(r.issues.length, 0);
});
