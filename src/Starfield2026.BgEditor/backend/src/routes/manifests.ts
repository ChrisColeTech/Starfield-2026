import { FastifyInstance } from 'fastify'
import fs from 'fs'
import path from 'path'

const MODEL_EXTS = ['.fbx', '.dae', '.obj']
const TEXTURE_EXTS = ['.png', '.jpg', '.jpeg', '.bmp', '.tga']

interface Manifest {
  name: string
  dir: string
  assetsPath: string
  modelFile: string
  modelFormat: string
  textures: string[]
  clipCount: number
  mtlFile?: string
}

interface GenerateBody {
  inputDir: string
  outputDir?: string // defaults to inputDir (in-place)
  formats?: string[] // e.g. ["fbx","dae","obj"] — defaults to all
  overwrite?: boolean // overwrite existing manifests — defaults to true
}

function isModel(file: string, formats?: string[]): boolean {
  const ext = path.extname(file).toLowerCase()
  if (formats && formats.length > 0) {
    return formats.some(f => ext === `.${f.toLowerCase()}`)
  }
  return MODEL_EXTS.includes(ext)
}

function isTexture(file: string): boolean {
  return TEXTURE_EXTS.includes(path.extname(file).toLowerCase())
}

function generateManifestsForFolder(
  folderPath: string,
  assetsDir: string,
  formats?: string[],
): Manifest[] {
  const entries = fs.readdirSync(folderPath)
  const files = entries.filter(e => fs.statSync(path.join(folderPath, e)).isFile())

  const modelFiles = files.filter(f => isModel(f, formats)).sort()
  if (modelFiles.length === 0) return []

  const textureFiles = files.filter(f => isTexture(f))
  const mtlFile = files.find(f => path.extname(f).toLowerCase() === '.mtl')
  const manifests: Manifest[] = []

  // One manifest per model file
  for (const modelFile of modelFiles) {
    const ext = path.extname(modelFile).toLowerCase().slice(1)
    const baseName = modelFile.replace(/\.[^.]+$/, '')

    const manifest: Manifest = {
      name: baseName,
      dir: folderPath.replace(/\\/g, '/'),
      assetsPath: path.relative(assetsDir, folderPath).replace(/\\/g, '/'),
      modelFile,
      modelFormat: ext,
      textures: textureFiles,
    }
    if (mtlFile) {
      manifest.mtlFile = mtlFile
    }
    manifests.push(manifest)
  }

  return manifests
}

function scanAndGenerate(
  folderPath: string,
  assetsDir: string,
  outputDir: string,
  formats?: string[],
  overwrite?: boolean,
): string[] {
  const generated: string[] = []
  const entries = fs.readdirSync(folderPath)
  const dirs = entries.filter(e => fs.statSync(path.join(folderPath, e)).isDirectory())

  const manifests = generateManifestsForFolder(folderPath, assetsDir, formats)
  for (const manifest of manifests) {
    // Compute output path — mirror folder structure under outputDir
    const rel = path.relative(assetsDir, folderPath)
    const outFolder = outputDir === assetsDir ? folderPath : path.join(outputDir, rel)
    // Use model-specific manifest filename so multiple models in one folder don't collide
    const manifestFileName = manifests.length === 1
      ? 'manifest.json'
      : `manifest.${manifest.name}.json`
    const outPath = path.join(outFolder, manifestFileName)

    if (!overwrite && fs.existsSync(outPath)) {
      // skip
    } else {
      if (outFolder !== folderPath) {
        fs.mkdirSync(outFolder, { recursive: true })
      }
      if (outputDir !== assetsDir) {
        manifest.dir = outFolder.replace(/\\/g, '/')
        manifest.assetsPath = path.relative(outputDir, outFolder).replace(/\\/g, '/')
      }
      fs.writeFileSync(outPath, JSON.stringify(manifest, null, 2))
      generated.push(manifest.assetsPath || manifest.name)
    }
  }

  for (const dir of dirs) {
    generated.push(...scanAndGenerate(path.join(folderPath, dir), assetsDir, outputDir, formats, overwrite))
  }
  return generated
}

