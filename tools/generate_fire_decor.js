const sharp = require('sharp');
const path = require('path');

const outputDir = path.resolve(__dirname, '..', 'src', 'Starfield.Assets', 'Sprites');

const sprites = {
  'tile_fire_rock': `<svg xmlns="http://www.w3.org/2000/svg" width="32" height="32">
  <ellipse cx="16" cy="20" rx="10" ry="8" fill="#4a3030"/>
  <ellipse cx="16" cy="18" rx="8" ry="6" fill="#6b4040"/>
  <ellipse cx="14" cy="16" rx="5" ry="3" fill="#8b5050"/>
  <circle cx="12" cy="15" r="2" fill="#ff6600"/>
  <circle cx="17" cy="17" r="1.5" fill="#ff3300"/>
</svg>`,

  'tile_fire_boulder': `<svg xmlns="http://www.w3.org/2000/svg" width="32" height="32">
  <ellipse cx="16" cy="20" rx="12" ry="8" fill="#3a2828"/>
  <ellipse cx="16" cy="18" rx="10" ry="6" fill="#5a3838"/>
  <ellipse cx="16" cy="16" rx="8" ry="5" fill="#7a4848"/>
  <ellipse cx="14" cy="14" rx="5" ry="3" fill="#9a5858"/>
  <circle cx="12" cy="13" r="2.5" fill="#ff6600"/>
  <circle cx="18" cy="15" r="2" fill="#ff3300"/>
  <circle cx="15" cy="16" r="1.5" fill="#ff9900"/>
</svg>`,

  'tile_lava_splatter': `<svg xmlns="http://www.w3.org/2000/svg" width="32" height="32">
  <ellipse cx="16" cy="20" rx="14" ry="8" fill="#cc2200"/>
  <ellipse cx="12" cy="16" rx="8" ry="5" fill="#ff4400"/>
  <ellipse cx="20" cy="18" rx="6" ry="4" fill="#ff6600"/>
  <ellipse cx="16" cy="14" rx="4" ry="3" fill="#ff9900"/>
  <circle cx="14" cy="12" r="2" fill="#ffcc00"/>
  <circle cx="18" cy="13" r="1.5" fill="#ffff66"/>
</svg>`,
};

async function main() {
  for (const [name, svg] of Object.entries(sprites)) {
    const outputPath = path.join(outputDir, `${name}.png`);
    await sharp(Buffer.from(svg))
      .png()
      .toFile(outputPath);
    console.log(`Generated ${name}.png`);
  }
  console.log('\nDone!');
}

main().catch(console.error);
