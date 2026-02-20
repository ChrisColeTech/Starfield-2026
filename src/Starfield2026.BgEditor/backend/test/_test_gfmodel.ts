import { GARC } from '../src/lib/ohana/Containers/GARC.js';
import { LZSS_Ninty } from '../src/lib/ohana/Compressions/LZSS_Ninty.js';
import { PkmnContainer } from '../src/lib/ohana/Containers/PkmnContainer.js';
import { GfModel } from '../src/lib/ohana/Models/PocketMonsters/GfModel.js';
import { BinaryReader } from '../src/lib/ohana/Core/BinaryReader.js';

const c = GARC.loadFile('D:/Projects/Starfield/src/Starfield.Tests/sun-moon-dump/RomFS/a/0/9/4');

// Try all PC container entries
for (let entryIdx = 0; entryIdx < c.content.length; entryIdx++) {
  const e = c.content[entryIdx];
  c.data!.seek(e.fileOffset);
  let d: Buffer;
  try {
    d = LZSS_Ninty.decompress(c.data!.readBytes(e.fileLength));
  } catch {
    continue; // not compressed or different format
  }

  let pc;
  try {
    pc = PkmnContainer.load(BinaryReader.fromBuffer(d));
  } catch {
    continue; // not a PC container
  }

  for (let subIdx = 0; subIdx < pc.content.length; subIdx++) {
    const sub = pc.content[subIdx].data;
    if (!sub || sub.length < 4) continue;

    const magic = sub.readUInt32LE(0);
    if (magic !== 0x15122117) continue; // only model magic

    console.log(`Entry ${entryIdx}, sub ${subIdx}: ${sub.length} bytes, magic=0x${magic.toString(16)}`);

    try {
      const model = GfModel.loadModel(BinaryReader.fromBuffer(sub));
      console.log(`  SUCCESS: "${model.name}", meshes=${model.mesh.length}, bones=${model.skeleton.length}`);
    } catch (err: any) {
      console.error(`  FAILED: ${err.message}`);
      console.error(`  ${err.stack?.split('\n').slice(0, 3).join('\n  ')}`);
    }
  }
}
