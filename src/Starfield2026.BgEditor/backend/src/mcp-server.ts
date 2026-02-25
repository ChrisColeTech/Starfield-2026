#!/usr/bin/env tsx
/**
 * MCP Server — exposes a render_model tool for headless 3D rendering.
 *
 * Usage:
 *   npx tsx src/mcp-server.ts
 *
 * Tool: render_model
 *   - inputPath: path to manifest.json or .dae model file
 *   - outputDir: directory to save rendered PNGs
 *   - width (optional): image width (default: 512)
 *   - height (optional): image height (default: 512)
 *
 * Renders 4 angles: front, 3/4, side, back
 */

// DOM polyfills — must execute before Three.js is imported
; (globalThis as any).document = {
    createElementNS: (_ns: string, tag: string) => {
        if (tag === 'canvas') return { style: {}, width: 0, height: 0, addEventListener: () => { }, removeEventListener: () => { }, getContext: () => null }
        if (tag === 'img') return { style: {}, addEventListener: () => { }, removeEventListener: () => { } }
        return { style: {} }
    },
    createElement: () => ({ style: {} }),
}
    ; (globalThis as any).window = globalThis
    ; (globalThis as any).self = globalThis
    ; (globalThis as any).requestAnimationFrame = (cb: Function) => setTimeout(cb, 16)
    ; (globalThis as any).cancelAnimationFrame = (id: number) => clearTimeout(id)
try { Object.defineProperty(globalThis, 'navigator', { value: { userAgent: 'node', platform: 'Win32' }, writable: true, configurable: true }) } catch { }
try { (globalThis as any).HTMLCanvasElement = class { } } catch { }
try { (globalThis as any).OffscreenCanvas = class { } } catch { }
import { DOMParser } from 'xmldom'
    ; (globalThis as any).DOMParser = DOMParser

import { McpServer } from '@modelcontextprotocol/sdk/server/mcp.js'
import { StdioServerTransport } from '@modelcontextprotocol/sdk/server/stdio.js'
import { z } from 'zod'
import fs from 'fs'
import path from 'path'
import * as THREE from 'three'
import {
    createHeadlessRenderer,
    setupCamera,
    setupLighting,
    loadManifestModel,
    loadDaeFromDisk,
    captureAndSave,
} from './cli-render.js'

const server = new McpServer({
    name: 'starfield-renderer',
    version: '1.0.0',
})

server.tool(
    'render_model',
    'Render a 3D model from multiple angles. Accepts a manifest.json or .dae file path, renders front/3-4/side/back views as PNGs.',
    {
        inputPath: z.string().describe('Path to manifest.json or .dae model file'),
        outputDir: z.string().describe('Directory to save rendered PNG files'),
        width: z.number().optional().default(512).describe('Image width in pixels (default: 512)'),
        height: z.number().optional().default(512).describe('Image height in pixels (default: 512)'),
    },
    async ({ inputPath, outputDir, width, height }) => {
        try {
            const absInput = path.resolve(inputPath)
            if (!fs.existsSync(absInput)) {
                return { content: [{ type: 'text' as const, text: `Error: File not found: ${absInput}` }] }
            }

            const ext = path.extname(absInput).toLowerCase()
            const isManifest = ext === '.json'
            const isDae = ext === '.dae'

            if (!isManifest && !isDae) {
                return { content: [{ type: 'text' as const, text: `Error: Unsupported file type "${ext}". Provide a .json manifest or .dae model.` }] }
            }

            // Load model
            const result = isManifest
                ? await loadManifestModel(absInput)
                : await loadDaeFromDisk(absInput)

            // Compute bounds
            const bbox = new THREE.Box3().setFromObject(result.scene)
            const size = bbox.getSize(new THREE.Vector3())

            // Create renderer
            const { renderer, glCtx } = createHeadlessRenderer(width, height, '1a1a2e')
            const renderScene = new THREE.Scene()
            renderScene.add(result.scene)
            setupLighting(renderScene, 'studio')

            fs.mkdirSync(outputDir, { recursive: true })

            const angles = ['front', '3/4', 'side', 'back'] as const
            const saved: string[] = []

            for (const angle of angles) {
                const camera = setupCamera(angle, bbox, width, height, 1.0, 0)
                renderer.render(renderScene, camera)
                const outPath = path.join(outputDir, `${angle.replace('/', '-')}.png`)
                captureAndSave(renderer, glCtx, width, height, outPath)
                saved.push(outPath)
            }

            renderer.dispose()

            const summary = [
                `✓ Rendered ${saved.length} angles for ${path.basename(absInput)}`,
                `  Model bounds: ${size.x.toFixed(2)} × ${size.y.toFixed(2)} × ${size.z.toFixed(2)}`,
                `  Resolution: ${width}×${height}`,
                `  Output: ${outputDir}`,
                ...saved.map(f => `    ${path.basename(f)}`),
            ].join('\n')

            return { content: [{ type: 'text' as const, text: summary }] }
        } catch (err: any) {
            return { content: [{ type: 'text' as const, text: `Error: ${err.message}` }] }
        }
    },
)

async function main() {
    const transport = new StdioServerTransport()
    await server.connect(transport)
}

main().catch(err => {
    console.error('MCP server error:', err)
    process.exit(1)
})
