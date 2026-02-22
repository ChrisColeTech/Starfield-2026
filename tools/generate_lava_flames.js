const sharp = require('sharp');
const path = require('path');

const outputDir = path.resolve(__dirname, '..', 'src', 'Starfield.Assets', 'Sprites');

function generateFlameFrame(frameIndex, withBackground) {
  const t = frameIndex;
  const flames = [];
  
  const flameData = [
    { x: 4, baseH: 20, phase: 0 },
    { x: 10, baseH: 26, phase: 1 },
    { x: 16, baseH: 22, phase: 2 },
    { x: 22, baseH: 28, phase: 0.5 },
    { x: 28, baseH: 18, phase: 1.5 },
  ];
  
  flameData.forEach(f => {
    const flicker = Math.sin((t + f.phase) * 1.2) * 4;
    const h = f.baseH + flicker;
    const sway = Math.sin((t + f.phase) * 0.8) * 2;
    const topY = 32 - h;
    const midY = 32 - h * 0.6;
    
    flames.push(`<path d="M${f.x - 3},32 Q${f.x - 2 + sway},${midY} ${f.x},${topY} Q${f.x + 2 + sway},${midY} ${f.x + 3},32 Z" fill="#cc3300"/>`);
    flames.push(`<path d="M${f.x - 2},32 Q${f.x - 1 + sway * 0.7},${midY + 2} ${f.x},${topY + 4} Q${f.x + 1 + sway * 0.7},${midY + 2} ${f.x + 2},32 Z" fill="#ff6600"/>`);
    flames.push(`<path d="M${f.x - 1},32 Q${f.x + sway * 0.5},${midY + 4} ${f.x},${topY + 8} Q${f.x + sway * 0.5},${midY + 4} ${f.x + 1},32 Z" fill="#ffaa00"/>`);
  });
  
  const bg = withBackground ? '<rect x="0" y="28" width="32" height="4" fill="#4a2020"/>' : '';
  
  return `<svg xmlns="http://www.w3.org/2000/svg" width="32" height="32">
  ${bg}
  ${flames.join('\n  ')}
</svg>`;
}

function generateLavaPoolFrame(frameIndex) {
  const t = frameIndex;
  const waves = [];
  
  const waveData = [
    { baseY: 8, baseX: 6, w: 10, phase: 0 },
    { baseY: 14, baseX: 2, w: 14, phase: 1 },
    { baseY: 20, baseX: 8, w: 12, phase: 2 },
    { baseY: 26, baseX: 4, w: 16, phase: 0.5 },
  ];
  
  waveData.forEach(w => {
    const offsetY = Math.sin((t + w.phase) * 1.5) * 2;
    const offsetX = Math.sin((t + w.phase) * 0.7) * 1;
    waves.push(`<rect x="${w.baseX + offsetX}" y="${w.baseY + offsetY}" width="${w.w}" height="3" fill="#ff6600" rx="1.5"/>`);
    waves.push(`<rect x="${w.baseX + 1 + offsetX}" y="${w.baseY + 1 + offsetY}" width="${w.w - 2}" height="1" fill="#ffaa00" rx="0.5"/>`);
  });
  
  return `<svg xmlns="http://www.w3.org/2000/svg" width="32" height="32">
  <rect width="32" height="32" fill="#4a1a0a"/>
  ${waves.join('\n  ')}
</svg>`;
}

async function main() {
  console.log('Generating lava pool frames...');
  for (let i = 0; i < 4; i++) {
    const name = i === 0 ? 'tile_lava_pool' : `tile_lava_pool_${i - 1}`;
    const svg = generateLavaPoolFrame(i);
    await sharp(Buffer.from(svg)).png().toFile(path.join(outputDir, `${name}.png`));
    console.log(`  ${name}.png`);
  }

  console.log('\nGenerating flames frames (with background)...');
  for (let i = 0; i < 5; i++) {
    const name = i === 0 ? 'tile_flames' : `tile_flames_${i - 1}`;
    const svg = generateFlameFrame(i, true);
    await sharp(Buffer.from(svg)).png().toFile(path.join(outputDir, `${name}.png`));
    console.log(`  ${name}.png`);
  }

  console.log('\nGenerating flames single frames (no background)...');
  for (let i = 0; i < 5; i++) {
    const name = i === 0 ? 'tile_flames_single' : `tile_flames_single_${i - 1}`;
    const svg = generateFlameFrame(i, false);
    await sharp(Buffer.from(svg)).png().toFile(path.join(outputDir, `${name}.png`));
    console.log(`  ${name}.png`);
  }

  console.log('\nDone!');
}

main().catch(console.error);
