const sharp = require('sharp');
const fs = require('fs');
const path = require('path');

const spriteInputDir = path.resolve(__dirname, '..', 'Assets', 'Sprites');
const spriteOutputDir = path.resolve(__dirname, '..', 'src', 'Starfield.Assets', 'Sprites');
const itemInputDir = path.resolve(__dirname, '..', 'Assets', 'Items');
const itemOutputDir = path.resolve(__dirname, '..', 'src', 'Starfield.Assets', 'Items');

fs.mkdirSync(spriteOutputDir, { recursive: true });
fs.mkdirSync(itemOutputDir, { recursive: true });

// Skip v2 files — their blades are now baked into the main SVGs
const spriteFiles = fs.readdirSync(spriteInputDir)
    .filter(f => f.endsWith('.svg') && !f.includes('_v2'));
const itemFiles = fs.readdirSync(itemInputDir)
    .filter(f => f.endsWith('.svg'));

(async () => {
// Convert sprites
for (const file of spriteFiles) {
    const name = path.basename(file, '.svg');
    const inputPath = path.join(spriteInputDir, file);
    const outputPath = path.join(spriteOutputDir, `${name}.png`);

    try {
        await sharp(inputPath, { density: 144 })
            .png()
            .toFile(outputPath);

        const meta = await sharp(outputPath).metadata();
        console.log(`${name}.png  ${meta.width}x${meta.height}`);
    } catch (err) {
        console.error(`FAIL ${file}: ${err.message}`);
    }
}

// Convert items
for (const file of itemFiles) {
    const name = path.basename(file, '.svg');
    const inputPath = path.join(itemInputDir, file);
    const outputPath = path.join(itemOutputDir, `${name}.png`);

    try {
        await sharp(inputPath, { density: 144 })
            .png()
            .toFile(outputPath);

        const meta = await sharp(outputPath).metadata();
        console.log(`${name}.png  ${meta.width}x${meta.height}`);
    } catch (err) {
        console.error(`FAIL ${file}: ${err.message}`);
    }
}
    console.log(`\nConverted ${files.length} SVGs to ${outputDir}`);
})();
