import { describe, it } from 'node:test';
import assert from 'node:assert/strict';
import { GARC } from '../src/lib/ohana/Containers/GARC.js';
import { LZSS_Ninty } from '../src/lib/ohana/Compressions/LZSS_Ninty.js';
import { FileIO, formatType } from '../src/lib/ohana/Core/FileIO.js';
import { BinaryReader } from '../src/lib/ohana/Core/BinaryReader.js';
import { PkmnContainer } from '../src/lib/ohana/Containers/PkmnContainer.js';

const TEST_ROOT = 'D:/Projects/Starfield/src/Starfield.Tests/sun-moon-dump/RomFS/a';

// a/0/9/4 = Pokemon model archive: 10,549 entries, 10,548 compressed .pc files
const POKEMON_GARC = `${TEST_ROOT}/0/9/4`;
// a/2/0/3 = smaller archive with .cm entries (uncompressed PkmnContainers)
const CM_GARC = `${TEST_ROOT}/2/0/3`;

function extractEntry(container: ReturnType<typeof GARC.loadFile>, index: number): Buffer {
  const entry = container.content[index];
  const reader = container.data!;
  reader.seek(entry.fileOffset);
  const raw = reader.readBytes(entry.fileLength);
  if (entry.doDecompression) {
    return LZSS_Ninty.decompress(raw);
  }
  return raw;
}

describe('GARC pipeline', () => {
  it('should parse the full Pokemon model GARC (10k+ entries)', () => {
    const container = GARC.loadFile(POKEMON_GARC);
    assert.equal(container.content.length, 10549);

    const pcEntries = container.content.filter(e => e.name.endsWith('.pc'));
    assert.equal(pcEntries.length, 10548);

    // Every .pc entry should be compressed
    for (const e of pcEntries) {
      assert.equal(e.doDecompression, true);
    }
  });

  it('should extract, decompress, and parse a .pc entry into sub-files', () => {
    const container = GARC.loadFile(POKEMON_GARC);

    // Extract first .pc entry (index 1)
    const decompressed = extractEntry(container, 1);
    assert.equal(decompressed.toString('ascii', 0, 2), 'PC');

    // Parse the PC as a PkmnContainer — should have sub-entries
    const pc = PkmnContainer.load(BinaryReader.fromBuffer(decompressed));
    assert.equal(pc.content.length, 5);

    // Sub-entry 0: GfTexture (magic 0x15122117)
    const sub0 = pc.content[0].data!;
    assert.equal(sub0.readUInt32LE(0), 0x15122117, 'Sub-entry 0 should be GfTexture');

    // Sub-entry 1: also GfTexture
    const sub1 = pc.content[1].data!;
    assert.equal(sub1.readUInt32LE(0), 0x15122117, 'Sub-entry 1 should be GfTexture');

    // Sub-entries 2-3: nested PkmnContainers (PS and PC magic)
    const sub2 = pc.content[2].data!;
    assert.equal(sub2.toString('ascii', 0, 2), 'PS');
    const sub3 = pc.content[3].data!;
    assert.equal(sub3.toString('ascii', 0, 2), 'PC');
  });

  it('should extract and decompress multiple entries across the archive', () => {
    const container = GARC.loadFile(POKEMON_GARC);

    // Sample entries spread across the archive
    for (const idx of [1, 10, 100, 1000, 5000, 10000]) {
      const entry = container.content[idx];
      assert.ok(entry.name.endsWith('.pc'), `Entry ${idx} should be .pc`);

      const decompressed = extractEntry(container, idx);
      assert.ok(decompressed.length > 0, `Entry ${idx} should decompress to non-empty`);
      assert.equal(decompressed.toString('ascii', 0, 2), 'PC', `Entry ${idx} should be a PC container`);

      // Each should parse as a PkmnContainer with sub-entries
      const pc = PkmnContainer.load(BinaryReader.fromBuffer(decompressed));
      assert.ok(pc.content.length > 0, `Entry ${idx} PC should have sub-entries`);
    }
  });

  it('should load a GARC with .cm entries and parse them as models with GfModel', () => {
    const container = GARC.loadFile(CM_GARC);
    assert.ok(container.content.length > 0);

    const cmEntry = container.content.find(e => e.name.endsWith('.cm'))!;
    assert.equal(cmEntry.doDecompression, false);

    const reader = container.data!;
    reader.seek(cmEntry.fileOffset);
    const raw = reader.readBytes(cmEntry.fileLength);
    assert.equal(raw.toString('ascii', 0, 2), 'CM');

    // FileIO routes CM to CM.load -> GfModel.load, returning a model group
    const result = FileIO.load(BinaryReader.fromBuffer(raw));
    assert.equal(result.type, formatType.model);

    const group = result.data as any;
    assert.ok(group.model.length > 0, 'CM should contain at least one model');
  });
});
