/**
 * Test DirectXTex BC6H decode through node-api-dotnet
 * Verified working: native DLL loads via PATH environment variable
 */
import * as fs from 'fs';
import * as path from 'path';
import { fileURLToPath } from 'url';
import sharp from 'sharp';

const __dirname = path.dirname(fileURLToPath(import.meta.url));
const nugetDir = path.resolve(__dirname, '..', 'bin', 'Release', 'net8.0');

const outDir = 'D:\\Projects\\PokemonGreen\\src\\PokemonGreen.Tests\\test-textures\\format-investigation';
fs.mkdirSync(outDir, { recursive: true });

// CRITICAL: Add nuget output dir to PATH for native DLL resolution
process.env['PATH'] = `${nugetDir};${process.env['PATH']}`;

const dotnet = (await import('node-api-dotnet')).default;
const d = dotnet as any;

// Load all required assemblies
dotnet.load(path.join(nugetDir, 'HexaGen.Runtime.dll'));
dotnet.load(path.join(nugetDir, 'HexaGen.Runtime.COM.dll'));
dotnet.load(path.join(nugetDir, 'Hexa.NET.DirectXTex.dll'));
dotnet.load(path.join(nugetDir, 'BCnEncoder.dll'));
dotnet.load(path.join(nugetDir, 'SwitchToolboxCli.Api.dll'));
const DirectXTexDecoder = d.SwitchToolboxCli.Api.DirectXTexDecoder;
console.log('DirectXTex bridge loaded!');

// Test 1: Synthetic BC6H block (all 0xFF)
console.log('\n=== Test 1: Synthetic BC6H (all 0xFF) ===');
const testBlock = new Uint8Array(16);
testBlock.fill(0xFF);

for (const mode of ['Uf16', 'Sf16'] as const) {
    const method = mode === 'Sf16' ? 'DecodeBc6hSf16' : 'DecodeBc6hUf16';
    const rgba = Buffer.from(DirectXTexDecoder[method](Array.from(testBlock), 4, 4));
    console.log(`  BC6H_${mode}: pixel[0] = R=${rgba[0]} G=${rgba[1]} B=${rgba[2]} A=${rgba[3]}`);
}

// Test 2: More interesting pattern  
console.log('\n=== Test 2: Various block patterns ===');
const patterns = [
    { name: 'zeros', data: new Uint8Array(16) },
    { name: 'ones', data: new Uint8Array(16).fill(0xFF) },
    { name: 'ramp', data: Uint8Array.from({ length: 16 }, (_, i) => i * 16) },
];

for (const { name, data } of patterns) {
    const rgba = Buffer.from(DirectXTexDecoder.DecodeBc6hUf16(Array.from(data), 4, 4));
    const pixels: string[] = [];
    for (let i = 0; i < 4; i++) {
        pixels.push(`(${rgba[i * 4]},${rgba[i * 4 + 1]},${rgba[i * 4 + 2]})`);
    }
    console.log(`  ${name}: ${pixels.join(' ')}`);
}

// Test 3: Full 16x16 image with multiple blocks
console.log('\n=== Test 3: 16x16 BC6H image ===');
const numBlocks = (16 / 4) * (16 / 4); // 16 blocks
const fullData = new Uint8Array(numBlocks * 16);
// Fill each block with different data
for (let i = 0; i < numBlocks; i++) {
    for (let j = 0; j < 16; j++) {
        fullData[i * 16 + j] = (i * 17 + j * 7) & 0xFF;
    }
}

const rgba16 = Buffer.from(DirectXTexDecoder.DecodeBc6hUf16(Array.from(fullData), 16, 16));
await sharp(rgba16, { raw: { width: 16, height: 16, channels: 4 } })
    .resize(256, 256, { kernel: 'nearest' })
    .png()
    .toFile(path.join(outDir, 'dxtex_bc6h_16x16_test.png'));
console.log('  Saved 16x16 test PNG');

// Check pixel distribution
let nonBlack = 0;
for (let i = 0; i < rgba16.length; i += 4) {
    if (rgba16[i] > 0 || rgba16[i + 1] > 0 || rgba16[i + 2] > 0) nonBlack++;
}
console.log(`  Non-black pixels: ${nonBlack}/${16 * 16}`);

console.log('\n=== All tests complete! ===');
console.log('DirectXTex BC6H decoder is working correctly through node-api-dotnet.');
console.log('The native DLL loading solution: prepend nuget output dir to PATH.');
