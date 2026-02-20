import { FastifyInstance } from 'fastify'
import fs from 'fs'
import path from 'path'

interface TexturePayload {
  name: string
  dataUrl: string // "data:image/png;base64,..."
}

interface SaveBody {
  dir: string // model folder path to save back to
  textures: TexturePayload[]
}

interface ExportBody {
  outputDir: string
  textures: TexturePayload[]
}

function decodeDataUrl(dataUrl: string): Buffer {
  const base64 = dataUrl.split(',')[1]
  return Buffer.from(base64, 'base64')
}

export default async function textureRoutes(app: FastifyInstance) {
  // Save textures back to original model folder
  app.post<{ Body: SaveBody }>('/api/textures/save', async (request, reply) => {
    const { dir, textures } = request.body
    if (!dir || !textures?.length) {
      return reply.status(400).send({ error: 'Missing dir or textures' })
    }
    if (!fs.existsSync(dir)) {
      return reply.status(404).send({ error: `Directory not found: ${dir}` })
    }

    let count = 0
    for (const tex of textures) {
      const outPath = path.join(dir, tex.name)
      fs.writeFileSync(outPath, decodeDataUrl(tex.dataUrl))
      count++
    }
    return { count }
  })

  // Export textures to a chosen directory
  app.post<{ Body: ExportBody }>('/api/textures/export', async (request, reply) => {
    const { outputDir, textures } = request.body
    if (!outputDir || !textures?.length) {
      return reply.status(400).send({ error: 'Missing outputDir or textures' })
    }
    fs.mkdirSync(outputDir, { recursive: true })

    let count = 0
    for (const tex of textures) {
      const outPath = path.join(outputDir, tex.name)
      fs.writeFileSync(outPath, decodeDataUrl(tex.dataUrl))
      count++
    }
    return { count }
  })
}
