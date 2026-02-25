import Fastify from 'fastify'
import cors from '@fastify/cors'
import websocket from '@fastify/websocket'
import type { WebSocket } from 'ws'
import fs from 'fs'
import path from 'path'
import manifestRoutes, { collectManifests } from './routes/manifests.js'
import textureRoutes from './routes/textures.js'
import extractionRoutes from './routes/extraction.js'
import {
  createHeadlessRenderer, setupCamera, setupLighting,
  loadManifestModel, loadDaeFromDisk, fixMaterialsForRender, captureAndSave,
} from './cli-render.js'
import * as THREE from 'three'

const ASSETS_DIR = "D:/Projects/Starfield/src/Starfield.Assets/Pokemon3D"
const PORT = 3001

const app = Fastify({ logger: true, bodyLimit: 100 * 1024 * 1024 })

await app.register(cors, { origin: true })
await app.register(websocket)
await app.register(manifestRoutes, { assetsDir: path.resolve(ASSETS_DIR) })
await app.register(textureRoutes)
await app.register(extractionRoutes)

// ─── WebSocket ───
const wsClients = new Set<WebSocket>()

function broadcast(type: string, data: Record<string, any>) {
  const msg = JSON.stringify({ type, ...data })
  for (const ws of wsClients) {
    if (ws.readyState === 1) ws.send(msg)
  }
}

app.get('/ws', { websocket: true }, (socket) => {
  wsClients.add(socket)
  console.log(`[WS] Client connected (${wsClients.size} total)`)

  // Relay incoming messages to all OTHER clients (MCP → frontend)
  socket.on('message', (raw) => {
    const msg = raw.toString()
    for (const ws of wsClients) {
      if (ws !== socket && ws.readyState === 1) ws.send(msg)
    }
  })

  socket.on('close', () => {
    wsClients.delete(socket)
    console.log(`[WS] Client disconnected (${wsClients.size} total)`)
  })
})

export { broadcast }

// ─── Manifest normalization helpers ───

function normalizeManifest(manifest: any): any {
  const normalizeClips = (clips: any[]) => clips.map((c: any, i: number) => ({
    index: c.index ?? i,
    id: c.id || c.name || `clip_${String(i).padStart(3, '0')}`,
    name: c.name || c.id || `clip_${i}`,
    sourceName: c.sourceName || c.name || '',
    semanticName: c.semanticName || null,
    semanticSource: c.semanticSource || null,
    file: c.file || '',
    frameCount: c.frameCount || 0,
    fps: c.fps || 30,
  }))

  if (manifest.models && Array.isArray(manifest.models)) {
    const rootClips = Array.isArray(manifest.clips) ? manifest.clips : []
    manifest.models = manifest.models.map((m: any) => ({
      name: m.name || '',
      modelFile: m.modelFile || m.file || manifest.modelFile || 'model.dae',
      clips: normalizeClips(m.clips || rootClips),
      meshCount: m.meshCount,
      boneCount: m.boneCount,
    }))
  } else if (manifest.modelFile) {
    const modelName = manifest.modelFile.replace(/\.[^.]+$/, '')
    manifest.models = [{
      name: modelName,
      modelFile: manifest.modelFile,
      clips: normalizeClips(manifest.clips || []),
    }]
  }
  if (!manifest.mode) manifest.mode = 'split-model-anims'
  return manifest
}

function readAndNormalizeManifest(dir: string): any {
  const manifestPath = path.join(dir, 'manifest.json')
  if (!fs.existsSync(manifestPath)) {
    throw new Error(`manifest.json not found in ${dir}`)
  }
  const manifest = JSON.parse(fs.readFileSync(manifestPath, 'utf-8'))
  manifest.dir = dir
  return normalizeManifest(manifest)
}

// ─── MCP → Frontend bridge ───

app.post<{ Body: { path: string; type: 'manifest' | 'dae' | 'folder' } }>('/api/load-model', async (request, reply) => {
  const { path: modelPath, type } = request.body
  if (!modelPath) return reply.status(400).send({ error: 'Missing path' })

  if (wsClients.size === 0) {
    return reply.status(503).send({ error: 'No frontend connected. Please open the BgEditor UI first.' })
  }

  try {
    if (type === 'folder') {
      // Recursively scan folder for manifests
      const dir = modelPath.replace(/\\/g, '/')
      const raw = collectManifests(dir)
      if (raw.length === 0) {
        return reply.status(404).send({ error: `No manifests found in ${dir}` })
      }
      const manifests = raw.map(m => normalizeManifest(m))
      broadcast('model:load', { dir, modelType: 'folder', manifests, manifest: manifests[0] })
      return reply.send({ ok: true, count: manifests.length })
    }

    // Single file (manifest or dae)
    const dir = modelPath.replace(/[\\/][^\\/]+$/, '').replace(/\\/g, '/')
    const manifest = readAndNormalizeManifest(dir)
    broadcast('model:load', { dir, modelType: type, manifests: [manifest], manifest })
    return reply.send({ ok: true })
  } catch (err: any) {
    return reply.status(500).send({ error: err.message })
  }
})

