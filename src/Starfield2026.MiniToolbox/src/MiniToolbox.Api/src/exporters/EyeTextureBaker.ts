/**
 * Bakes eye albedo textures from EyeClearCoat material parameters.
 * In-game, the EyeClearCoat shader uses BaseColorLayer1-4 + LayerMaskMap
 * to composite the eye color at runtime. Since DAE/Blender don't support
 * this shader, we bake a flat albedo texture from the material data.
 */

import * as fs from 'fs';
import * as path from 'path';
import sharp from 'sharp';
import { BntxDecoder } from '../bntx/BntxDecoder.js';
import type { TrinityMaterial } from '../decoders/TrinityMaterial.js';
import { Vector3 } from '../decoders/Math.js';

export class EyeTextureBaker {
    /**
     * Check if a material uses the EyeClearCoat shader and can be baked.
     */
    public static IsEyeMaterial(material: TrinityMaterial): boolean {
        return material.ShaderName.toLowerCase() === 'eyeclearcoat';
    }

    /**
     * Bake an eye albedo texture and save it.
     * Returns path to baked texture, or null if baking fails.
     */
    public static async BakeEyeTexture(material: TrinityMaterial, tempRoot: string, texOutDir: string): Promise<string | null> {
        if (!EyeTextureBaker.IsEyeMaterial(material)) {
            return null;
        }

        // Find the LayerMaskMap texture
        const lymRef = material.Textures.find(t => t.Name.toLowerCase() === 'layermaskmap');
        if (!lymRef) {
            return null;
        }

        // Find the BNTX file for the layer mask
        const lymBntxPath = EyeTextureBaker.FindBntxFile(lymRef.FilePath, tempRoot);
        if (!lymBntxPath || !fs.existsSync(lymBntxPath)) {
            return null;
        }

        // Decode layer mask texture
        const maskImageResult = await EyeTextureBaker.DecodeBntxToImage(lymBntxPath);
        if (!maskImageResult) {
            return null;
        }

        // Extract material color parameters
        const baseColors = EyeTextureBaker.ExtractBaseColors(material);
        const emissionColors = EyeTextureBaker.ExtractEmissionColors(material);
        const emissionIntensities = EyeTextureBaker.ExtractEmissionIntensities(material);

        // Bake composited texture
        const { width, height, data } = maskImageResult;
        const result = new Uint8Array(width * height * 4);

        for (let y = 0; y < height; y++) {
            for (let x = 0; x < width; x++) {
                const idx = (y * width + x) * 4;
                const maskR = maskImageResult.data[idx] / 255;     // Layer 1
                const maskG = maskImageResult.data[idx + 1] / 255; // Layer 2
                const maskB = maskImageResult.data[idx + 2] / 255; // Layer 3
                const maskA = maskImageResult.data[idx + 3] / 255; // Layer 4

                // Shader compositing formula
                const maskSum = maskR + maskG + maskB + maskA;
                const remainder = Math.max(0, Math.min(1, 1 - maskSum));

                // Blend base colors using mask channels (linear space)
                let color = new Vector3(
                    baseColors[0].x * maskR + baseColors[1].x * maskG + baseColors[2].x * maskB + baseColors[3].x * maskA + remainder,
                    baseColors[0].y * maskR + baseColors[1].y * maskG + baseColors[2].y * maskB + baseColors[3].y * maskA + remainder,
                    baseColors[0].z * maskR + baseColors[1].z * maskG + baseColors[2].z * maskB + baseColors[3].z * maskA + remainder
                );

                // Add emission contribution (these provide the actual visible color in-game)
                const emission = new Vector3(
                    emissionColors[0].x * emissionIntensities[0] * maskR +
                    emissionColors[1].x * emissionIntensities[1] * maskG +
                    emissionColors[2].x * emissionIntensities[2] * maskB +
                    emissionColors[3].x * emissionIntensities[3] * maskA +
                    emissionColors[4].x * emissionIntensities[4] * remainder,
                    emissionColors[0].y * emissionIntensities[0] * maskR +
                    emissionColors[1].y * emissionIntensities[1] * maskG +
                    emissionColors[2].y * emissionIntensities[2] * maskB +
                    emissionColors[3].y * emissionIntensities[3] * maskA +
                    emissionColors[4].y * emissionIntensities[4] * remainder,
                    emissionColors[0].z * emissionIntensities[0] * maskR +
                    emissionColors[1].z * emissionIntensities[1] * maskG +
                    emissionColors[2].z * emissionIntensities[2] * maskB +
                    emissionColors[3].z * emissionIntensities[3] * maskA +
                    emissionColors[4].z * emissionIntensities[4] * remainder
                );

                // Combine: base color + full emission (the game relies on emission for eye visibility)
                color = new Vector3(
                    color.x + emission.x,
                    color.y + emission.y,
                    color.z + emission.z
                );

                // Apply sRGB gamma correction (linear → sRGB) for correct display
                color = new Vector3(
                    EyeTextureBaker.LinearToSrgb(color.x),
                    EyeTextureBaker.LinearToSrgb(color.y),
                    EyeTextureBaker.LinearToSrgb(color.z)
                );

                color = new Vector3(
                    Math.max(0, Math.min(1, color.x)),
                    Math.max(0, Math.min(1, color.y)),
                    Math.max(0, Math.min(1, color.z))
                );

                const resultIdx = (y * width + x) * 4;
                result[resultIdx] = Math.round(color.x * 255);
                result[resultIdx + 1] = Math.round(color.y * 255);
                result[resultIdx + 2] = Math.round(color.z * 255);
                result[resultIdx + 3] = 255;
            }
        }

        // Save the baked texture
        const albFileName = EyeTextureBaker.GetAlbedoFileName(material);
        const outPath = path.join(texOutDir, albFileName);

        // Ensure directory exists
        const outDir = path.dirname(outPath);
        if (!fs.existsSync(outDir)) {
            fs.mkdirSync(outDir, { recursive: true });
        }

        // Save as PNG using sharp
        await sharp(Buffer.from(result), { raw: { width, height, channels: 4 } }).png().toFile(outPath);

        console.log(`  Baked eye texture: ${path.basename(outPath)} (${width}x${height})`);
        return outPath;
    }

