import type { GeneratorService, GenerationContext, SpriteFrame } from '../types/index.js';
import { SeededRandom } from '../utils/seededRandom.js';
import { GRASS_PALETTE } from '../utils/colorPalettes.js';

export class GrassGenerator implements GeneratorService {
  readonly type = 'grass' as const;
  readonly label = 'Grass (Multi-tile)';
  readonly defaultFrames = 3;

  generate(context: GenerationContext): SpriteFrame {
    const rng = new SeededRandom(context.seed + context.frameIndex);
    const size = 16;

    const blades: string[] = [];
    const bladeCount = rng.int(8, 14);

    for (let i = 0; i < bladeCount; i++) {
      const x = rng.int(1, size - 2);
      const baseY = size - 1;
      const height = rng.int(4, 10);
      const sway = rng.float(-1.5, 1.5) + (context.frameIndex * 0.5);
      const color = rng.pick([...GRASS_PALETTE.base, ...GRASS_PALETTE.highlight]);

      blades.push(
        `<line x1="${x}" y1="${baseY}" x2="${x + sway}" y2="${baseY - height}" stroke="${color}" stroke-width="1.2" stroke-linecap="round"/>`
      );
    }

    // Ground fill
    const ground = `<rect x="0" y="${size - 2}" width="${size}" height="2" fill="${GRASS_PALETTE.shadow[0]}" opacity="0.4"/>`;

    const svg = [
      `<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 ${size} ${size}" width="${size}" height="${size}">`,
      ground,
      ...blades,
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
