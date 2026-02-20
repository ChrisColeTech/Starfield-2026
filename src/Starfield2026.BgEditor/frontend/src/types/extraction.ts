// ---------------------------------------------------------------------------
// GARC Extraction types
// ---------------------------------------------------------------------------

/** A GARC archive discovered by scanning the RomFS directory */
export interface ScannedArchive {
  subpath: string
  sizeBytes: number
  sizeLabel: string
}

/** Export mode */
export type ExportMode = 'split' | 'individual'

/** Extraction phase */
export type ExtractionPhase = 'idle' | 'parsing' | 'grouping' | 'exporting' | 'done' | 'error' | 'stopped'

/** Live extraction stats */
export interface ExtractionStats {
  totalEntries: number
  processedEntries: number
  groupsFound: number
  modelsExported: number
  texturesExported: number
  clipsExported: number
  parseErrors: number
  exportErrors: number
}

/** Extraction progress state */
export interface ExtractionProgress {
  phase: ExtractionPhase
  stats: ExtractionStats
  logLines: string[]
  elapsedSeconds: number
}

/** A single extracted group result */
export interface ExtractedGroup {
  folderName: string
  modelCount: number
  textureCount: number
  clipCount: number
  files: string[]
  expanded: boolean
}

export const INITIAL_STATS: ExtractionStats = {
  totalEntries: 0,
  processedEntries: 0,
  groupsFound: 0,
  modelsExported: 0,
  texturesExported: 0,
  clipsExported: 0,
  parseErrors: 0,
  exportErrors: 0,
}

export const INITIAL_PROGRESS: ExtractionProgress = {
  phase: 'idle',
  stats: { ...INITIAL_STATS },
  logLines: [],
  elapsedSeconds: 0,
}
