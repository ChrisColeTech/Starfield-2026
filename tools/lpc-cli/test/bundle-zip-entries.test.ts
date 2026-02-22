import test from "node:test";
import assert from "node:assert/strict";
import { buildBundleEntries } from "../src/generate/bundleZip";

test("bundle entry planner keeps base files first and sorts actions by filename", () => {
  const entries = buildBundleEntries({
    outputBaseName: "npc_guard",
    outDir: "D:/tmp/out",
    spritesheetPath: "D:/tmp/out/npc_guard.spritesheet.png",
    characterPath: "D:/tmp/out/npc_guard.character.json",
    creditsCsvPath: "D:/tmp/out/npc_guard.credits.csv",
    creditsTxtPath: "D:/tmp/out/npc_guard.credits.txt",
    splitActionPaths: [
      "D:/tmp/out/npc_guard.actions/walk.png",
      "D:/tmp/out/npc_guard.actions/idle.png",
      "D:/tmp/out/npc_guard.actions/backslash.png"
    ]
  });

  assert.deepEqual(
    entries.map((entry) => entry.archivePath),
    [
      "npc_guard/npc_guard.spritesheet.png",
      "npc_guard/npc_guard.character.json",
      "npc_guard/npc_guard.credits.csv",
      "npc_guard/npc_guard.credits.txt",
      "npc_guard/actions/backslash.png",
      "npc_guard/actions/idle.png",
      "npc_guard/actions/walk.png"
    ]
  );
});

test("bundle entry planner rejects archive collisions", () => {
  assert.throws(
    () =>
      buildBundleEntries({
        outputBaseName: "npc_guard",
        outDir: "D:/tmp/out",
        spritesheetPath: "D:/tmp/out/npc_guard.spritesheet.png",
        characterPath: "D:/tmp/out/npc_guard.character.json",
        creditsCsvPath: "D:/tmp/out/npc_guard.credits.csv",
        creditsTxtPath: "D:/tmp/out/npc_guard.credits.txt",
        splitActionPaths: [
          "D:/tmp/out/npc_guard.actions/idle.png",
          "D:/tmp/other/idle.png"
        ]
      }),
    /collision/
  );
});