function collectManifests(folderPath: string): Manifest[] {
  const manifests: Manifest[] = []
  let entries: string[]
  try { entries = fs.readdirSync(folderPath) } catch { return manifests }

  // Match manifest.json and manifest.*.json
  const manifestFiles = entries.filter(e => e === 'manifest.json' || (e.startsWith('manifest.') && e.endsWith('.json')))
  for (const mf of manifestFiles) {
    try {
      const content = fs.readFileSync(path.join(folderPath, mf), 'utf-8')
      const parsed = JSON.parse(content)
      // Always use the actual folder path, not the stale dir from inside the manifest
      parsed.dir = folderPath.replace(/\\/g, '/')
      // Count clips from both flat and nested manifest formats
      let clipCount = 0
      if (Array.isArray(parsed.clips)) {
        clipCount = parsed.clips.length
      } else if (Array.isArray(parsed.models)) {
        clipCount = parsed.models.reduce((sum: number, m: any) => sum + (Array.isArray(m.clips) ? m.clips.length : 0), 0)
      }
      parsed.clipCount = clipCount
      manifests.push(parsed)
    } catch { /* skip malformed */ }
  }

  const dirs = entries.filter(e => {
    try { return fs.statSync(path.join(folderPath, e)).isDirectory() }
    catch { return false }
  })

  for (const dir of dirs) {
    manifests.push(...collectManifests(path.join(folderPath, dir)))
  }
  return manifests
}

