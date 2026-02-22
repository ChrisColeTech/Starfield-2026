/**
 * Full export test: Extracts pm0025 (Pikachu) and exports to DAE.
 * Run: npx tsx src/test_export.ts
 */
import { main } from './cli.js';

// Override process.argv
process.argv = [
    'node', 'cli.ts',
    '--arc', 'D:\\Projects\\PokemonGreen\\src\\PokemonGreen.Tests\\scarlet-dump\\arc',
    '--model', 'pokemon/data/pm0025/pm0025_00_00/pm0025_00_00.trmdl',
    '--output', 'D:\\Projects\\PokemonGreen\\src\\PokemonGreen.Tests\\scarlet-dump\\exported'
];

console.log('Starting full export test...');
console.log('Args:', process.argv.slice(2).join(' '));

try {
    const exitCode = await main();
    console.log(`\nExit code: ${exitCode}`);
    process.exit(exitCode);
} catch (err: any) {
    console.error('FATAL ERROR:', err.message);
    console.error(err.stack);
    process.exit(1);
}
