#!/usr/bin/env tsx
/**
 * CLI renderer — loads a manifest or DAE model and renders a PNG snapshot.
 *
 * Usage:
 *   npx tsx src/cli-render.ts --manifest <path/to/manifest.json> [options]
 *   npx tsx src/cli-render.ts --model <path/to/model.dae> [options]
 *
 * Options:
 *   --manifest <path>     Path to manifest.json
 *   --model <path>        Path to .dae model file (alternative to --manifest)
 *   --output <path>       Output PNG path (default: render.png)
 *   --width <n>           Image width  (default: 1920)
 *   --height <n>          Image height (default: 1080)
 *   --bg <hex>            Background color (default: 1a1a2e)
 *   --camera <preset>     Camera preset: front|back|left|right|top|3/4 (default: 3/4)
 *   --distance <n>        Camera distance multiplier (default: 1.0)
 *   --lighting <preset>   Lighting: studio|flat|dramatic (default: studio)
 *   --rotate <degrees>    Rotate model Y-axis (default: 0)
 *   --clip <file>         Clip DAE to bake (seeks to frame 0)
 *   --frame <n>           Seek to frame N in the clip (default: 0)
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

import fs from 'fs'
import path from 'path'
import createContext from 'gl'
import { PNG } from 'pngjs'
import * as THREE from 'three'
import { ColladaLoader } from 'three/examples/jsm/loaders/ColladaLoader.js'

// ─────────────────────────── Arg parsing ───────────────────────────

function parseArgs(): Record<string, string> {
    const args: Record<string, string> = {}
    const argv = process.argv.slice(2)
    for (let i = 0; i < argv.length; i++) {
        if (argv[i].startsWith('--') && i + 1 < argv.length) {
            args[argv[i].slice(2)] = argv[++i]
        }
    }
    return args
}

// ─────────────────────────── WebGL context ───────────────────────────

export function createHeadlessRenderer(width: number, height: number, bgColor: string) {
    const glCtx = createContext(width, height, { preserveDrawingBuffer: true }) as any
    if (!glCtx) throw new Error('Failed to create headless GL context')

    // Stub WebGL2 methods (gl package is WebGL1 only, Three.js probes for these)
    const stubs = ['texImage3D', 'texSubImage3D', 'texStorage2D', 'texStorage3D',
        'createVertexArray', 'bindVertexArray', 'deleteVertexArray',
        'drawArraysInstanced', 'drawElementsInstanced', 'vertexAttribDivisor',
        'drawBuffers', 'readBuffer', 'getInternalformatParameter',
        'renderbufferStorageMultisample', 'blitFramebuffer', 'invalidateFramebuffer']
    for (const fn of stubs) if (!glCtx[fn]) glCtx[fn] = () => { }
    const consts: Record<string, number> = { TEXTURE_3D: 0x806F, TEXTURE_2D_ARRAY: 0x8C1A, RGBA8: 0x8058, READ_FRAMEBUFFER: 0x8CA8, DRAW_FRAMEBUFFER: 0x8CA9, UNIFORM_BUFFER: 0x8A11 }
    for (const [k, v] of Object.entries(consts)) if (!glCtx[k]) glCtx[k] = v

    const renderer = new THREE.WebGLRenderer({
        context: glCtx as any,
        antialias: true,
        preserveDrawingBuffer: true,
        alpha: false,
    })
    renderer.setSize(width, height)
    renderer.setClearColor(new THREE.Color('#' + bgColor), 1)
    try { (renderer as any).outputEncoding = THREE.sRGBEncoding } catch { }
    try { renderer.outputColorSpace = THREE.SRGBColorSpace } catch { }

    return { renderer, glCtx }
}

// ─────────────────────────── Camera presets ───────────────────────────

export function setupCamera(
    preset: string,
    bbox: THREE.Box3,
    width: number,
    height: number,
    distanceMul: number,
    rotateY: number,
) {
    const center = bbox.getCenter(new THREE.Vector3())
    const size = bbox.getSize(new THREE.Vector3())
    const maxDim = Math.max(size.x, size.y, size.z)
    const baseDist = maxDim * 2.0 * distanceMul

    const camera = new THREE.PerspectiveCamera(45, width / height, 0.01, 1000)

    const angles: Record<string, [number, number]> = {
        'front': [0, 0],
        'back': [Math.PI, 0],
        'left': [-Math.PI / 2, 0],
        'right': [Math.PI / 2, 0],
        'top': [0, Math.PI / 2],
        '3/4': [Math.PI / 5, Math.PI / 8],
    }

    const [yaw, pitch] = angles[preset] ?? angles['3/4']
    const totalYaw = yaw + (rotateY * Math.PI / 180)

    camera.position.set(
        center.x + baseDist * Math.sin(totalYaw) * Math.cos(pitch),
        center.y + baseDist * Math.sin(pitch),
        center.z + baseDist * Math.cos(totalYaw) * Math.cos(pitch),
    )
    camera.lookAt(center)

    return camera
}

// ─────────────────────────── Lighting presets ───────────────────────────

export function setupLighting(scene: THREE.Scene, preset: string) {
    const ambient = new THREE.AmbientLight()
    const key = new THREE.DirectionalLight()
    const fill = new THREE.DirectionalLight()

    switch (preset) {
        case 'flat':
            ambient.intensity = 1.2
            ambient.color.set(0xffffff)
            key.intensity = 0.3
            key.position.set(0, 10, 5)
            fill.intensity = 0.0
            break

        case 'dramatic':
            ambient.intensity = 0.15
            ambient.color.set(0x2233aa)
            key.intensity = 2.0
            key.color.set(0xffeedd)
            key.position.set(3, 8, 2)
            fill.intensity = 0.3
            fill.color.set(0x4466ff)
            fill.position.set(-3, 2, -4)
            break

        case 'studio':
        default:
            ambient.intensity = 0.5
            ambient.color.set(0xffffff)
            key.intensity = 1.0
            key.color.set(0xffeedd)
            key.position.set(5, 10, 7)
            fill.intensity = 0.4
            fill.color.set(0xaaccff)
            fill.position.set(-5, 3, -3)
            break
    }

    scene.add(ambient, key, fill)
}

// ─────────────────────────── Model loading ───────────────────────────

import sharp from 'sharp'

async function decodeTexturesFromDae(
    text: string,
    basePath: string,
): Promise<Map<string, THREE.DataTexture>> {
    const xmlDoc = new DOMParser().parseFromString(text, 'text/xml')
    const imageMap = new Map<string, string>() // image id → file path
    const xmlImages = xmlDoc.getElementsByTagName('image')
    for (let i = 0; i < xmlImages.length; i++) {
        const img = xmlImages[i]
        const id = img.getAttribute('id') || ''
        const initFrom = img.getElementsByTagName('init_from')[0]
        if (initFrom?.textContent) {
            const raw = initFrom.textContent.trim()
            if (raw.includes('/') || raw.includes('.')) {
                imageMap.set(id, raw)
            }
        }
    }

    const decodedTextures = new Map<string, THREE.DataTexture>()

    async function decodeTex(absImg: string, key: string) {
        try {
            const { data, info } = await sharp(absImg)
                .ensureAlpha()
                .raw()
                .toBuffer({ resolveWithObject: true })

            const tex = new THREE.DataTexture(
                new Uint8Array(data), info.width, info.height,
                THREE.RGBAFormat, THREE.UnsignedByteType,
            )
            tex.flipY = true
            try { (tex as any).encoding = (THREE as any).sRGBEncoding } catch { }
            try { tex.colorSpace = THREE.SRGBColorSpace } catch { }
            tex.needsUpdate = true
            decodedTextures.set(key, tex)
            decodedTextures.set(path.basename(absImg, path.extname(absImg)), tex)
        } catch (err: any) {
            console.warn(`  ⚠ Decode failed: ${path.basename(absImg)}: ${err.message}`)
        }
    }

    // Decode images referenced in DAE XML
    for (const [id, relPath] of imageMap) {
        const absImg = path.resolve(basePath, relPath).replace(/\\/g, '/')
        if (!fs.existsSync(absImg)) {
            console.warn(`  ⚠ Missing: ${relPath}`)
            continue
        }
        await decodeTex(absImg, id)
    }

    // Fallback: if no images in DAE, scan textures/ folder
    if (imageMap.size === 0) {
        const texDir = path.join(basePath, 'textures')
        if (fs.existsSync(texDir)) {
            const texFiles = fs.readdirSync(texDir)
                .filter((f: string) => /\.png$/i.test(f))
                .filter((f: string) => !/(nor|nrm|mask|msk|ao|_1)\./i.test(f))
            for (const f of texFiles) {
                const absImg = path.join(texDir, f).replace(/\\/g, '/')
                const key = path.basename(f, path.extname(f))
                await decodeTex(absImg, key)
            }
        }
    }

    return decodedTextures
}

export async function loadDaeFromDisk(daeFilePath: string): Promise<{ scene: THREE.Group; animations: THREE.AnimationClip[] }> {
    const absPath = path.resolve(daeFilePath)
    if (!fs.existsSync(absPath)) throw new Error(`File not found: ${absPath}`)

    const text = fs.readFileSync(absPath, 'utf-8')
    const basePath = path.dirname(absPath).replace(/\\/g, '/') + '/'

    const loader = new ColladaLoader()
    const collada = loader.parse(text, basePath)
    const scene = collada.scene as unknown as THREE.Group
    const animations = (collada as any).animations || collada.scene.animations || []

    // Decode textures with sharp and apply to materials
    const decodedTextures = await decodeTexturesFromDae(text, basePath)
    console.log(`  ${Math.floor(decodedTextures.size / 2)} textures decoded`)

    applyDecodedTextures(scene, decodedTextures)

    return { scene, animations }
}

export async function loadManifestModel(manifestPath: string, clipFile?: string): Promise<{ scene: THREE.Group; animations: THREE.AnimationClip[] }> {
    const absManifest = path.resolve(manifestPath)
    if (!fs.existsSync(absManifest)) throw new Error(`Manifest not found: ${absManifest}`)

    const dir = path.dirname(absManifest)
    const manifest = JSON.parse(fs.readFileSync(absManifest, 'utf-8'))
    const modelFile = manifest.modelFile || manifest.model || 'model.dae'
    const modelPath = path.join(dir, modelFile)

    // If clip specified, bake it into the model
    if (clipFile) {
        const clipPath = path.join(dir, clipFile)
        if (!fs.existsSync(clipPath)) throw new Error(`Clip not found: ${clipPath}`)

        let modelXml = fs.readFileSync(modelPath, 'utf-8')
        const clipXml = fs.readFileSync(clipPath, 'utf-8')

        const animStart = clipXml.indexOf('<library_animations')
        const animEnd = clipXml.indexOf('</library_animations>')
        if (animStart >= 0 && animEnd >= 0) {
            const animBlock = clipXml.substring(animStart, animEnd + '</library_animations>'.length)

            const existStart = modelXml.indexOf('<library_animations')
            if (existStart >= 0) {
                const existEnd = modelXml.indexOf('</library_animations>', existStart)
                if (existEnd >= 0) {
                    modelXml = modelXml.substring(0, existStart) + modelXml.substring(existEnd + '</library_animations>'.length)
                }
            }

            const insertIdx = modelXml.lastIndexOf('</COLLADA>')
            if (insertIdx >= 0) {
                modelXml = modelXml.substring(0, insertIdx) + '\n' + animBlock + '\n' + modelXml.substring(insertIdx)
            }
        }

        const tmpPath = path.join(dir, '__baked_temp.dae')
        fs.writeFileSync(tmpPath, modelXml)
        try {
            return await loadDaeFromDisk(tmpPath)
        } finally {
            fs.unlinkSync(tmpPath)
        }
    }

    return await loadDaeFromDisk(modelPath)
}

// ─────────────────────────── Apply decoded textures to materials ───────────────────────────

function applyDecodedTextures(scene: THREE.Group, decodedTextures: Map<string, THREE.DataTexture>) {
    scene.traverse((node: any) => {
        if (!node.isMesh) return
        const mats = Array.isArray(node.material) ? node.material : [node.material]

        node.material = mats.map((mat: any) => {
            let decoded: THREE.DataTexture | undefined
            const matName = mat.name || '(unnamed)'

            // 1. Match by material name against image IDs/paths
            const matBase = mat.name.toLowerCase()
                .replace(/^[lr]_/, '')
                .replace(/_skin$/, '')
                .replace(/_default$/, '')

            for (const [key, tex] of decodedTextures) {
                const keyLower = key.toLowerCase()
                const keyCore = keyLower
                    .replace(/^image_/, '')
                    .replace(/^pm\d+_\d+_/, '')
                    .replace(/tr\d+_\d+_/, '')
                    .replace(/_\d+_alb$/, '')
                    .replace(/_alb$/, '')
                    .replace(/\d+\.tga.*$/, '')

                if (keyCore && matBase && (
                    keyCore === matBase ||
                    matBase.startsWith(keyCore) ||
                    keyCore.startsWith(matBase) ||
                    keyLower.includes(matBase) ||
                    matBase.includes(keyCore)
                )) {
                    decoded = tex
                    break
                }
            }

            console.log(`    mesh=${node.name} mat=${matName} → ${decoded ? '✓ textured' : '✗ fallback'}`)

            const isEye = /eye/i.test(mat.name) && !/eyebrow/i.test(mat.name)

            if (decoded) {
                if (isEye) {
                    return new THREE.MeshBasicMaterial({ map: decoded, side: THREE.DoubleSide })
                }
                return new THREE.MeshPhongMaterial({ map: decoded, side: THREE.DoubleSide, specular: 0x000000 })
            }
            return new THREE.MeshPhongMaterial({ color: 0xcccccc, side: THREE.DoubleSide, specular: 0x000000 })
        })

        if (Array.isArray(node.material) && node.material.length === 1)
            node.material = node.material[0]
    })
}

export function fixMaterialsForRender(_scene: THREE.Group) {
    // Materials are now handled by applyDecodedTextures during loading — this is a no-op
}

// ─────────────────────────── Readback + save PNG ───────────────────────────

export function captureAndSave(renderer: THREE.WebGLRenderer, glCtx: any, width: number, height: number, outputPath: string) {
    const pixels = new Uint8Array(width * height * 4)
    glCtx.readPixels(0, 0, width, height, glCtx.RGBA, glCtx.UNSIGNED_BYTE, pixels)

    // Flip vertically (GL origin is bottom-left)
    const rowSize = width * 4
    const flipped = new Uint8Array(width * height * 4)
    for (let y = 0; y < height; y++) {
        const srcRow = (height - 1 - y) * rowSize
        const dstRow = y * rowSize
        flipped.set(pixels.subarray(srcRow, srcRow + rowSize), dstRow)
    }

    const png = new PNG({ width, height })
    png.data = Buffer.from(flipped)
    const buffer = PNG.sync.write(png)
    fs.writeFileSync(outputPath, buffer)
    console.log(`✓ Saved ${width}×${height} render to ${outputPath}`)
}

// ─────────────────────────── Main ───────────────────────────

async function main() {
    const args = parseArgs()

    if (!args.manifest && !args.model) {
        console.error('Usage: npx tsx src/cli-render.ts --manifest <path> [--output <path>] [--width N] [--height N]')
        console.error('       npx tsx src/cli-render.ts --model <path.dae> [options]')
        console.error('')
        console.error('Options:')
        console.error('  --manifest <path>     Path to manifest.json')
        console.error('  --model <path>        Path to .dae file')
        console.error('  --output <path>       Output PNG (default: render.png)')
        console.error('  --width <n>           Width (default: 1920)')
        console.error('  --height <n>          Height (default: 1080)')
        console.error('  --bg <hex>            Background color (default: 1a1a2e)')
        console.error('  --camera <preset>     front|back|left|right|top|3/4 (default: 3/4)')
        console.error('  --distance <n>        Camera distance multiplier (default: 1.0)')
        console.error('  --lighting <preset>   studio|flat|dramatic (default: studio)')
        console.error('  --rotate <degrees>    Rotate model Y-axis (default: 0)')
        console.error('  --clip <file>         Clip DAE to bake into model')
        console.error('  --frame <n>           Animation frame to render (default: 0)')
        process.exit(1)
    }

    const width = parseInt(args.width || '1920', 10)
    const height = parseInt(args.height || '1080', 10)
    const bgColor = args.bg || '1a1a2e'
    const cameraPreset = args.camera || '3/4'
    const distanceMul = parseFloat(args.distance || '1.0')
    const lightingPreset = args.lighting || 'studio'
    const rotateY = parseFloat(args.rotate || '0')
    const outputPath = args.output || 'render.png'
    const clipFile = args.clip
    const frame = parseInt(args.frame || '0', 10)

    console.log(`Loading model...`)

    // Load scene
    let scene: THREE.Group
    let animations: THREE.AnimationClip[]

    if (args.manifest) {
        const result = await loadManifestModel(args.manifest, clipFile)
        scene = result.scene
        animations = result.animations
    } else {
        const result = await loadDaeFromDisk(args.model)
        scene = result.scene
        animations = result.animations
    }

    fixMaterialsForRender(scene)

    // Apply animation frame
    if (animations.length > 0 && frame >= 0) {
        const mixer = new THREE.AnimationMixer(scene)
        const action = mixer.clipAction(animations[0])
        action.play()
        const clip = animations[0]
        const frameTime = clip.duration > 0 ? (frame / 30) : 0 // assume 30fps
        mixer.update(Math.min(frameTime, clip.duration))
        console.log(`  Applied animation frame ${frame} (t=${frameTime.toFixed(3)}s, clip duration=${clip.duration.toFixed(3)}s, ${clip.tracks.length} tracks)`)
    }

    // Compute bounds
    const bbox = new THREE.Box3().setFromObject(scene)
    const size = bbox.getSize(new THREE.Vector3())
    console.log(`  Model bounds: ${size.x.toFixed(2)} × ${size.y.toFixed(2)} × ${size.z.toFixed(2)}`)

    // Setup Three.js
    console.log(`Rendering ${width}×${height} (camera=${cameraPreset}, lighting=${lightingPreset})...`)
    const { renderer, glCtx } = createHeadlessRenderer(width, height, bgColor)

    const renderScene = new THREE.Scene()
    renderScene.add(scene)
    setupLighting(renderScene, lightingPreset)

    const camera = setupCamera(cameraPreset, bbox, width, height, distanceMul, rotateY)

    // Render
    renderer.render(renderScene, camera)

    // Save
    captureAndSave(renderer, glCtx, width, height, outputPath)

    // Cleanup
    renderer.dispose()
    process.exit(0)
}

// Only run main() when executed directly (not when imported)
const isDirectRun = process.argv[1]?.replace(/\\/g, '/').endsWith('cli-render.ts')
    || process.argv[1]?.replace(/\\/g, '/').endsWith('cli-render.js')
if (isDirectRun) {
    main().catch(err => {
        console.error('Error:', err.message)
        process.exit(1)
    })
}
