import type { GeneratorService, GenerationContext, SpriteFrame } from '../types/index.js';
import { SeededRandom } from '../utils/seededRandom.js';
import { BUSH_PALETTE } from '../utils/colorPalettes.js';

export class BushGenerator implements GeneratorService {
  readonly type = 'bush' as const;
  readonly label = 'Bush';
  readonly defaultFrames = 3;

  generate(context: GenerationContext): SpriteFrame {
    const rng = new SeededRandom(context.seed + context.frameIndex);
    const size = 16;
    const sway = Math.sin(context.frameIndex * 0.5) * 0.4;

    // Leaf blobs
    const blobs: string[] = [];
    const blobCount = rng.int(5, 8);
    for (let i = 0; i < blobCount; i++) {
      const bx = size / 2 + rng.float(-5, 5) + sway;
      const by = rng.float(4, 12);
      const br = rng.float(2.5, 4.5);
      const color = rng.pick(BUSH_PALETTE.leaves);
      blobs.push(`<circle cx="${bx}" cy="${by}" r="${br}" fill="${color}"/>`);
    }

    // Optional berries
    const berries: string[] = [];
    if (rng.next() > 0.4) {
      const berryCount = rng.int(2, 5);
      for (let i = 0; i < berryCount; i++) {
        const bx = size / 2 + rng.float(-4, 4);
        const by = rng.float(5, 11);
        const color = rng.pick(BUSH_PALETTE.berry);
        berries.push(`<circle cx="${bx}" cy="${by}" r="0.8" fill="${color}"/>`);
      }
    }

    const svg = [
      `<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 ${size} ${size}" width="${size}" height="${size}">`,
      ...blobs,
      ...berries,
      '</svg>',
    ].join('\n');

    return { svg, width: size, height: size };
  }

  generateAll(seed: number, frames?: number): SpriteFrame[] {
    const total = frames ?? this.defaultFrames;
    return Array.from({ length: total }, (_, i) =>
      this.generate({ seed, frameIndex: i, totalFrames: total })
    );
  }
}
