import test from "node:test";
import assert from "node:assert/strict";
import { buildActionExportAreas, validateActionAreaNames } from "../src/generate/splitActions";
import { FRAME_SIZE, SPRITESHEET_HEIGHT, SPRITESHEET_WIDTH, STANDARD_ANIMATIONS } from "../src/generate/constants";

test("action area planner keeps standard ordering and appends custom actions", () => {
  const areas = buildActionExportAreas({
    customAnimations: [
      {
        name: "wheelchair",
        baseAnimation: "sit",
        frameSize: 64,
        rows: 4,
        columns: 2,
        width: 128,
        height: 256,
        yOffset: SPRITESHEET_HEIGHT
      }
    ]
  });

  const standardNames = STANDARD_ANIMATIONS.map((animation) => animation.name);
  assert.deepEqual(
    areas.slice(0, standardNames.length).map((area) => area.name),
    standardNames
  );

  assert.equal(areas.at(-1)?.name, "wheelchair");
  assert.equal(areas[0]?.top, 0);
  assert.equal(areas[0]?.width, SPRITESHEET_WIDTH);
  assert.equal(areas[0]?.height, 4 * FRAME_SIZE);
  assert.equal(areas.at(-1)?.top, SPRITESHEET_HEIGHT);
  assert.equal(areas.at(-1)?.width, 128);
  assert.equal(areas.at(-1)?.height, 256);
});

test("action area name validation rejects duplicates and unsafe names", () => {
  assert.throws(
    () =>
      validateActionAreaNames([
        { name: "idle", left: 0, top: 0, width: SPRITESHEET_WIDTH, height: 4 * FRAME_SIZE },
        { name: "idle", left: 0, top: 10, width: SPRITESHEET_WIDTH, height: 4 * FRAME_SIZE }
      ]),
    /collision/
  );

  assert.throws(
    () =>
      validateActionAreaNames([
        { name: "walk/alt", left: 0, top: 0, width: SPRITESHEET_WIDTH, height: 4 * FRAME_SIZE }
      ]),
    /unsupported characters/
  );
});