app.post<{ Body: { pathA: string; pathB: string } }>('/api/compare-models', async (request, reply) => {
  const { pathA, pathB } = request.body
  if (!pathA || !pathB) return reply.status(400).send({ error: 'Missing pathA or pathB' })

  if (wsClients.size === 0) {
    return reply.status(503).send({ error: 'No frontend connected. Please open the BgEditor UI first.' })
  }

  try {
    // Resolve dirs — strip filename if path points to a file
    const dirA = fs.statSync(pathA).isDirectory() ? pathA.replace(/\\/g, '/') : pathA.replace(/[\\/][^\\/]+$/, '').replace(/\\/g, '/')
    const dirB = fs.statSync(pathB).isDirectory() ? pathB.replace(/\\/g, '/') : pathB.replace(/[\\/][^\\/]+$/, '').replace(/\\/g, '/')

    const manifestA = readAndNormalizeManifest(dirA)
    const manifestB = readAndNormalizeManifest(dirB)
    const manifests = [manifestA, manifestB]

    broadcast('model:compare', { manifests, manifest: manifestA })
    return reply.send({ ok: true, models: [manifestA.name || dirA, manifestB.name || dirB] })
  } catch (err: any) {
    return reply.status(500).send({ error: err.message })
  }
})

// ─── Screenshot capture (MCP → WS → Electron IPC → file) ───

const pendingScreenshots = new Map<string, { resolve: (v: any) => void; timer: NodeJS.Timeout }>()

app.post<{ Body: { outputPath: string } }>('/api/screenshot', async (request, reply) => {
  const { outputPath } = request.body
  if (!outputPath) return reply.status(400).send({ error: 'Missing outputPath' })

  if (wsClients.size === 0) {
    return reply.status(503).send({ error: 'No frontend connected.' })
  }

  const requestId = `ss_${Date.now()}`

  // Wait for frontend to capture and POST back the result
  const result = await new Promise<any>((resolve) => {
    const timer = setTimeout(() => {
      pendingScreenshots.delete(requestId)
      resolve({ error: 'Screenshot timed out after 10s' })
    }, 10000)
    pendingScreenshots.set(requestId, { resolve, timer })
    broadcast('screenshot:capture', { requestId, outputPath })
  })

  if (result.error) return reply.status(500).send(result)
  return reply.send(result)
})

// Frontend POSTs back the result after Electron captures
app.post<{ Body: { requestId: string; ok?: boolean; path?: string; size?: number; error?: string } }>(
  '/api/screenshot/result',
  async (request, reply) => {
    const { requestId, ...result } = request.body
    const pending = pendingScreenshots.get(requestId)
    if (pending) {
      clearTimeout(pending.timer)
      pendingScreenshots.delete(requestId)
      pending.resolve(result)
    }
    return reply.send({ ok: true })
  }
)

// Serve model/texture files from any directory on disk.
// The frontend encodes the manifest's `dir` (absolute path) as a base64url
// token in the URL: /serve/<token>/<filename>
// Three.js loaders resolve textures relative to the model URL, so all files
// in the same directory are served under the same base path automatically.
function decodeDirToken(token: string): string {
  // base64url → base64
  const b64 = token.replace(/-/g, '+').replace(/_/g, '/')
  return Buffer.from(b64, 'base64').toString()
}

