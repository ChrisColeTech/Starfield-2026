import sharp from 'sharp';
import { glob } from 'glob';
import { dirname, basename, join } from 'path';
import { fileURLToPath } from 'url';

const __dirname = dirname(fileURLToPath(import.meta.url));
const rootDir = join(__dirname, '..');
const inputDir = join(rootDir, 'src/Starfield/Content/Tilesets');
const outputDir = join(rootDir, 'Assets/TilesetPreview');

const scale = 4;

const files = await glob('*.png', { cwd: inputDir });

console.log(`Scaling ${files.length} tilesets by ${scale}x...\n`);

for (const file of files) {
    const inputPath = join(inputDir, file);
    const outputPath = join(outputDir, file);
    
    const image = sharp(inputPath);
    const metadata = await image.metadata();
    
    const newWidth = metadata.width * scale;
    const newHeight = metadata.height * scale;
    
    await image
        .resize(newWidth, newHeight, { 
            kernel: 'nearest',
        })
        .toFile(outputPath);
    
    console.log(`✓ ${file}: ${metadata.width}x${metadata.height} → ${newWidth}x${newHeight}`);
}

console.log(`\nDone! Scaled files saved to ${outputDir}`);
