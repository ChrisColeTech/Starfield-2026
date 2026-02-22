const fs = require('fs');
const path = require('path');

const OUTPUT_DIR = path.resolve(__dirname, '..', 'Assets', 'Sprites');

class SeededRandom {
  constructor(seed = 12345) {
    this.seed = seed;
  }
  
  next() {
    this.seed = (this.seed * 1103515245 + 12345) & 0x7fffffff;
    return this.seed / 0x7fffffff;
  }
  
  range(min, max) {
    return min + this.next() * (max - min);
  }
  
  int(min, max) {
    return Math.floor(this.range(min, max + 1));
  }
  
  pick(arr) {
    return arr[Math.floor(this.next() * arr.length)];
  }
  
  shuffle(arr) {
    const result = [...arr];
    for (let i = result.length - 1; i > 0; i--) {
      const j = Math.floor(this.next() * (i + 1));
      [result[i], result[j]] = [result[j], result[i]];
    }
    return result;
  }
}

const PALETTES = {
  grass: {
    bottom: ['#186a1e', '#1a7021', '#207627', '#217d28', '#1e5a18', '#165214'],
    top: ['#1e7a1e', '#228b22', '#2d9633', '#268029', '#1f7523', '#1b6920'],
    front: ['#2ea32e', '#32cd32', '#3cb443', '#4acc52', '#38b840', '#30a838'],
    highlight: ['#4dd854', '#55ee5c', '#66ff66', '#50dd50', '#58e858'],
  },
  flower: {
    petals: {
      red: ['#e63946', '#d62839', '#c91831', '#ff4d5a', '#cc2233'],
      pink: ['#ff69b4', '#ff1493', '#db7093', '#ffb6c1', '#ff85a2'],
      yellow: ['#ffd700', '#ffec00', '#f4d03f', '#ffdf00', '#e6c200'],
      white: ['#ffffff', '#f8f8ff', '#f0f0f0', '#fafafa', '#f5f5f5'],
      purple: ['#9b59b6', '#8e44ad', '#a569bd', '#7d3c98', '#bb8fce'],
    },
    centers: ['#ffeb3b', '#ffc107', '#ff9800', '#ffd54f', '#ffca28'],
    stems: ['#228b22', '#2e7d32', '#388e3c', '#43a047', '#1b5e20'],
    leaves: ['#32cd32', '#3cb371', '#2e8b57', '#228b22', '#00fa9a'],
  },
  tree: {
    trunk: ['#8b4513', '#a0522d', '#6b4423', '#5d3a1a', '#7c4a1e'],
    trunkHighlight: ['#a0522d', '#b8734a', '#8b5a2b'],
    leaves: {
      green: ['#228b22', '#2e8b57', '#32cd32', '#3cb371', '#006400', '#008000'],
      autumn: ['#ff8c00', '#ffa500', '#ff4500', '#dc143c', '#b8860b', '#cd853f'],
    },
    shadow: ['rgba(0,0,0,0.2)', 'rgba(0,0,0,0.15)', 'rgba(0,0,0,0.1)'],
  },
};

function generateBlade(x, baseY, tipY, width, sway, rng) {
  const halfWidth = width / 2;
  const tipX = Math.max(0, Math.min(32, x + halfWidth + sway));
  const leftX = Math.max(0, x);
  const rightX = Math.min(32, x + width);
  return `${leftX.toFixed(1)},${baseY.toFixed(1)} ${rightX.toFixed(1)},${baseY.toFixed(1)} ${tipX.toFixed(1)},${tipY.toFixed(1)}`;
}

