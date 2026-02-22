import sharp from 'sharp';
import { glob } from 'glob';
import { dirname, basename, join } from 'path';
import { fileURLToPath } from 'url';

const __dirname = dirname(fileURLToPath(import.meta.url));
const rootDir = join(__dirname, '..');
const itemsDir = join(rootDir, 'Assets', 'Items');
const outputDir = join(rootDir, 'src', 'Starfield.Assets', 'Items');

const files = await glob('*.svg', { cwd: itemsDir });

console.log(`Found ${files.length} SVG files to convert...\n`);

for (const file of files) {
    const inputPath = join(itemsDir, file);
    const outputPath = join(outputDir, file.replace('.svg', '.png'));
    
    try {
        await sharp(inputPath)
            .resize(32, 32)
            .png()
            .toFile(outputPath);
        console.log(`✓ ${basename(file)}`);
    } catch (err) {
        console.error(`✗ ${basename(file)}: ${err.message}`);
    }
}

console.log(`\nDone! Converted ${files.length} files to ${outputDir}`);
