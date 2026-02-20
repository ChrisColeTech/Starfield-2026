import type { GeneratorService, GenerationContext, SpriteFrame } from '../types/index.js';
import { SeededRandom } from '../utils/seededRandom.js';
import { FLOWER_PALETTES } from '../utils/colorPalettes.js';

export class FlowerGenerator implements GeneratorService {
  readonly type = 'flower' as const;
  readonly label = 'Flower';
  readonly defaultFrames = 3;
  readonly variants = Object.keys(FLOWER_PALETTES);

  generate(context: GenerationContext): SpriteFrame {
    const rng = new SeededRandom(context.seed + context.frameIndex);
    const size = 16;
    const variant = context.variant ?? 'red';
    const palette = FLOWER_PALETTES[variant] ?? FLOWER_PALETTES.red;

    const cx = size / 2;
    const cy = size / 2 - 1;
    const petalSize = 2.5 + rng.float(0, 0.5);
    const sway = Math.sin(context.frameIndex * 0.8) * 0.5;

    // Stem
    const stem = `<line x1="${cx}" y1="${cy + 3}" x2="${cx + sway}" y2="${size - 1}" stroke="#2d6b25" stroke-width="1.2"/>`;

    // Petals
    const petals: string[] = [];
    for (let angle = 0; angle < 360; angle += 72) {
      const rad = ((angle + context.frameIndex * 5) * Math.PI) / 180;
      const px = cx + Math.cos(rad) * petalSize;
      const py = cy + Math.sin(rad) * petalSize;
      const color = rng.pick(palette.petals);
      petals.push(`<circle cx="${px}" cy="${py}" r="${petalSize * 0.6}" fill="${color}"/>`);
    }

    // Center
    const center = `<circle cx="${cx}" cy="${cy}" r="1.5" fill="${palette.center}"/>`;

    const svg = [
      `<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 ${size} ${size}" width="${size}" height="${size}">`,
      stem,
      ...petals,
      center,
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
