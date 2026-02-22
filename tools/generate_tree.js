const sharp = require('sharp');
const fs = require('fs');
const path = require('path');

const outputDir = path.resolve(__dirname, '..', 'src', 'Starfield.Assets', 'Sprites');

const frames = [
  { name: 'tile_tree', sway: 0 },
  { name: 'tile_tree_0', sway: 1 },
  { name: 'tile_tree_1', sway: 0.5 },
  { name: 'tile_tree_2', sway: -0.5 },
  { name: 'tile_tree_3', sway: -1 },
  { name: 'tile_tree_4', sway: -0.5 },
];

function generateTreeSVG(sway) {
  const s = sway;
  return `<svg xmlns="http://www.w3.org/2000/svg" width="64" height="64">
  <rect x="${28 + s * 0.3}" y="44" width="8" height="20" fill="#5a3a1a"/>
  <circle cx="${32 + s}" cy="24" r="20" fill="#1a5a1a"/>
  <circle cx="${20 + s * 0.8}" cy="30" r="12" fill="#2a6a2a"/>
  <circle cx="${44 + s * 0.8}" cy="30" r="12" fill="#2a6a2a"/>
  <circle cx="${32 + s * 1.2}" cy="14" r="14" fill="#2a7a2a"/>
  <circle cx="${24 + s * 0.6}" cy="20" r="8" fill="#3a8a3a"/>
  <circle cx="${40 + s * 0.6}" cy="20" r="8" fill="#3a8a3a"/>
  <circle cx="${32 + s}" cy="28" r="6" fill="#3a9a3a"/>
</svg>`;
}

async function main() {
  for (const frame of frames) {
    const svg = generateTreeSVG(frame.sway);
    const outputPath = path.join(outputDir, `${frame.name}.png`);
    
    await sharp(Buffer.from(svg))
      .png()
      .toFile(outputPath);
    
    console.log(`Generated ${frame.name}.png`);
  }
  
  console.log('\nDone!');
}

main().catch(console.error);
