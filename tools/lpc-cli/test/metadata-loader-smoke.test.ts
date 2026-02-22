import test from "node:test";
import assert from "node:assert/strict";
import { resolve } from "node:path";
import { loadMetadataFromFile } from "../src/metadata/loadMetadata";

test("loads metadata fixture and infers body types", async () => {
  const fixturePath = resolve(__dirname, "fixtures", "item-metadata.fixture.js");
  const metadata = await loadMetadataFromFile(fixturePath);

  assert.equal(metadata.items.length, 2);
  assert.equal(metadata.itemsById.body.id, "body");
  assert.deepEqual(Array.from(metadata.itemsById.hat_blue.variants), ["blue", "red"]);
  assert.ok(metadata.bodyTypes.includes("male"));
  assert.ok(metadata.bodyTypes.includes("female"));
});
