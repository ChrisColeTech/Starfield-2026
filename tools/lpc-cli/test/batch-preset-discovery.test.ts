import test from "node:test";
import assert from "node:assert/strict";
import { discoverPresetPathsFromEntries, isPresetJsonFile, orderPresetFileNames } from "../src/batch/presetDiscovery";

test("preset file detection accepts .json regardless of case", () => {
  assert.equal(isPresetJsonFile("npc_guard.preset.json"), true);
  assert.equal(isPresetJsonFile("NPC_GUARD.PRESET.JSON"), true);
  assert.equal(isPresetJsonFile("npc_guard.preset.json.bak"), false);
  assert.equal(isPresetJsonFile("README.md"), false);
});

test("preset ordering is deterministic and case-stable", () => {
  const ordered = orderPresetFileNames([
    "npc_10.preset.json",
    "npc_2.preset.json",
    "Npc_alpha.preset.json",
    "npc_Alpha.preset.json",
    "npc_beta.preset.json"
  ]);

  assert.deepEqual(ordered, [
    "npc_10.preset.json",
    "npc_2.preset.json",
    "npc_Alpha.preset.json",
    "Npc_alpha.preset.json",
    "npc_beta.preset.json"
  ]);
});

test("discovery filters non-files and returns ordered absolute paths", () => {
  const discovered = discoverPresetPathsFromEntries("D:/tmp/presets", [
    { name: "npc_b.preset.json", isFile: () => true },
    { name: "npc_a.preset.json", isFile: () => true },
    { name: "npcs", isFile: () => false },
    { name: "notes.txt", isFile: () => true }
  ]);

  assert.deepEqual(discovered, [
    "D:\\tmp\\presets\\npc_a.preset.json",
    "D:\\tmp\\presets\\npc_b.preset.json"
  ]);
});