function generateGrass(frameIndex, rng, style = 'double') {
  const blades = [];
  const sway = Math.sin((frameIndex / 4) * Math.PI * 2) * 1.5;
  
  if (style === 'double' || style === 'both') {
    for (let i = 0; i < 16; i++) {
      const x = i * 2;
      const tipY = rng.range(10, 18);
      const color = rng.pick(PALETTES.grass.bottom);
      const bladeSway = sway + rng.range(-0.5, 0.5);
      blades.push(`  <polygon points="${generateBlade(x, 32, tipY, 2, bladeSway)}" fill="${color}"/>`);
    }
    
    for (let i = 0; i < 8; i++) {
      const x = i * 4;
      const tipY = rng.range(0, 6);
      const color = rng.pick(PALETTES.grass.top);
      const bladeSway = sway + rng.range(-0.7, 0.7);
      blades.push(`  <polygon points="${generateBlade(x, 20, tipY, 2, bladeSway)}" fill="${color}"/>`);
    }
    
    for (let i = 0; i < 12; i++) {
      const x = rng.range(0, 30);
      const tipY = rng.range(8, 16);
      const color = rng.pick(PALETTES.grass.front);
      const bladeSway = sway + rng.range(-0.6, 0.6);
      blades.push(`  <polygon points="${generateBlade(x, 32, tipY, 2, bladeSway)}" fill="${color}"/>`);
    }
    
    for (let i = 0; i < 5; i++) {
      const x = rng.range(0, 28);
      const tipY = rng.range(10, 14);
      const color = rng.pick(PALETTES.grass.highlight);
      const bladeSway = sway + rng.range(-0.4, 0.4);
      blades.push(`  <polygon points="${generateBlade(x, 32, tipY, 1.5, bladeSway)}" fill="${color}" opacity="0.7"/>`);
    }
  }
  
  if (style === 'single' || style === 'both') {
    const offset = style === 'both' ? 0 : 0;
    const baseY = style === 'both' ? 32 : 32;
    
    for (let i = 0; i < 20; i++) {
      const x = rng.range(0, 30);
      const tipY = rng.range(8, 16);
      const color = rng.pick([...PALETTES.grass.bottom, ...PALETTES.grass.front]);
      const bladeSway = sway + rng.range(-0.5, 0.5);
      blades.push(`  <polygon points="${generateBlade(x, baseY, tipY, 1.5, bladeSway)}" fill="${color}"/>`);
    }
  }
  
  return `<svg xmlns="http://www.w3.org/2000/svg" width="32" height="32" viewBox="0 0 32 32">
  <rect x="0" y="28" width="32" height="4" fill="#1a7021"/>
${blades.join('\n')}
</svg>`;
}

function generateFlower(frameIndex, rng, colorType = null) {
  const petalColors = colorType ? PALETTES.flower.petals[colorType] : rng.pick(Object.values(PALETTES.flower.petals));
  const centerX = 16;
  const centerY = 16;
  const petalCount = rng.int(5, 8);
  const sway = Math.sin((frameIndex / 4) * Math.PI * 2) * 0.3;
  
  const elements = [];
  
  elements.push(`  <ellipse cx="${centerX + sway}" cy="30" rx="6" ry="2" fill="${rng.pick(PALETTES.tree.shadow)}"/>`);
  
  for (let i = 0; i < 3; i++) {
    const stemX = centerX + rng.range(-2, 2);
    elements.push(`  <path d="M${stemX},32 Q${stemX - 2 + sway},24 ${stemX + sway},16" stroke="${rng.pick(PALETTES.flower.stems)}" stroke-width="2" fill="none"/>`);
  }
  
  for (let i = 0; i < 2; i++) {
    const leafX = centerX + rng.range(-4, 4);
    const leafY = rng.range(22, 28);
    elements.push(`  <ellipse cx="${leafX + sway}" cy="${leafY}" rx="4" ry="2" fill="${rng.pick(PALETTES.flower.leaves)}" transform="rotate(${rng.range(-30, 30)} ${leafX + sway} ${leafY})"/>`);
  }
  
  const petalLength = rng.range(6, 9);
  const petalWidth = rng.range(3, 5);
  
  for (let i = 0; i < petalCount; i++) {
    const angle = (i / petalCount) * Math.PI * 2;
    const px = centerX + Math.cos(angle) * petalLength * 0.5 + sway;
    const py = centerY + Math.sin(angle) * petalLength * 0.5;
    const px2 = centerX + Math.cos(angle) * petalLength + sway;
    const py2 = centerY + Math.sin(angle) * petalLength;
    const color = rng.pick(petalColors);
    elements.push(`  <ellipse cx="${px2.toFixed(1)}" cy="${py2.toFixed(1)}" rx="${petalWidth}" ry="${petalLength * 0.6}" fill="${color}" transform="rotate(${(angle * 180 / Math.PI).toFixed(0)} ${px2.toFixed(1)} ${py2.toFixed(1)})"/>`);
  }
  
  elements.push(`  <circle cx="${centerX + sway}" cy="${centerY}" r="3" fill="${rng.pick(PALETTES.flower.centers)}"/>`);
  elements.push(`  <circle cx="${centerX - 0.5 + sway}" cy="${centerY - 0.5}" r="1" fill="#fff" opacity="0.6"/>`);
  
  return `<svg xmlns="http://www.w3.org/2000/svg" width="32" height="32" viewBox="0 0 32 32">
${elements.join('\n')}
</svg>`;
}

