const sharp = require('sharp');
const path = require('path');

const outputDir = path.resolve(__dirname, '..', 'src', 'Starfield.Assets', 'Sprites');

function generateLavaFrame(frameIndex) {
  const offset = frameIndex * 2;
  const bubbleOffset = Math.sin(frameIndex * 1.5) * 2;
  
  return `<svg xmlns="http://www.w3.org/2000/svg" width="32" height="32">
  <rect width="32" height="32" fill="#1a0808"/>
  <ellipse cx="${16 + Math.sin(offset * 0.5) * 1}" cy="${16 + Math.cos(offset * 0.3) * 1}" rx="14" ry="12" fill="#cc2200"/>
  <ellipse cx="${16 + Math.sin(offset * 0.7) * 0.5}" cy="${16 + Math.cos(offset * 0.5) * 0.5}" rx="12" ry="10" fill="#ff3300"/>
  <ellipse cx="${14 + Math.sin(offset * 0.3) * 1}" cy="${14 + Math.cos(offset * 0.4) * 1}" rx="8" ry="6" fill="#ff5500"/>
  <ellipse cx="${16 + bubbleOffset * 0.3}" cy="${15 + bubbleOffset * 0.2}" rx="5" ry="4" fill="#ff7700"/>
  <circle cx="${14 + Math.sin(offset) * 1.5}" cy="${13 + Math.cos(offset) * 1}" r="2" fill="#ffaa00"/>
  <circle cx="${18 + Math.cos(offset * 0.8) * 1}" cy="${14 + Math.sin(offset * 0.6) * 1}" r="1.5" fill="#ffcc00"/>
</svg>`;
}

async function main() {
  for (let i = 0; i < 4; i++) {
    const name = i === 0 ? 'tile_lava_pool' : `tile_lava_pool_${i - 1}`;
    const svg = generateLavaFrame(i);
    const outputPath = path.join(outputDir, `${name}.png`);
    
    await sharp(Buffer.from(svg))
      .png()
      .toFile(outputPath);
    console.log(`Generated ${name}.png`);
  }
  console.log('\nDone!');
}

main().catch(console.error);
