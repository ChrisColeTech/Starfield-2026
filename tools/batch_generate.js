const fs = require('fs');
const path = require('path');
const { execSync } = require('child_process');

const INPUT_DIR = path.resolve(__dirname, '..', 'Assets', 'Sprites');
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
}

const PALETTES = {
  grass: {
    bottom: ['#186a1e', '#1a7021', '#207627', '#217d28', '#1e5a18', '#165214'],
    top: ['#1e7a1e', '#228b22', '#2d9633', '#268029', '#1f7523', '#1b6920'],
    front: ['#2ea32e', '#32cd32', '#3cb443', '#4acc52', '#38b840', '#30a838'],
    highlight: ['#4dd854', '#55ee5c', '#66ff66', '#50dd50', '#58e858'],
  },
  flower: {
    red: { petals: ['#e63946', '#d62839', '#c91831', '#ff4d5a', '#cc2233'], center: '#ff9800' },
    pink: { petals: ['#ff69b4', '#ff1493', '#db7093', '#ffb6c1', '#ff85a2'], center: '#ffd700' },
    yellow: { petals: ['#ffd700', '#ffec00', '#f4d03f', '#ffdf00', '#e6c200'], center: '#8b4513' },
    white: { petals: ['#ffffff', '#f8f8ff', '#f0f0f0', '#fafafa', '#f5f5f5'], center: '#ffeb3b' },
    purple: { petals: ['#9b59b6', '#8e44ad', '#a569bd', '#7d3c98', '#bb8fce'], center: '#ffc107' },
  },
  tree: {
    trunk: ['#8b4513', '#a0522d', '#6b4423', '#5d3a1a'],
    leaves: ['#228b22', '#2e8b57', '#32cd32', '#3cb371', '#006400'],
    autumn: ['#ff8c00', '#ffa500', '#ff4500', '#dc143c', '#b8860b'],
  },
};

function generateBlade(x, baseY, tipY, width, sway) {
  const halfWidth = width / 2;
  const tipX = Math.max(0, Math.min(32, x + halfWidth + sway));
  const leftX = Math.max(0, x);
  const rightX = Math.min(32, x + width);
  return `${leftX.toFixed(1)},${baseY.toFixed(1)} ${rightX.toFixed(1)},${baseY.toFixed(1)} ${tipX.toFixed(1)},${tipY.toFixed(1)}`;
}

function generateGrassFrames(baseSvg, frameCount, seed) {
  const frames = [];
  const rng = new SeededRandom(seed);
  
  for (let f = 0; f < frameCount; f++) {
    const sway = Math.sin((f / frameCount) * Math.PI * 2) * 1.5;
    const blades = [];
    
    for (let i = 0; i < 16; i++) {
      const x = i * 2;
      const tipY = 10 + (i % 5) * 2 + Math.sin(i * 1.3) * 2;
      const color = PALETTES.grass.bottom[i % PALETTES.grass.bottom.length];
      const bladeSway = sway + Math.sin(i * 0.7) * 0.5;
      blades.push(`  <polygon points="${generateBlade(x, 32, tipY, 2, bladeSway)}" fill="${color}"/>`);
    }
    
    for (let i = 0; i < 8; i++) {
      const x = i * 4;
      const tipY = Math.abs((i % 4) * 1.5) + Math.sin(i * 1.1) * 1;
      const color = PALETTES.grass.top[i % PALETTES.grass.top.length];
      const bladeSway = sway + Math.sin(i * 0.9) * 0.7;
      blades.push(`  <polygon points="${generateBlade(x, 20, tipY, 2, bladeSway)}" fill="${color}"/>`);
    }
    
    for (let i = 0; i < 12; i++) {
      const x = i * 2.7 + 0.5;
      const tipY = 8 + (i % 4) * 2 + Math.sin(i * 1.5) * 2;
      const color = PALETTES.grass.front[i % PALETTES.grass.front.length];
      const bladeSway = sway + Math.sin(i * 1.2) * 0.6;
      blades.push(`  <polygon points="${generateBlade(x, 32, tipY, 2, bladeSway)}" fill="${color}"/>`);
    }
    
    frames.push(`<svg xmlns="http://www.w3.org/2000/svg" width="32" height="32" viewBox="0 0 32 32">
  <rect x="0" y="28" width="32" height="4" fill="#1a7021"/>
${blades.join('\n')}
</svg>`);
  }
  
  return frames;
}

