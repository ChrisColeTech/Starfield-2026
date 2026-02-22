const fs = require('fs');
const path = require('path');

const OUTPUT_DIR = path.resolve(__dirname, '..', 'Assets', 'Sprites');

// Color palettes for variety
const COLORS = {
  // Bottom layer - darker greens
  bottom: ['#186a1e', '#1a7021', '#207627', '#217d28', '#1e5a18', '#165214'],
  // Top layer - medium greens  
  top: ['#1e7a1e', '#228b22', '#2d9633', '#268029', '#1f7523', '#1b6920'],
  // Front layer - brighter greens
  front: ['#2ea32e', '#32cd32', '#3cb443', '#4acc52', '#38b840', '#30a838'],
  // Extra highlights
  highlight: ['#4dd854', '#55ee5c', '#66ff66', '#50dd50', '#58e858'],
};

function randomFrom(arr) {
  return arr[Math.floor(Math.random() * arr.length)];
}

function generateBlade(x, baseY, tipY, width = 2, sway = 0) {
  const halfWidth = width / 2;
  const tipX = Math.max(0, Math.min(32, x + halfWidth + sway)); // Clamp to 0-32
  const leftX = Math.max(0, x);
  const rightX = Math.min(32, x + width);
  return `${leftX},${baseY} ${rightX},${baseY} ${tipX.toFixed(1)},${tipY.toFixed(1)}`;
}

function generateDoubleLayerGrass(frameIndex = 0) {
  const blades = [];
  const sway = (frameIndex - 2) * 0.5; // -1 to +1 sway based on frame
  
  // Bottom layer: 16 blades, tips at y=10-18
  for (let i = 0; i < 16; i++) {
    const x = i * 2;
    const tipY = 10 + (i % 5) * 2 + Math.sin(i * 1.3) * 2;
    const color = randomFrom(COLORS.bottom);
    const bladeSway = sway + (Math.sin(i * 0.7) * 0.5);
    blades.push(`  <polygon points="${generateBlade(x, 32, tipY, 2, bladeSway)}" fill="${color}"/>`);
  }
  
  // Top layer: 8 blades, tips at y=0-6, bases at y=20
  for (let i = 0; i < 8; i++) {
    const x = i * 4;
    const tipY = Math.abs((i % 4) * 1.5) + Math.sin(i * 1.1) * 1;
    const color = randomFrom(COLORS.top);
    const bladeSway = sway + (Math.sin(i * 0.9) * 0.7);
    blades.push(`  <polygon points="${generateBlade(x, 20, tipY, 2, bladeSway)}" fill="${color}"/>`);
  }
  
  // Front bright layer: 12 blades, tips at y=8-16
  for (let i = 0; i < 12; i++) {
    const x = i * 2.7 + 0.5;
    const tipY = 8 + (i % 4) * 2 + Math.sin(i * 1.5) * 2;
    const color = randomFrom(COLORS.front);
    const bladeSway = sway + (Math.sin(i * 1.2) * 0.6);
    blades.push(`  <polygon points="${generateBlade(x, 32, tipY, 2, bladeSway)}" fill="${color}"/>`);
  }
  
  // Extra highlight blades: sparse, bright
  for (let i = 0; i < 6; i++) {
    const x = i * 5.5 + 1;
    const tipY = 12 + (i % 3) * 2;
    const color = randomFrom(COLORS.highlight);
    const bladeSway = sway + (Math.sin(i * 1.8) * 0.4);
    blades.push(`  <polygon points="${generateBlade(x, 32, tipY, 1.5, bladeSway)}" fill="${color}" opacity="0.7"/>`);
  }

  return `<svg xmlns="http://www.w3.org/2000/svg" width="32" height="32" viewBox="0 0 32 32">
  <rect x="0" y="28" width="32" height="4" fill="#1a7021"/>
${blades.join('\n')}
</svg>
`;
}

function generateSingleLayerGrass(frameIndex = 0) {
  const blades = [];
  const sway = (frameIndex - 2) * 0.5;
  
  // Dense single layer: 24 blades, tips at y=8-16
  for (let i = 0; i < 24; i++) {
    const x = i * 1.35;
    const tipY = 8 + (i % 6) * 1.5 + Math.sin(i * 1.3) * 2;
    const color = randomFrom([...COLORS.bottom, ...COLORS.front]);
    const bladeSway = sway + (Math.sin(i * 0.8) * 0.5);
    blades.push(`  <polygon points="${generateBlade(x, 32, tipY, 1.5, bladeSway)}" fill="${color}"/>`);
  }
  
  // Highlight blades
  for (let i = 0; i < 8; i++) {
    const x = i * 4 + 0.5;
    const tipY = 10 + (i % 3) * 2;
    const color = randomFrom(COLORS.highlight);
    const bladeSway = sway + (Math.sin(i * 1.1) * 0.4);
    blades.push(`  <polygon points="${generateBlade(x, 32, tipY, 1.5, bladeSway)}" fill="${color}" opacity="0.6"/>`);
  }

  return `<svg xmlns="http://www.w3.org/2000/svg" width="32" height="32" viewBox="0 0 32 32">
  <rect x="0" y="28" width="32" height="4" fill="#1a7021"/>
${blades.join('\n')}
</svg>
`;
}

function main() {
  // Generate double-layer grass
  for (let i = 0; i < 5; i++) {
    const filename = i === 0 ? 'tile_tall_grass.svg' : `tile_tall_grass_${i - 1}.svg`;
    const content = generateDoubleLayerGrass(i);
    fs.writeFileSync(path.join(OUTPUT_DIR, filename), content);
    console.log(`Generated ${filename}`);
  }
  
  // Generate single-layer grass
  for (let i = 0; i < 5; i++) {
    const filename = i === 0 ? 'tile_tall_grass_single.svg' : `tile_tall_grass_single_${i - 1}.svg`;
    const content = generateSingleLayerGrass(i);
    fs.writeFileSync(path.join(OUTPUT_DIR, filename), content);
    console.log(`Generated ${filename}`);
  }
  
  console.log('\nDone! Run node tools/convert_sprites.js to generate PNGs.');
}

main();