function generateTree(frameIndex, rng, style = 'green') {
  const sway = Math.sin((frameIndex / 6) * Math.PI * 2) * 0.2;
  const elements = [];
  
  elements.push(`  <ellipse cx="16" cy="31" rx="10" ry="3" fill="${rng.pick(PALETTES.tree.shadow)}"/>`);
  
  const trunkWidth = rng.range(4, 6);
  const trunkHeight = rng.range(8, 12);
  const trunkX = 16 - trunkWidth / 2;
  
  elements.push(`  <rect x="${trunkX}" y="${32 - trunkHeight}" width="${trunkWidth}" height="${trunkHeight}" fill="${rng.pick(PALETTES.tree.trunk)}"/>`);
  elements.push(`  <rect x="${trunkX}" y="${32 - trunkHeight}" width="${trunkWidth * 0.3}" height="${trunkHeight}" fill="${rng.pick(PALETTES.tree.trunkHighlight)}"/>`);
  
  const leafColors = style === 'autumn' ? PALETTES.tree.leaves.autumn : PALETTES.tree.leaves.green;
  
  const layers = [
    { y: 32 - trunkHeight - 4, rx: 10, ry: 5 },
    { y: 32 - trunkHeight - 8, rx: 9, ry: 5 },
    { y: 32 - trunkHeight - 12, rx: 7, ry: 4 },
    { y: 32 - trunkHeight - 15, rx: 5, ry: 3 },
  ];
  
  layers.forEach((layer, i) => {
    const layerSway = sway * (i + 1) * 0.3;
    elements.push(`  <ellipse cx="${16 + layerSway}" cy="${layer.y}" rx="${layer.rx}" ry="${layer.ry}" fill="${rng.pick(leafColors)}"/>`);
    elements.push(`  <ellipse cx="${14 + layerSway}" cy="${layer.y - 1}" rx="${layer.rx * 0.4}" ry="${layer.ry * 0.5}" fill="${rng.pick(leafColors)}" opacity="0.7"/>`);
  });
  
  return `<svg xmlns="http://www.w3.org/2000/svg" width="32" height="32" viewBox="0 0 32 32">
${elements.join('\n')}
</svg>`;
}

function generateBush(frameIndex, rng) {
  const sway = Math.sin((frameIndex / 4) * Math.PI * 2) * 0.4;
  const elements = [];
  
  elements.push(`  <ellipse cx="16" cy="30" rx="8" ry="2" fill="${rng.pick(PALETTES.tree.shadow)}"/>`);
  
  const bushBlobs = [
    { cx: 16, cy: 22, rx: 10, ry: 8 },
    { cx: 10, cy: 24, rx: 6, ry: 5 },
    { cx: 22, cy: 24, rx: 6, ry: 5 },
    { cx: 16, cy: 18, rx: 7, ry: 6 },
  ];
  
  bushBlobs.forEach(blob => {
    elements.push(`  <ellipse cx="${blob.cx + sway}" cy="${blob.cy}" rx="${blob.rx}" ry="${blob.ry}" fill="${rng.pick(PALETTES.tree.leaves.green)}"/>`);
  });
  
  for (let i = 0; i < 6; i++) {
    const hx = rng.range(8, 24);
    const hy = rng.range(16, 26);
    elements.push(`  <circle cx="${hx + sway}" cy="${hy}" r="${rng.range(1, 2)}" fill="${rng.pick(PALETTES.tree.leaves.green)}" opacity="0.5"/>`);
  }
  
  return `<svg xmlns="http://www.w3.org/2000/svg" width="32" height="32" viewBox="0 0 32 32">
${elements.join('\n')}
</svg>`;
}

