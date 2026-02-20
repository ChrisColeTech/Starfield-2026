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
      manifests.push(JSON.parse(content))
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

  // Read a raw manifest.json from a folder path
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
      return JSON.parse(content)
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
}