    private static ExtractBaseColors(material: TrinityMaterial): Vector3[] {
        const colors: Vector3[] = [];
        for (let i = 0; i < 4; i++) {
            const paramName = `BaseColorLayer${i + 1}`;
            const param = material.Vec4Params.find(p => p.Name.toLowerCase() === paramName.toLowerCase());
            if (param?.Value) {
                // Note: C# uses W, X, Y (ordering might need adjustment)
                colors.push(new Vector3(param.Value.W, param.Value.X, param.Value.Y));
            } else {
                colors.push(Vector3.Zero);
            }
        }
        return colors;
    }

    private static ExtractEmissionColors(material: TrinityMaterial): Vector3[] {
        const colors: Vector3[] = [];
        const names = ['EmissionColorLayer1', 'EmissionColorLayer2', 'EmissionColorLayer3', 'EmissionColorLayer4', 'EmissionColorLayer5'];
        for (let i = 0; i < 5; i++) {
            const param = material.Vec4Params.find(p => p.Name.toLowerCase() === names[i].toLowerCase());
            if (param?.Value) {
                colors.push(new Vector3(param.Value.W, param.Value.X, param.Value.Y));
            } else {
                colors.push(Vector3.Zero);
            }
        }
        return colors;
    }

    private static ExtractEmissionIntensities(material: TrinityMaterial): number[] {
        const intensities: number[] = [];
        const names = ['EmissionIntensityLayer1', 'EmissionIntensityLayer2', 'EmissionIntensityLayer3', 'EmissionIntensityLayer4', 'EmissionIntensityLayer5'];
        for (let i = 0; i < 5; i++) {
            const param = material.FloatParams.find(p => p.Name.toLowerCase() === names[i].toLowerCase());
            if (param) {
                intensities.push(param.Value);
            } else {
                intensities.push(0);
            }
        }
        return intensities;
    }

    private static GetAlbedoFileName(material: TrinityMaterial): string {
        const albRef = material.Textures.find(t => t.Name.toLowerCase() === 'basecolormap');
        if (albRef) {
            const fileName = path.basename(albRef.FilePath, path.extname(albRef.FilePath));
            return `${fileName}.png`;
        }
        return `${material.Name}_eye_baked.png`;
    }

    private static FindBntxFile(referencePath: string, tempRoot: string): string | null {
        // The reference path from the material might be relative or absolute
        // Try to find the .bntx file in the temp extraction directory
        let fileName = path.basename(referencePath);
        if (!fileName.toLowerCase().endsWith('.bntx')) {
            fileName += '.bntx';
        }

        // Recursively search for the file
        function searchDir(dir: string): string | null {
            const entries = fs.readdirSync(dir, { withFileTypes: true });
            for (const entry of entries) {
                const fullPath = path.join(dir, entry.name);
                if (entry.isDirectory()) {
                    const result = searchDir(fullPath);
                    if (result) return result;
                } else if (entry.name.toLowerCase() === fileName.toLowerCase()) {
                    return fullPath;
                }
            }
            return null;
        }

        try {
            return searchDir(tempRoot);
        } catch {
            return null;
        }
    }

    private static async DecodeBntxToImage(bntxPath: string): Promise<{ width: number; height: number; data: Uint8Array } | null> {
        try {
            const textures = BntxDecoder.decodeFile(bntxPath);
            if (!textures || textures.length === 0) {
                return null;
            }

            const tex = textures[0];
            return { width: tex.width, height: tex.height, data: new Uint8Array(tex.rgbaData) };
        } catch (ex) {
            console.warn(`BNTX decoding failed for ${bntxPath}: ${ex}`);
            return null;
        }
    }

    /**
     * Convert linear-space value to sRGB gamma.
     * The standard sRGB transfer function.
     */
    private static LinearToSrgb(linear: number): number {
        if (linear <= 0.0031308) {
            return linear * 12.92;
        }
        return 1.055 * Math.pow(linear, 1.0 / 2.4) - 0.055;
    }
}