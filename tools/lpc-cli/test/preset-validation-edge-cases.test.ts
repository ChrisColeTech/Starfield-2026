import test from "node:test";
import assert from "node:assert/strict";
import { validatePresetAgainstMetadata } from "../src/validation/validatePreset";
import { LoadedMetadata } from "../src/types/metadata";
import { LpcPreset } from "../src/types/preset";

const metadata: LoadedMetadata = {
  sourcePath: "fixture",
  itemsById: {
    body_light: {
      id: "body_light",
      name: "Body Light",
      typeName: "body",
      priority: 0,
      required: ["male"],
      animations: ["idle"],
      tags: [],
      requiredTags: [],
      excludedTags: [],
      path: [],
      variants: ["light"],
      layers: {},
      credits: [],
      raw: {}
    },
    body_dark: {
      id: "body_dark",
      name: "Body Dark",
      typeName: "body",
      priority: 0,
      required: ["male"],
      animations: ["idle"],
      tags: [],
      requiredTags: [],
      excludedTags: [],
      path: [],
      variants: ["dark"],
      layers: {},
      credits: [],
      raw: {}
    },
    hat: {
      id: "hat",
      name: "Hat",
      typeName: "hat",
      priority: 0,
      required: ["male"],
      animations: ["idle"],
      tags: [],
      requiredTags: [],
      excludedTags: [],
      path: [],
      variants: ["blue", "red"],
      layers: {},
      credits: [],
      raw: {}
    },
    shadow: {
      id: "shadow",
      name: "Shadow",
      typeName: "shadow",
      priority: 0,
      required: ["male"],
      animations: ["idle"],
      tags: [],
      requiredTags: [],
      excludedTags: [],
      path: [],
      variants: [],
      layers: {},
      credits: [],
      raw: {}
    }
  },
  items: [],
  categoryTree: { items: [], children: {} },
  bodyTypes: ["male"]
};

test("duplicate selection group returns strict error", () => {
  const preset: LpcPreset = {
    version: 1,
    bodyType: "male",
    selections: [
      { itemId: "body_light", variant: "light" },
      { itemId: "body_dark", variant: "dark" }
    ]
  };

  const result = validatePresetAgainstMetadata(preset, metadata);
  assert.ok(result.result.errors.some((error) => error.code === "duplicate_selection_group"));
});

test("missing variant for multi-variant item is rejected", () => {
  const preset: LpcPreset = {
    version: 1,
    bodyType: "male",
    selections: [{ itemId: "hat" }]
  };

  const result = validatePresetAgainstMetadata(preset, metadata);
  assert.ok(result.result.errors.some((error) => error.code === "variant_required"));
});

test("variant provided for non-variant item is rejected", () => {
  const preset: LpcPreset = {
    version: 1,
    bodyType: "male",
    selections: [{ itemId: "shadow", variant: "any" }]
  };

  const result = validatePresetAgainstMetadata(preset, metadata);
  assert.ok(result.result.errors.some((error) => error.code === "variant_not_supported"));
});