export default async function manifestRoutes(app: FastifyInstance, opts: { assetsDir: string }) {
  const { assetsDir } = opts

  // Read a raw manifest.json from a folder path — normalizes to consistent models[] format
  app.get<{ Querystring: { dir: string } }>('/api/manifests/read', async (request, reply) => {
    const dir = request.query.dir
    if (!dir) {
      reply.code(400)
      return { error: 'Missing "dir" query parameter' }
    }
    const manifestPath = path.join(dir, 'manifest.json')
    if (!fs.existsSync(manifestPath)) {
      reply.code(404)
      return { error: `manifest.json not found in ${dir}` }
    }
    try {
      const content = fs.readFileSync(manifestPath, 'utf-8')
      const parsed = JSON.parse(content)

      // Normalize clips array — ensure each clip has id, sourceName fields
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

      if (parsed.models && Array.isArray(parsed.models)) {
        // Models exist — normalize each model entry
        const rootClips = Array.isArray(parsed.clips) ? parsed.clips : []
        parsed.models = parsed.models.map((m: any) => ({
          name: m.name || '',
          // Support both "file" and "modelFile" keys
          modelFile: m.modelFile || m.file || parsed.modelFile || 'model.dae',
          // Clips may be on the model or at root level
          clips: normalizeClips(m.clips || rootClips),
          meshCount: m.meshCount,
          boneCount: m.boneCount,
        }))
      } else if (parsed.modelFile) {
        // Flat format — no models array, just modelFile + clips at root
        const modelName = parsed.modelFile.replace(/\.[^.]+$/, '')
        parsed.models = [{
          name: modelName,
          modelFile: parsed.modelFile,
          clips: normalizeClips(parsed.clips || []),
        }]
      }

      if (!parsed.mode) parsed.mode = 'split-model-anims'
      return parsed
    } catch (err) {
      reply.code(500)
      return { error: `Failed to read manifest: ${err}` }
    }
  })

  // Write an updated manifest.json back to disk
  app.post<{ Body: { dir: string; manifest: Record<string, unknown> } }>('/api/manifests/save', async (request, reply) => {
    const { dir, manifest } = request.body || {}
    if (!dir || !manifest) {
      reply.code(400)
      return { error: 'Missing "dir" or "manifest" in request body' }
    }
    if (!fs.existsSync(dir)) {
      reply.code(404)
      return { error: `Directory not found: ${dir}` }
    }
    try {
      const manifestPath = path.join(dir, 'manifest.json')
      fs.writeFileSync(manifestPath, JSON.stringify(manifest, null, 2))
      return { ok: true }
    } catch (err) {
      reply.code(500)
      return { error: `Failed to write manifest: ${err}` }
    }
  })

  // Get current config defaults
  app.get('/api/manifests/config', async () => {
    return {
      defaultInputDir: assetsDir.replace(/\\/g, '/'),
      defaultOutputDir: assetsDir.replace(/\\/g, '/'),
      supportedFormats: MODEL_EXTS.map(e => e.slice(1)),
    }
  })

  app.post<{ Body: GenerateBody }>('/api/manifests/generate', async (request) => {
    const { inputDir, outputDir, formats, overwrite } = request.body || {} as GenerateBody
    const scanDir = inputDir || assetsDir
    const outDir = outputDir || scanDir
    const doOverwrite = overwrite !== false

    if (!fs.existsSync(scanDir)) {
      return { error: `Input directory not found: ${scanDir}`, generated: 0, folders: [] }
    }
    if (outDir !== scanDir && !fs.existsSync(outDir)) {
      fs.mkdirSync(outDir, { recursive: true })
    }

    const folders = scanAndGenerate(scanDir, scanDir, outDir, formats, doOverwrite)
    return { generated: folders.length, folders }
  })

  app.get<{ Querystring: { dir?: string } }>('/api/manifests', async (request) => {
    const dir = request.query.dir || assetsDir
    if (!fs.existsSync(dir)) {
      return []
    }
    return collectManifests(dir)
  })

  // Bake a clip's animations into the model DAE and return the merged XML
  app.get<{ Querystring: { dir: string; clip: string; model?: string } }>(
    '/api/clips/bake',
    async (request, reply) => {
      const { dir, clip, model } = request.query
      if (!dir || !clip) {
        reply.code(400)
        return { error: 'Missing "dir" and/or "clip" query parameters' }
      }

      const modelFile = model || 'model.dae'
      const modelPath = path.join(dir, modelFile)
      const clipPath = path.join(dir, clip)

      if (!fs.existsSync(modelPath)) {
        reply.code(404)
        return { error: `Model not found: ${modelFile}` }
      }
      if (!fs.existsSync(clipPath)) {
        reply.code(404)
        return { error: `Clip not found: ${clip}` }
      }

      try {
        const modelXml = fs.readFileSync(modelPath, 'utf-8')
        const clipXml = fs.readFileSync(clipPath, 'utf-8')

        // Extract <library_animations> ... </library_animations> from clip
        const animStart = clipXml.indexOf('<library_animations')
        const animEnd = clipXml.indexOf('</library_animations>')
        if (animStart < 0 || animEnd < 0) {
          reply.code(422)
          return { error: 'Clip DAE has no <library_animations>' }
        }
        const animBlock = clipXml.substring(animStart, animEnd + '</library_animations>'.length)

        // Remove any existing <library_animations> from model
        let merged = modelXml
        const existingStart = merged.indexOf('<library_animations')
        if (existingStart >= 0) {
          const existingEnd = merged.indexOf('</library_animations>', existingStart)
          if (existingEnd >= 0) {
            merged = merged.substring(0, existingStart) +
              merged.substring(existingEnd + '</library_animations>'.length)
          }
        }

        // Insert clip animations before </COLLADA>
        const insertIdx = merged.lastIndexOf('</COLLADA>')
        if (insertIdx < 0) {
          reply.code(422)
          return { error: 'Model DAE missing </COLLADA> tag' }
        }
        merged = merged.substring(0, insertIdx) + '\n' + animBlock + '\n' + merged.substring(insertIdx)

        reply.type('application/xml').send(merged)
      } catch (err) {
        reply.code(500)
        return { error: `Bake failed: ${err}` }
      }
    },
  )
}