function generateFlowerFrames(colorType, frameCount, seed) {
  const frames = [];
  const rng = new SeededRandom(seed);
  const palette = PALETTES.flower[colorType] || PALETTES.flower.red;
  
  for (let f = 0; f < frameCount; f++) {
    const sway = Math.sin((f / frameCount) * Math.PI * 2) * 0.3;
    const elements = [];
    
    elements.push(`  <ellipse cx="16" cy="30" rx="6" ry="2" fill="rgba(0,0,0,0.15)"/>`);
    
    elements.push(`  <path d="M16,32 Q14,24 16,16" stroke="#228b22" stroke-width="2" fill="none"/>`);
    elements.push(`  <ellipse cx="20" cy="24" rx="4" ry="2" fill="#32cd32" transform="rotate(-20 20 24)"/>`);
    
    const petalCount = 5;
    for (let i = 0; i < petalCount; i++) {
      const angle = (i / petalCount) * Math.PI * 2 - Math.PI / 2;
      const px = 16 + Math.cos(angle) * 7 + sway;
      const py = 16 + Math.sin(angle) * 7;
      const color = palette.petals[i % palette.petals.length];
      elements.push(`  <ellipse cx="${px.toFixed(1)}" cy="${py.toFixed(1)}" rx="4" ry="5" fill="${color}" transform="rotate(${(angle * 180 / Math.PI + 90).toFixed(0)} ${px.toFixed(1)} ${py.toFixed(1)})"/>`);
    }
    
    elements.push(`  <circle cx="${16 + sway}" cy="16" r="3" fill="${palette.center}"/>`);
    
    frames.push(`<svg xmlns="http://www.w3.org/2000/svg" width="32" height="32" viewBox="0 0 32 32">
${elements.join('\n')}
</svg>`);
  }
  
  return frames;
}

function generateTreeFrames(isAutumn, frameCount, seed) {
  const frames = [];
  const rng = new SeededRandom(seed);
  const leafColors = isAutumn ? PALETTES.tree.autumn : PALETTES.tree.leaves;
  
  for (let f = 0; f < frameCount; f++) {
    const sway = Math.sin((f / frameCount) * Math.PI * 2) * 0.3;
    const elements = [];
    
    elements.push(`  <ellipse cx="16" cy="31" rx="10" ry="3" fill="rgba(0,0,0,0.15)"/>`);
    elements.push(`  <rect x="13" y="20" width="6" height="12" fill="#8b4513"/>`);
    elements.push(`  <rect x="13" y="20" width="2" height="12" fill="#a0522d"/>`);
    
    const layers = [
      { y: 16, rx: 10, ry: 6 },
      { y: 12, rx: 8, ry: 5 },
      { y: 8, rx: 6, ry: 4 },
      { y: 5, rx: 4, ry: 3 },
    ];
    
    layers.forEach((layer, i) => {
      const layerSway = sway * (i + 1) * 0.3;
      elements.push(`  <ellipse cx="${16 + layerSway}" cy="${layer.y}" rx="${layer.rx}" ry="${layer.ry}" fill="${leafColors[i % leafColors.length]}"/>`);
    });
    
    frames.push(`<svg xmlns="http://www.w3.org/2000/svg" width="32" height="32" viewBox="0 0 32 32">
${elements.join('\n')}
</svg>`);
  }
  
  return frames;
}

