const API_BASE = 'http://localhost:3001'

// ---------------------------------------------------------------------------
// API response types (match backend route responses)
// ---------------------------------------------------------------------------

export interface StartExtractionParams {
  garcPath: string
  outputDir: string
  splitModelAnims: boolean
  entryLimit?: number
  deriveFolderNames?: boolean
}

export interface StartExtractionResponse {
  jobId: string
}

export interface ExtractionStatusResponse {
  jobId: string
  phase: string
  stats: {
    totalEntries: number
    processedEntries: number
    groupsFound: number
    modelsExported: number
    texturesExported: number
    clipsExported: number
    parseErrors: number
    exportErrors: number
  }
  logLines: string[]
  elapsedSeconds: number
  complete: boolean
}

export interface ExtractedGroupResult {
  folderName: string
  modelCount: number
  textureCount: number
  clipCount: number
  files: string[]
}

export interface ExtractionResultsResponse {
  jobId: string
  phase: string
  groups: ExtractedGroupResult[]
}

export interface ScannedArchive {
  subpath: string
  sizeBytes: number
  sizeLabel: string
}

export interface ScanResponse {
  romfsPath: string
  archives: ScannedArchive[]
}

// ---------------------------------------------------------------------------
// API calls
// ---------------------------------------------------------------------------

export async function startExtraction(params: StartExtractionParams): Promise<StartExtractionResponse> {
  const res = await fetch(`${API_BASE}/api/extraction/start`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify(params),
  })
  if (!res.ok) {
    const err = await res.json().catch(() => ({ error: res.statusText }))
    throw new Error(err.error || `Start failed: ${res.status}`)
  }
  return res.json()
}

export async function getExtractionStatus(jobId: string): Promise<ExtractionStatusResponse> {
  const res = await fetch(`${API_BASE}/api/extraction/status/${jobId}`)
  if (!res.ok) {
    const err = await res.json().catch(() => ({ error: res.statusText }))
    throw new Error(err.error || `Status failed: ${res.status}`)
  }
  return res.json()
}

export async function cancelExtraction(jobId: string): Promise<void> {
  const res = await fetch(`${API_BASE}/api/extraction/cancel/${jobId}`, { method: 'POST' })
  if (!res.ok) {
    const err = await res.json().catch(() => ({ error: res.statusText }))
    throw new Error(err.error || `Cancel failed: ${res.status}`)
  }
}

export async function getExtractionResults(jobId: string): Promise<ExtractionResultsResponse> {
  const res = await fetch(`${API_BASE}/api/extraction/results/${jobId}`)
  if (!res.ok) {
    const err = await res.json().catch(() => ({ error: res.statusText }))
    throw new Error(err.error || `Results failed: ${res.status}`)
  }
  return res.json()
}

export async function scanArchives(romfsPath: string): Promise<ScanResponse> {
  const res = await fetch(`${API_BASE}/api/extraction/scan`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ romfsPath }),
  })
  if (!res.ok) {
    const err = await res.json().catch(() => ({ error: res.statusText }))
    throw new Error(err.error || `Scan failed: ${res.status}`)
  }
  return res.json()
}