app.get('/serve/*', async (request, reply) => {
  const wildcard = (request.params as { '*': string })['*']
  const slashIdx = wildcard.indexOf('/')
  if (slashIdx < 0) return reply.status(400).send({ error: 'Missing path' })
  const token = wildcard.slice(0, slashIdx)
  const fileName = wildcard.slice(slashIdx + 1)
  if (!token || !fileName) return reply.status(400).send({ error: 'Missing path' })

  const dir = decodeDirToken(token)
  const fullPath = path.resolve(dir, fileName)

  // Security: ensure resolved path stays within the decoded directory
  const resolvedDir = path.resolve(dir)
  if (!fullPath.startsWith(resolvedDir + path.sep) && fullPath !== resolvedDir) {
    return reply.status(403).send({ error: 'Forbidden' })
  }

  if (!fs.existsSync(fullPath) || !fs.statSync(fullPath).isFile()) {
    return reply.status(404).send({ error: `Not found: ${fileName}` })
  }

  const ext = path.extname(fullPath).toLowerCase()
  const stream = fs.createReadStream(fullPath)
  return reply.type(mimeForExt(ext)).send(stream)
})

// Keep the query-based endpoint as a fallback
app.get<{ Querystring: { dir: string; name: string } }>('/api/file', async (request, reply) => {
  const { dir, name } = request.query

  if (!dir || !name) {
    return reply.status(400).send({ error: 'Missing dir or name parameter' })
  }

  const fullPath = path.resolve(dir, name)

  if (!fullPath.startsWith(path.resolve(dir))) {
    return reply.status(403).send({ error: 'Forbidden' })
  }

  if (!fs.existsSync(fullPath)) {
    return reply.status(404).send({ error: `File not found: ${name}` })
  }

  const ext = path.extname(name).toLowerCase()
  const mime = mimeForExt(ext)

  const stream = fs.createReadStream(fullPath)
  return reply.type(mime).send(stream)
})

// Save rendered images to disk
app.post<{ Body: { dir: string; files: { name: string; data: string }[] } }>('/api/save-render', async (request, reply) => {
  const { dir, files } = request.body
  if (!dir || !files?.length) return reply.status(400).send({ error: 'Missing dir or files' })

  fs.mkdirSync(dir, { recursive: true })
  const saved: string[] = []
  for (const file of files) {
    const buf = Buffer.from(file.data.replace(/^data:image\/png;base64,/, ''), 'base64')
    const outPath = path.join(dir, file.name)
    fs.writeFileSync(outPath, buf)
    saved.push(outPath)
  }
  return reply.send({ saved })
})

// Render model from multiple angles
app.post<{ Body: { modelPath?: string; manifestPath?: string; outputDir: string; width?: number; height?: number } }>('/api/render-angles', async (request, reply) => {
  const { modelPath, manifestPath, outputDir, width = 512, height = 512 } = request.body
  if (!outputDir) return reply.status(400).send({ error: 'Missing outputDir' })
  if (!modelPath && !manifestPath) return reply.status(400).send({ error: 'Missing modelPath or manifestPath' })

  try {
    // Load model
    const result = manifestPath
      ? await loadManifestModel(manifestPath)
      : await loadDaeFromDisk(modelPath!)

    const bbox = new THREE.Box3().setFromObject(result.scene)
    const { renderer, glCtx } = createHeadlessRenderer(width, height, '1a1a2e')

    const renderScene = new THREE.Scene()
    renderScene.add(result.scene)
    setupLighting(renderScene, 'studio')

    fs.mkdirSync(outputDir, { recursive: true })

    const angles = ['front', '3/4', 'side', 'back'] as const
    const saved: string[] = []

    for (const angle of angles) {
      broadcast('render:progress', { angle, status: 'rendering' })
      const camera = setupCamera(angle, bbox, width, height, 1.0, 0)
      renderer.render(renderScene, camera)
      const outPath = path.join(outputDir, `${angle.replace('/', '-')}.png`)
      captureAndSave(renderer, glCtx, width, height, outPath)
      saved.push(outPath)
    }

    renderer.dispose()
    broadcast('render:complete', { outputDir, files: saved.map(f => path.basename(f)) })
    return reply.send({ saved })
  } catch (err: any) {
    return reply.status(500).send({ error: err.message })
  }
})

function mimeForExt(ext: string): string {
  switch (ext) {
    case '.png': return 'image/png'
    case '.jpg': case '.jpeg': return 'image/jpeg'
    case '.bmp': return 'image/bmp'
    case '.tga': return 'application/octet-stream'
    case '.fbx': return 'application/octet-stream'
    case '.dae': return 'text/xml'
    case '.obj': return 'text/plain'
    case '.mtl': return 'text/plain'
    default: return 'application/octet-stream'
  }
}

app.listen({ port: PORT }, (err) => {
  if (err) {
    app.log.error(err)
    process.exit(1)
  }
  console.log(`Backend listening on http://localhost:${PORT}`)
})
