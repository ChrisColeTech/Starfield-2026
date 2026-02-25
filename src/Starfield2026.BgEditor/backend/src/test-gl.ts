/**
 * CLI Renderer — loads a DAE model and renders multiple camera angles.
 *
 * Usage: npx tsx src/test-gl.ts <path-to-model.dae>
 *
 * Output: outputs/<parent-folder>/front.png, 3-4.png, side.png, back.png
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

const OUTPUT_ROOT = 'D:/Projects/Starfield-2026/src/Starfield2026.BgEditor/backend/outputs'

async function main() {
    const createContext = (await import('gl')).default
    const THREE = await import('three')
    const { ColladaLoader } = await import('three/examples/jsm/loaders/ColladaLoader.js')
    const { PNG } = await import('pngjs')
    const fs = await import('fs')
    const path = await import('path')
    const sharp = (await import('sharp')).default

    const daePath = process.argv[2]
    if (!daePath) {
        console.error('Usage: npx tsx src/test-gl.ts <model.dae>')
        process.exit(1)
    }

    const absPath = path.resolve(daePath)
    if (!fs.existsSync(absPath)) {
        console.error(`File not found: ${absPath}`)
        process.exit(1)
    }

    // Output folder: outputs/<parent-folder-name>/
    const parentFolder = path.basename(path.dirname(absPath))
    const outDir = path.join(OUTPUT_ROOT, parentFolder).replace(/\\/g, '/')
    fs.mkdirSync(outDir, { recursive: true })

    const W = 512, H = 512

    // --- GL context + WebGL2 stubs ---
    const glCtx = createContext(W, H, { preserveDrawingBuffer: true }) as any
    if (!glCtx) { console.error('Failed to create GL context'); process.exit(1) }
    const stubs = ['texImage3D', 'texSubImage3D', 'texStorage2D', 'texStorage3D',
        'createVertexArray', 'bindVertexArray', 'deleteVertexArray',
        'drawArraysInstanced', 'drawElementsInstanced', 'vertexAttribDivisor',
        'drawBuffers', 'readBuffer', 'getInternalformatParameter',
        'renderbufferStorageMultisample', 'blitFramebuffer', 'invalidateFramebuffer']
    for (const fn of stubs) if (!glCtx[fn]) glCtx[fn] = () => { }
    if (!glCtx.createVertexArray) glCtx.createVertexArray = () => ({})
    const consts: Record<string, number> = { TEXTURE_3D: 0x806F, TEXTURE_2D_ARRAY: 0x8C1A, RGBA8: 0x8058, READ_FRAMEBUFFER: 0x8CA8, DRAW_FRAMEBUFFER: 0x8CA9, UNIFORM_BUFFER: 0x8A11 }
    for (const [k, v] of Object.entries(consts)) if (!glCtx[k]) glCtx[k] = v

    // --- Renderer (sRGB output) ---
    const renderer = new THREE.WebGLRenderer({ context: glCtx as any, antialias: false, preserveDrawingBuffer: true })
    renderer.setSize(W, H)
    renderer.setClearColor(0x1a1a2e, 1)
    try { (renderer as any).outputEncoding = THREE.sRGBEncoding } catch { }
    try { renderer.outputColorSpace = THREE.SRGBColorSpace } catch { }

    // --- Load DAE ---
    console.log(`Loading: ${parentFolder}/${path.basename(absPath)}`)
    const text = fs.readFileSync(absPath, 'utf-8')
    const basePath = path.dirname(absPath).replace(/\\/g, '/') + '/'

    const loader = new ColladaLoader()
    const collada = loader.parse(text, basePath)
    const scene = collada.scene as unknown as THREE.Group

    // --- Pre-decode ALL referenced images ---
    const imageMap = new Map<string, string>()  // image id → file path
    const xmlDoc = new DOMParser().parseFromString(text, 'text/xml')
    const xmlImages = xmlDoc.getElementsByTagName('image')
    for (let i = 0; i < xmlImages.length; i++) {
        const img = xmlImages[i]
        const id = img.getAttribute('id') || ''
        const initFrom = img.getElementsByTagName('init_from')[0]
        if (initFrom?.textContent) {
            const raw = initFrom.textContent.trim()
            // Only take file paths (skip references to other image IDs)
            if (raw.includes('/') || raw.includes('.')) {
                imageMap.set(id, raw)
            }
        }
    }

    // Decode every texture file with sharp
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
            try { (tex as any).encoding = THREE.sRGBEncoding } catch { }
            try { tex.colorSpace = THREE.SRGBColorSpace } catch { }
            tex.needsUpdate = true
            decodedTextures.set(key, tex)
            // Also key by base filename
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
                .filter((f: string) => !/(nor|nrm|mask|msk|ao|_1)\./i.test(f))  // skip normal/mask/ao/alt
            for (const f of texFiles) {
                const absImg = path.join(texDir, f).replace(/\\/g, '/')
                const key = path.basename(f, path.extname(f))
                await decodeTex(absImg, key)
            }
        }
    }

    console.log(`  ${Math.floor(decodedTextures.size / 2)} textures decoded`)

    // --- Apply decoded textures to meshes ---
    let meshCount = 0
    let texApplied = 0

    scene.traverse((node: any) => {
        if (!node.isMesh) return
        meshCount++
        const mats = Array.isArray(node.material) ? node.material : [node.material]

        node.material = mats.map((mat: any) => {
            let decoded: THREE.DataTexture | undefined
            const matName = mat.name || '(unnamed)'
            const mapName = mat.map ? (mat.map.name || mat.map.image?.src || '(no name)') : '(no map)'

            // 1. ColladaLoader already set mat.map — check its .name or .image source
            if (mat.map) {
                for (const [key, tex] of decodedTextures) {
                    if (mapName.includes(key) || key.includes(mapName.replace(/\.[^.]+$/, ''))) {
                        decoded = tex
                        break
                    }
                }
            }

            // 2. Match by material name against image IDs/paths
            //    Strip _skin, _00, l_/r_ prefixes to find the base concept
            if (!decoded) {
                const matBase = mat.name.toLowerCase()
                    .replace(/^[lr]_/, '')       // l_eye → eye
                    .replace(/_skin$/, '')        // face_skin → face
                    .replace(/_default$/, '')

                for (const [imgId, relPath] of imageMap) {
                    const imgLower = imgId.toLowerCase()
                    const pathLower = relPath.toLowerCase()

                    // Direct contains
                    if (imgLower.includes(matBase) || pathLower.includes(matBase)) {
                        decoded = decodedTextures.get(imgId)
                        if (decoded) break
                    }

                    // Try the base portion of the image ID (strip Image_ prefix and _alb suffix)
                    const imgBase = imgLower
                        .replace(/^image_/, '')
                        .replace(/tr\d+_\d+_/, '')   // strip model prefix
                        .replace(/_\d+_alb$/, '')     // strip _00_alb
                        .replace(/_alb$/, '')

                    if (imgBase === matBase || imgBase.includes(matBase) || matBase.includes(imgBase)) {
                        decoded = decodedTextures.get(imgId)
                        if (decoded) break
                    }
                }
            }

            // 3. Match against all decoded texture keys (covers textures/ folder scan)
            //    BodyCKurumiru_mat → BodyC → matches pm0003_00_BodyC1.tga
            if (!decoded) {
                const matCore = mat.name.toLowerCase()
                    .replace(/_mat$/, '')
                    .replace(/^[lr]_/, '')
                    .replace(/_skin$/, '')
                    .replace(/_default$/, '')
                    .replace(/vco$/i, '')
                    .replace(/kurumiru$/i, '')   // strip variant suffixes

                for (const [key, tex] of decodedTextures) {
                    const keyLower = key.toLowerCase()
                    // Strip model prefix + trailing number/ext from texture key
                    // pm0003_00_BodyC1.tga → bodyc
                    const keyCore = keyLower
                        .replace(/^pm\d+_\d+_/, '')
                        .replace(/\d+\.tga.*$/, '')
                        .replace(/_\d+_alb$/, '')

                    if (keyCore && matCore && (
                        keyCore === matCore ||
                        matCore.startsWith(keyCore) ||
                        keyCore.startsWith(matCore)
                    )) {
                        decoded = tex
                        break
                    }
                }
            }

            console.log(`    mesh=${node.name} mat=${matName} map=${mapName} → ${decoded ? '✓ textured' : '✗ fallback'}`)

            const isEye = /eye/i.test(mat.name) && !/eyebrow/i.test(mat.name)

            if (decoded) {
                texApplied++
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
    console.log(`  ${meshCount} meshes, ${texApplied} textures applied`)

    // --- Compute bounds ---
    const bbox = new THREE.Box3().setFromObject(scene)
    const center = bbox.getCenter(new THREE.Vector3())
    const size = bbox.getSize(new THREE.Vector3())
    const maxDim = Math.max(size.x, size.y, size.z)
    console.log(`  Bounds: ${size.x.toFixed(2)} x ${size.y.toFixed(2)} x ${size.z.toFixed(2)}`)

    // --- Render all angles ---
    const angles: [string, number, number][] = [
        ['front', 0, 0],
        ['3-4', Math.PI / 5, Math.PI / 8],
        ['side', Math.PI / 2, 0],
        ['back', Math.PI, 0],
    ]

    const renderScene = new THREE.Scene()
    renderScene.add(scene)

    // Lighting
    renderScene.add(new THREE.AmbientLight(0xffffff, 0.35))
    const keyLight = new THREE.DirectionalLight(0xffffff, 0.45)
    keyLight.position.set(5, 10, 7)
    renderScene.add(keyLight)
    const fillLight = new THREE.DirectionalLight(0xffffff, 0.15)
    fillLight.position.set(-5, 5, -3)
    renderScene.add(fillLight)

    let allPassed = true
    for (const [name, yaw, pitch] of angles) {
        const camera = new THREE.PerspectiveCamera(45, W / H, 0.01, maxDim * 10)
        const dist = maxDim * 2
        camera.position.set(
            center.x + dist * Math.sin(yaw) * Math.cos(pitch),
            center.y + dist * Math.sin(pitch),
            center.z + dist * Math.cos(yaw) * Math.cos(pitch),
        )
        camera.lookAt(center)

        renderer.render(renderScene, camera)

        const pixels = new Uint8Array(W * H * 4)
        glCtx.readPixels(0, 0, W, H, glCtx.RGBA, glCtx.UNSIGNED_BYTE, pixels)

        let nonBg = 0, colored = 0
        for (let i = 0; i < pixels.length; i += 4) {
            if (pixels[i] !== 26 || pixels[i + 1] !== 26 || pixels[i + 2] !== 46) {
                nonBg++
                if (pixels[i] !== pixels[i + 1] || pixels[i + 1] !== pixels[i + 2]) colored++
            }
        }

        // Flip Y
        const flipped = new Uint8Array(W * H * 4)
        for (let y = 0; y < H; y++) {
            flipped.set(pixels.subarray((H - 1 - y) * W * 4, (H - y) * W * 4), y * W * 4)
        }

        const png = new PNG({ width: W, height: H })
        png.data = Buffer.from(flipped)
        const outFile = path.join(outDir, `${name}.png`)
        fs.writeFileSync(outFile, PNG.sync.write(png))

        const hasFill = nonBg > 500
        const hasColor = colored > 100
        if (!hasFill || !hasColor) allPassed = false
        const status = hasFill ? (hasColor ? '✓' : '⚠ no color') : '✗ empty'
        console.log(`  ${status} ${name}.png (${(nonBg / (W * H) * 100).toFixed(1)}% fill)`)
    }

    console.log(`\nOutput: ${outDir}/`)
    if (texApplied < meshCount) {
        console.log(`✗ FAILED: only ${texApplied}/${meshCount} textures matched`)
        allPassed = false
    }
    console.log(allPassed ? '✓ ALL PASSED' : '✗ SOME FAILED')

    renderer.dispose()
    process.exit(allPassed ? 0 : 1)
}

main().catch(err => { console.error('Error:', err.message); process.exit(1) })