const GENERATORS = {
  grass: { fn: generateGrass, prefix: 'tile_tall_grass', frames: 5 },
  'grass-single': { fn: (f, r) => generateGrass(f, r, 'single'), prefix: 'tile_tall_grass_single', frames: 5 },
  flower: { fn: generateFlower, prefix: 'tile_flower', frames: 5, variants: ['red', 'pink', 'yellow', 'white', 'purple'] },
  'tree-green': { fn: (f, r) => generateTree(f, r, 'green'), prefix: 'tile_tree_green', frames: 6 },
  'tree-autumn': { fn: (f, r) => generateTree(f, r, 'autumn'), prefix: 'tile_tree_autumn', frames: 6 },
  bush: { fn: generateBush, prefix: 'tile_bush', frames: 5 },
};

function generateSprite(type, config, seed) {
  const rng = new SeededRandom(seed);
  const files = [];
  
  if (config.variants) {
    config.variants.forEach((variant, vIndex) => {
      for (let frame = 0; frame < config.frames; frame++) {
        const frameRng = new SeededRandom(seed + vIndex * 1000 + frame);
        const filename = frame === 0 
          ? `${config.prefix}_${variant}.svg`
          : `${config.prefix}_${variant}_${frame - 1}.svg`;
        const content = config.fn(frame, frameRng, variant);
        files.push({ filename, content });
      }
    });
  } else {
    for (let frame = 0; frame < config.frames; frame++) {
      const frameRng = new SeededRandom(seed + frame);
      const filename = frame === 0 
        ? `${config.prefix}.svg`
        : `${config.prefix}_${frame - 1}.svg`;
      const content = config.fn(frame, frameRng);
      files.push({ filename, content });
    }
  }
  
  return files;
}

function main() {
  const args = process.argv.slice(2);
  
  if (args.length === 0 || args.includes('--help')) {
    console.log(`
Sprite Generator
================

Usage: node generate_sprites.js [options] [types...]

Options:
  --seed <number>    Random seed for reproducible generation (default: 12345)
  --all              Generate all sprite types
  --list             List available sprite types
  --help             Show this help

Types:
  grass              Tall grass (double layer)
  grass-single       Tall grass (single layer)
  flower             Flowers (all color variants)
  tree-green         Green tree
  tree-autumn        Autumn tree
  bush               Bush/shrub

Examples:
  node generate_sprites.js --all
  node generate_sprites.js --seed 42 grass flower
  node generate_sprites.js tree-green bush
`);
    return;
  }
  
  if (args.includes('--list')) {
    console.log('Available sprite types:');
    Object.keys(GENERATORS).forEach(type => {
      const config = GENERATORS[type];
      const variants = config.variants ? ` (${config.variants.length} variants)` : '';
      console.log(`  ${type} - ${config.frames} frames${variants}`);
    });
    return;
  }
  
  const seedIndex = args.indexOf('--seed');
  const seed = seedIndex !== -1 ? parseInt(args[seedIndex + 1]) || 12345 : 12345;
  
  const generateAll = args.includes('--all');
  const types = generateAll 
    ? Object.keys(GENERATORS)
    : args.filter(arg => !arg.startsWith('--') && GENERATORS[arg]);
  
  if (types.length === 0) {
    console.log('No valid sprite types specified. Use --list to see available types.');
    return;
  }
  
  if (!fs.existsSync(OUTPUT_DIR)) {
    fs.mkdirSync(OUTPUT_DIR, { recursive: true });
  }
  
  let totalFiles = 0;
  
  types.forEach(type => {
    const config = GENERATORS[type];
    const typeSeed = seed + type.split('').reduce((a, c) => a + c.charCodeAt(0), 0);
    
    console.log(`Generating ${type}...`);
    const files = generateSprite(type, config, typeSeed);
    
    files.forEach(({ filename, content }) => {
      fs.writeFileSync(path.join(OUTPUT_DIR, filename), content);
      console.log(`  ${filename}`);
      totalFiles++;
    });
  });
  
  console.log(`\nDone! Generated ${totalFiles} files.`);
  console.log('Run node tools/convert_sprites.js to generate PNGs.');
}

main();
