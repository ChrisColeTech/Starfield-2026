import { create } from 'zustand'
import type {
  ExtractionPhase,
  ExtractionStats,
  ExtractedGroup,
} from '../types/extraction'
import { INITIAL_STATS } from '../types/extraction'
import {
  startExtraction,
  getExtractionStatus,
  cancelExtraction,
  getExtractionResults,
  type StartExtractionParams,
  type ExtractedGroupResult,
} from '../services/extractionService'

const POLL_INTERVAL_MS = 500

interface ExtractionState {
  // Job state
  jobId: string | null
  phase: ExtractionPhase
  stats: ExtractionStats
  logLines: string[]
  elapsedSeconds: number
  running: boolean
  error: string | null
  results: ExtractedGroup[]

  // Internal
  _pollTimer: ReturnType<typeof setInterval> | null

  // Actions
  start: (params: StartExtractionParams) => Promise<void>
  cancel: () => Promise<void>
  reset: () => void
  toggleResult: (index: number) => void
}

export const useExtractionStore = create<ExtractionState>()((set, get) => ({
  jobId: null,
  phase: 'idle',
  stats: { ...INITIAL_STATS },
  logLines: [],
  elapsedSeconds: 0,
  running: false,
  error: null,
  results: [],
  _pollTimer: null,

  start: async (params: StartExtractionParams) => {
    // Clear any existing poll
    const existing = get()._pollTimer
    if (existing) clearInterval(existing)

    set({
      phase: 'parsing',
      stats: { ...INITIAL_STATS },
      logLines: [],
      elapsedSeconds: 0,
      running: true,
      error: null,
      results: [],
      _pollTimer: null,
    })

    try {
      const { jobId } = await startExtraction(params)
      set({ jobId })

      // Start polling
      const timer = setInterval(async () => {
        const currentJobId = get().jobId
        if (!currentJobId) return

        try {
          const status = await getExtractionStatus(currentJobId)
          set({
            phase: status.phase as ExtractionPhase,
            stats: status.stats,
            logLines: status.logLines,
            elapsedSeconds: status.elapsedSeconds,
          })

          if (status.complete) {
            const t = get()._pollTimer
            if (t) clearInterval(t)
            set({ _pollTimer: null, running: false })

            if (status.phase === 'done') {
              try {
                const res = await getExtractionResults(currentJobId)
                set({
                  results: res.groups.map((g: ExtractedGroupResult) => ({
                    folderName: g.folderName,
                    modelCount: g.modelCount,
                    textureCount: g.textureCount,
                    clipCount: g.clipCount,
                    files: g.files,
                    expanded: false,
                  })),
                })
              } catch {
                // Results unavailable
              }
            }

            if (status.phase === 'error') {
              const lastLine = status.logLines[status.logLines.length - 1] || 'Unknown error'
              set({ error: lastLine })
            }
          }
        } catch (err) {
          console.warn('[extractionStore] Poll error:', err)
        }
      }, POLL_INTERVAL_MS)

      set({ _pollTimer: timer })
    } catch (err) {
      set({
        phase: 'error',
        running: false,
        error: err instanceof Error ? err.message : 'Failed to start extraction',
      })
    }
  },

  cancel: async () => {
    const jobId = get().jobId
    if (!jobId) return
    try {
      await cancelExtraction(jobId)
    } catch (err) {
      console.warn('[extractionStore] Cancel error:', err)
    }
  },

  reset: () => {
    const timer = get()._pollTimer
    if (timer) clearInterval(timer)
    set({
      jobId: null,
      phase: 'idle',
      stats: { ...INITIAL_STATS },
      logLines: [],
      elapsedSeconds: 0,
      running: false,
      error: null,
      results: [],
      _pollTimer: null,
    })
  },

  toggleResult: (index: number) => {
    set({
      results: get().results.map((r, i) =>
        i === index ? { ...r, expanded: !r.expanded } : r
      ),
    })
  },
}))
