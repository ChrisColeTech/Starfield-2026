const sharp = require('sharp');
const path = require('path');

const outputDir = path.resolve(__dirname, '..', 'src', 'Starfield.Assets', 'Sprites');

const svg = `<svg xmlns="http://www.w3.org/2000/svg" width="32" height="32">
  <rect width="32" height="32" fill="#4a2020"/>
  <rect x="0" y="0" width="4" height="4" fill="#8b3a3a"/>
  <rect x="8" y="4" width="4" height="4" fill="#a04040"/>
  <rect x="16" y="2" width="4" height="4" fill="#c45030"/>
  <rect x="24" y="6" width="4" height="4" fill="#8b3a3a"/>
  <rect x="4" y="12" width="4" height="4" fill="#a04040"/>
  <rect x="12" y="10" width="4" height="4" fill="#c45030"/>
  <rect x="20" y="14" width="4" height="4" fill="#8b3a3a"/>
  <rect x="28" y="12" width="4" height="4" fill="#a04040"/>
  <rect x="2" y="20" width="4" height="4" fill="#c45030"/>
  <rect x="10" y="22" width="4" height="4" fill="#8b3a3a"/>
  <rect x="18" y="18" width="4" height="4" fill="#a04040"/>
  <rect x="26" y="24" width="4" height="4" fill="#c45030"/>
  <rect x="6" y="28" width="4" height="4" fill="#8b3a3a"/>
  <rect x="14" y="26" width="4" height="4" fill="#a04040"/>
  <rect x="22" y="30" width="4" height="4" fill="#c45030"/>
</svg>`;

async function main() {
  const outputPath = path.join(outputDir, 'tile_fire_ground.png');
  await sharp(Buffer.from(svg))
    .png()
    .toFile(outputPath);
  console.log('Generated tile_fire_ground.png');
}

main().catch(console.error);
