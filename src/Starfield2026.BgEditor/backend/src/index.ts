import Fastify from 'fastify'
import cors from '@fastify/cors'
import fs from 'fs'
import path from 'path'
import manifestRoutes from './routes/manifests.js'
import textureRoutes from './routes/textures.js'
import extractionRoutes from './routes/extraction.js'

const ASSETS_DIR = "D:/Projects/Starfield/src/Starfield.Assets/Pokemon3D"
const PORT = 3001

const app = Fastify({ logger: true, bodyLimit: 100 * 1024 * 1024 })

await app.register(cors, { origin: true })
await app.register(manifestRoutes, { assetsDir: path.resolve(ASSETS_DIR) })
await app.register(textureRoutes)
await app.register(extractionRoutes)

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
