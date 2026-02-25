#!/usr/bin/env tsx
/**
 * MCP Server — exposes a render_model tool for headless 3D rendering.
 * Imports cli-render functions directly — no REST calls.
 *
 * Tool: render_model
 *   - inputPath: path to manifest.json or .dae model file
 *   - outputDir: directory to save rendered PNGs
 *   - width / height (optional, default 512)
 *
 * Renders 4 angles: front, 3/4, side, back
 */

// ── Intercept ALL stdout writes and redirect to stderr ──
// The gl package and Three.js write directly to process.stdout,
// bypassing console.log. This corrupts the JSON-RPC stdio protocol.
// We capture stdout.write and route non-JSON-RPC content to stderr.
const _origStdoutWrite = process.stdout.write.bind(process.stdout)
const _stderr = process.stderr.write.bind(process.stderr)
process.stdout.write = (chunk: any, ...args: any[]) => {
    const str = typeof chunk === 'string' ? chunk : chunk.toString()
    // Let JSON-RPC messages through (they start with { or are newlines)
    if (str.startsWith('{') || str === '\n') {
        return (_origStdoutWrite as any)(chunk, ...args)
    }
    // Everything else goes to stderr
    return _stderr(chunk, ...args)
}
console.log = (...a: any[]) => { _stderr(a.join(' ') + '\n') }
console.warn = (...a: any[]) => { _stderr(a.join(' ') + '\n') }
console.error = (...a: any[]) => { _stderr(a.join(' ') + '\n') }

    // ── DOM polyfills (before Three.js loads) ──
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

async function main() {
    // Dynamic imports — guarantees console redirect + polyfills run first
    const { DOMParser } = await import('xmldom')
        ; (globalThis as any).DOMParser = DOMParser

    const { McpServer } = await import('@modelcontextprotocol/sdk/server/mcp.js')
    const { StdioServerTransport } = await import('@modelcontextprotocol/sdk/server/stdio.js')
    const { z } = await import('zod')
    const fs = await import('fs')
    const path = await import('path')
    const THREE = await import('three')
    const {
        createHeadlessRenderer,
        setupCamera,
        setupLighting,
        loadManifestModel,
        loadDaeFromDisk,
        captureAndSave,
    } = await import('./cli-render.js')

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
                if (ext !== '.json' && ext !== '.dae') {
                    return { content: [{ type: 'text' as const, text: `Error: Unsupported file type "${ext}". Provide a .json manifest or .dae model.` }] }
                }

                const result = ext === '.json'
                    ? await loadManifestModel(absInput)
                    : await loadDaeFromDisk(absInput)

                const bbox = new THREE.Box3().setFromObject(result.scene)
                const size = bbox.getSize(new THREE.Vector3())
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

    const BACKEND = 'http://localhost:3001'

    async function checkBackend(): Promise<string | null> {
        try {
            await fetch(`${BACKEND}/api/manifests?dir=test`)
            return null
        } catch {
            return 'Error: BgEditor is not running. Please start it first.'
        }
    }

    async function sendToFrontend(absPath: string, type: 'manifest' | 'dae' | 'folder'): Promise<string | null> {
        const res = await fetch(`${BACKEND}/api/load-model`, {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({ path: absPath, type }),
        })
        if (!res.ok) {
            const err = await res.json().catch(() => ({ error: 'Unknown error' }))
            return `Error: ${(err as any).error}`
        }
        return null
    }

    server.tool(
        'load_model',
        'Load a model into the BgEditor frontend. Accepts a manifest.json path or a .dae model path. Requires the BgEditor to be running.',
        {
            path: z.string().describe('Path to manifest.json or .dae model file'),
        },
        async ({ path: inputPath }) => {
            try {
                const absPath = path.resolve(inputPath)
                if (!fs.existsSync(absPath)) {
                    return { content: [{ type: 'text' as const, text: `Error: File not found: ${absPath}` }] }
                }

                const err = await checkBackend()
                if (err) return { content: [{ type: 'text' as const, text: err }] }

                const ext = path.extname(absPath).toLowerCase()
                let type: 'manifest' | 'dae'

                if (ext === '.json') type = 'manifest'
                else if (ext === '.dae') type = 'dae'
                else return { content: [{ type: 'text' as const, text: `Error: Unsupported file type "${ext}". Provide a .json manifest or .dae model.` }] }

                const sendErr = await sendToFrontend(absPath, type)
                if (sendErr) return { content: [{ type: 'text' as const, text: sendErr }] }

                return { content: [{ type: 'text' as const, text: `✓ Loaded ${path.basename(absPath)} in BgEditor.` }] }
            } catch (err: any) {
                if (err.cause?.code === 'ECONNREFUSED') {
                    return { content: [{ type: 'text' as const, text: 'Error: BgEditor is not running. Please start it first.' }] }
                }
                return { content: [{ type: 'text' as const, text: `Error: ${err.message}` }] }
            }
        },
    )

    server.tool(
        'load_path',
        'Load a folder of models into the BgEditor frontend. Scans the folder for manifests and loads them. Requires the BgEditor to be running.',
        {
            path: z.string().describe('Path to a folder containing model manifests'),
        },
        async ({ path: inputPath }) => {
            try {
                const absPath = path.resolve(inputPath)
                if (!fs.existsSync(absPath) || !fs.statSync(absPath).isDirectory()) {
                    return { content: [{ type: 'text' as const, text: `Error: Not a valid folder: ${absPath}` }] }
                }

                const err = await checkBackend()
                if (err) return { content: [{ type: 'text' as const, text: err }] }

                // Scan for manifests
                const res = await fetch(`${BACKEND}/api/manifests?dir=${encodeURIComponent(absPath)}`)
                const manifests = await res.json() as any[]

                if (manifests.length === 0) {
                    return { content: [{ type: 'text' as const, text: `No manifests found in: ${absPath}` }] }
                }

                const sendErr = await sendToFrontend(absPath, 'folder')
                if (sendErr) return { content: [{ type: 'text' as const, text: sendErr }] }

                const summary = [
                    `✓ Loaded ${manifests.length} manifest(s) in BgEditor:`,
                    ...manifests.map((m: any) => `  • ${m.name}`),
                ].join('\n')

                return { content: [{ type: 'text' as const, text: summary }] }
            } catch (err: any) {
                if (err.cause?.code === 'ECONNREFUSED') {
                    return { content: [{ type: 'text' as const, text: 'Error: BgEditor is not running. Please start it first.' }] }
                }
                return { content: [{ type: 'text' as const, text: `Error: ${err.message}` }] }
            }
        },
    )

    const transport = new StdioServerTransport()
    await server.connect(transport)
}

main().catch(err => {
    _stderr(`MCP server error: ${err}\n`)
    process.exit(1)
})
