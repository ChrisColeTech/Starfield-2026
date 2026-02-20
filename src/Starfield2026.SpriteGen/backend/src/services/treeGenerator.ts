import type { GeneratorService, GenerationContext, SpriteFrame } from '../types/index.js';
import { SeededRandom } from '../utils/seededRandom.js';
import { TREE_PALETTES } from '../utils/colorPalettes.js';

export class TreeGenerator implements GeneratorService {
  readonly type = 'tree-green' as const;
  readonly label = 'Tree';
  readonly defaultFrames = 3;
  readonly variants = ['green', 'autumn'];

  generate(context: GenerationContext): SpriteFrame {
    const rng = new SeededRandom(context.seed + context.frameIndex);
    const size = 16;
    const variant = context.variant ?? 'green';
    const palette = TREE_PALETTES[variant as keyof typeof TREE_PALETTES] ?? TREE_PALETTES.green;

    const cx = size / 2;
    const trunkW = 2;
    const sway = Math.sin(context.frameIndex * 0.6) * 0.3;

    // Trunk
    const trunk = `<rect x="${cx - trunkW / 2}" y="9" width="${trunkW}" height="6" fill="${rng.pick(palette.trunk)}"/>`;

    // Canopy blobs
    const blobs: string[] = [];
    const blobCount = rng.int(4, 7);
    for (let i = 0; i < blobCount; i++) {
      const bx = cx + rng.float(-4, 4) + sway;
      const by = rng.float(1, 8);
      const br = rng.float(2, 4);
      const color = rng.pick(palette.leaves);
      blobs.push(`<circle cx="${bx}" cy="${by}" r="${br}" fill="${color}"/>`);
    }

    const svg = [
      `<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 ${size} ${size}" width="${size}" height="${size}">`,
      trunk,
      ...blobs,
      '</svg>',
    ].join('\n');

    return { svg, width: size, height: size };
  }

  generateAll(seed: number, frames?: number, variant?: string): SpriteFrame[] {
    const total = frames ?? this.defaultFrames;
    return Array.from({ length: total }, (_, i) =>
      this.generate({ seed, frameIndex: i, totalFrames: total, variant })
    );
  }
}