const BATCH_CONFIGS = {
  grass: {
    generator: (seed, frames) => generateGrassFrames(null, frames, seed),
    baseName: 'tile_tall_grass',
    frames: 5,
  },
  'grass-single': {
    generator: (seed, frames) => generateGrassFrames(null, frames, seed),
    baseName: 'tile_tall_grass_single',
    frames: 5,
  },
  flower: {
    generator: (seed, frames) => generateFlowerFrames('red', frames, seed),
    baseName: 'tile_flower_red',
    frames: 5,
    variants: ['red', 'pink', 'yellow', 'white', 'purple'],
    variantGenerator: (variant, seed, frames) => generateFlowerFrames(variant, frames, seed),
  },
  'tree-green': {
    generator: (seed, frames) => generateTreeFrames(false, frames, seed),
    baseName: 'tile_tree_green',
    frames: 6,
  },
  'tree-autumn': {
    generator: (seed, frames) => generateTreeFrames(true, frames, seed),
    baseName: 'tile_tree_autumn',
    frames: 6,
  },
};

function processBatch(type, config, seed, outputDir) {
  const files = [];
  
  if (config.variants) {
    config.variants.forEach((variant, vi) => {
      const variantSeed = seed + vi * 1000;
      const frames = config.variantGenerator(variant, variantSeed, config.frames);
      frames.forEach((svg, fi) => {
        const filename = fi === 0 
          ? `${config.baseName.replace(/_[^_]+$/, `_${variant}`)}.svg`
          : `${config.baseName.replace(/_[^_]+$/, `_${variant}`)}_${fi - 1}.svg`;
        files.push({ filename, content: svg });
      });
    });
  } else {
    const frames = config.generator(seed, config.frames);
    frames.forEach((svg, fi) => {
      const filename = fi === 0 ? `${config.baseName}.svg` : `${config.baseName}_${fi - 1}.svg`;
      files.push({ filename, content: svg });
    });
  }
  
  if (!fs.existsSync(outputDir)) {
    fs.mkdirSync(outputDir, { recursive: true });
  }
  
  files.forEach(({ filename, content }) => {
    fs.writeFileSync(path.join(outputDir, filename), content);
  });
  
  return files.length;
}

function main() {
  const args = process.argv.slice(2);
  
  if (args.length === 0 || args.includes('--help')) {
    console.log(`
Batch Sprite Generator
======================

Usage: node batch_generate.js [options] [types...]

Options:
  --seed <n>       Random seed (default: 12345)
  --output <dir>   Output directory (default: Assets/Sprites)
  --all            Generate all types
  --list           List available types
  --help           Show this help

Types:
  grass            Tall grass animation
  grass-single     Single-layer grass
  flower           Flowers (5 color variants)
  tree-green       Green tree
  tree-autumn      Autumn-colored tree

Examples:
  node batch_generate.js --all
  node batch_generate.js --seed 99 grass flower
  node batch_generate.js --output ./output tree-green
`);
    return;
  }
  
  if (args.includes('--list')) {
    console.log('Available batch types:');
    Object.entries(BATCH_CONFIGS).forEach(([type, config]) => {
      const variants = config.variants ? ` (${config.variants.length} colors)` : '';
      console.log(`  ${type} - ${config.frames} frames${variants}`);
    });
    return;
  }
  
  const seedIdx = args.indexOf('--seed');
  const seed = seedIdx !== -1 ? (parseInt(args[seedIdx + 1]) || 12345) : 12345;
  
  const outIdx = args.indexOf('--output');
  const outputDir = outIdx !== -1 ? path.resolve(args[outIdx + 1]) : OUTPUT_DIR;
  
  const generateAll = args.includes('--all');
  const types = generateAll
    ? Object.keys(BATCH_CONFIGS)
    : args.filter(a => !a.startsWith('--') && BATCH_CONFIGS[a]);
  
  if (types.length === 0) {
    console.log('No valid types. Use --list to see options.');
    return;
  }
  
  let total = 0;
  types.forEach(type => {
    const config = BATCH_CONFIGS[type];
    const typeSeed = seed + type.split('').reduce((a, c) => a + c.charCodeAt(0), 0);
    console.log(`Generating ${type}...`);
    const count = processBatch(type, config, typeSeed, outputDir);
    console.log(`  ${count} files`);
    total += count;
  });
  
  console.log(`\nDone! ${total} files generated.`);
}

main();
