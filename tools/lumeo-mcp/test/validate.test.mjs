import { test } from "node:test";
import assert from "node:assert/strict";
import { createValidator } from "../dist/validate.js";

// Small synthetic catalog mirroring the real ApiComponent shape, so the validator
// is exercised independent of the (large, churning) live registry.
const catalog = [
  {
    name: "Select",
    parameters: [{ name: "Value" }, { name: "Disabled" }, { name: "Side" }],
    enums: [{ name: "Side", values: ["Top", "Bottom", "Left", "Right"] }],
    subComponents: {
      SelectItem: { componentName: "SelectItem", parameters: [{ name: "Value" }], enums: [] },
    },
  },
  {
    name: "Button",
    parameters: [{ name: "Variant" }, { name: "Disabled" }],
    enums: [{ name: "ButtonVariant", values: ["Default", "Outline", "Ghost"] }],
    subComponents: {},
  },
];
const validate = createValidator(catalog);

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

test("invalid enum value whose text contains the enum name is an error", () => {
  // "Sideways" contains "Side" → recognised as a Side attempt, then rejected.
  const r = validate('<Select Side="Sideways" />');
  assert.equal(r.ok, false);
  assert.ok(r.issues.some((i) => /not a valid Side value/.test(i.message)));
});

test("valid enum value passes — bare and qualified", () => {
  assert.equal(validate('<Button Variant="Outline" />').ok, true);
  assert.equal(validate('<Button Variant="ButtonVariant.Ghost" />').ok, true);
  assert.equal(validate('<Select Side="Bottom" />').ok, true);
});

test("sub-component without its required parent is an error", () => {
  const r = validate('<SelectItem Value="x" />');
  assert.equal(r.ok, false);
  assert.ok(r.issues.some((i) => /must be used inside <Select>/.test(i.message)));
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
