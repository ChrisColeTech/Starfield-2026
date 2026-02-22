import test from "node:test";
import assert from "node:assert/strict";
import { resolveStandardCharacter } from "../src/generate/resolver";
import { LoadedMetadata } from "../src/types/metadata";

const metadata: LoadedMetadata = {
  sourcePath: "fixture",
  itemsById: {
    body: {
      id: "body",
      name: "Body",
      typeName: "body",
      priority: 0,
      required: ["male"],
      animations: ["idle"],
      tags: [],
      requiredTags: [],
      excludedTags: [],
      path: [],
      variants: ["light"],
      layers: {
        layer_1: { zPos: 0, male: "body/base" }
      },
      credits: [],
      raw: {}
    },
    sword: {
      id: "sword",
      name: "Sword",
      typeName: "weapon",
      priority: 0,
      required: ["male"],
      animations: ["idle"],
      tags: [],
      requiredTags: [],
      excludedTags: [],
      path: [],
      variants: ["steel"],
      layers: {
        layer_1: { zPos: 50, male: "weapon/sword" }
      },
      credits: [],
      raw: {}
    },
    cloak: {
      id: "cloak",
      name: "Cloak",
      typeName: "back",
      priority: 0,
      required: ["male"],
      animations: ["idle"],
      tags: [],
      requiredTags: [],
      excludedTags: [],
      path: [],
      variants: ["brown"],
      layers: {
        layer_10: { zPos: 50, male: "back/cloak_top" },
        layer_2: { zPos: 50, male: "back/cloak_bottom" }
      },
      credits: [],
      raw: {}
    }
  },
  items: [],
  categoryTree: { items: [], children: {} },
  bodyTypes: ["male"]
};

test("standard resolution layer ordering stays deterministic", () => {
  const preset = {
    bodyType: "male",
    selections: [
      { itemId: "body", variant: null },
      { itemId: "sword", variant: null },
      { itemId: "cloak", variant: null }
    ]
  };

  const first = resolveStandardCharacter(preset, metadata, "D:/assets");
  const second = resolveStandardCharacter(preset, metadata, "D:/assets");

  const firstKeys = first.layers.map((layer) => `${layer.itemId}:${layer.layerNum}:${layer.zPos}:${layer.selectionIndex}`);
  const secondKeys = second.layers.map((layer) => `${layer.itemId}:${layer.layerNum}:${layer.zPos}:${layer.selectionIndex}`);

  assert.deepEqual(firstKeys, secondKeys);
  assert.deepEqual(firstKeys, ["body:1:0:0", "sword:1:50:1", "cloak:2:50:2", "cloak:10:50:2"]);
});
