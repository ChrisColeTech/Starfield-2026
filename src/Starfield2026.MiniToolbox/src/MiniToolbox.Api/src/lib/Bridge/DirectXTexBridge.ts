/**
 * DirectXTex Bridge Loader
 * 
 * Loads the C# DirectXTex bridge assemblies via node-api-dotnet and registers
 * the BC6H decoder with BntxDecoder. Call initDirectXTexBridge() once at startup.
 *
 * Required: SwitchToolboxCli.Api must be built (dotnet build -c Release).
 */
import * as path from 'path';
import * as fs from 'fs';
import { fileURLToPath } from 'url';
import { BntxDecoder } from '../Texture/index.js';

const __dirname = path.dirname(fileURLToPath(import.meta.url));

/**
 * Initialize the DirectXTex BC6H decoder bridge.
 * Loads the required .NET assemblies and registers the decoder with BntxDecoder.
 * Returns true on success, false if the bridge couldn't be loaded (non-fatal).
 */
export async function initDirectXTexBridge(): Promise<boolean> {
    try {
        // Path to the compiled C# assemblies
        // __dirname = .../SwitchToolboxCli.Api/src/lib/Bridge
        // We need .../SwitchToolboxCli.Api/bin/Release/net8.0
        const apiProjectDir = path.resolve(__dirname, '..', '..', '..');
        const nugetDir = path.join(apiProjectDir, 'bin', 'Release', 'net8.0');

        if (!fs.existsSync(path.join(nugetDir, 'SwitchToolboxCli.Api.dll'))) {
            console.warn('[DirectXTexBridge] SwitchToolboxCli.Api.dll not found. Run: dotnet build -c Release');
            console.warn(`  Expected at: ${nugetDir}`);
            return false;
        }

        // CRITICAL: Add nuget output dir to PATH for native DLL resolution
        // The native DirectXTex.dll must be findable via system PATH when loaded from Node.js
        process.env['PATH'] = `${nugetDir};${process.env['PATH']}`;

        const dotnet = (await import('node-api-dotnet')).default;
        const d = dotnet as any;

        // Load assemblies in dependency order
        dotnet.load(path.join(nugetDir, 'HexaGen.Runtime.dll'));
        dotnet.load(path.join(nugetDir, 'HexaGen.Runtime.COM.dll'));
        dotnet.load(path.join(nugetDir, 'Hexa.NET.DirectXTex.dll'));
        dotnet.load(path.join(nugetDir, 'BCnEncoder.dll'));
        dotnet.load(path.join(nugetDir, 'SwitchToolboxCli.Api.dll'));

        const DirectXTexDecoder = d.SwitchToolboxCli.Api.DirectXTexDecoder;
        if (!DirectXTexDecoder) {
            console.warn('[DirectXTexBridge] DirectXTexDecoder class not found in assembly.');
            return false;
        }

        // Register the decoder methods with BntxDecoder
        BntxDecoder.setBc6hDecoder(
            (data: number[], w: number, h: number) => DirectXTexDecoder.DecodeBc6hSf16(data, w, h),
            (data: number[], w: number, h: number) => DirectXTexDecoder.DecodeBc6hUf16(data, w, h)
        );

        return true;
    } catch (ex: any) {
        console.warn(`[DirectXTexBridge] Failed to initialize: ${ex.message}`);
        console.warn('[DirectXTexBridge] BC6H textures will show as magenta placeholders.');
        return false;
    }
}
