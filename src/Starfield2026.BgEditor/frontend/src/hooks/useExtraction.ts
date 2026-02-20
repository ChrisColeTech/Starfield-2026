import { useState, useRef, useCallback, useEffect } from 'react'
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

export interface ExtractionState {
  /** Current phase of the extraction job */
  phase: ExtractionPhase
  /** Live stats counters */
  stats: ExtractionStats
  /** Log output lines from the backend */
  logLines: string[]
  /** Elapsed seconds since job started */
  elapsedSeconds: number
  /** Whether a job is currently running */
  running: boolean
  /** Error message if something went wrong */
  error: string | null
  /** Extracted groups after completion */
  results: ExtractedGroup[]
}

export interface ExtractionActions {
  /** Start a new extraction job */
  start: (params: StartExtractionParams) => Promise<void>
  /** Cancel the current running job */
  cancel: () => Promise<void>
  /** Reset state back to idle */
  reset: () => void
  /** Toggle a result row's expanded state */
  toggleResult: (index: number) => void
}

export function useExtraction(): [ExtractionState, ExtractionActions] {
  const [phase, setPhase] = useState<ExtractionPhase>('idle')
  const [stats, setStats] = useState<ExtractionStats>({ ...INITIAL_STATS })
  const [logLines, setLogLines] = useState<string[]>([])
  const [elapsedSeconds, setElapsedSeconds] = useState(0)
  const [running, setRunning] = useState(false)
  const [error, setError] = useState<string | null>(null)
  const [results, setResults] = useState<ExtractedGroup[]>([])

  const jobIdRef = useRef<string | null>(null)
  const pollRef = useRef<ReturnType<typeof setInterval> | null>(null)

  // Clean up polling on unmount
  useEffect(() => {
    return () => {
      if (pollRef.current) clearInterval(pollRef.current)
    }
  }, [])

  const stopPolling = useCallback(() => {
    if (pollRef.current) {
      clearInterval(pollRef.current)
      pollRef.current = null
    }
  }, [])

  const pollStatus = useCallback(async () => {
    const jobId = jobIdRef.current
    if (!jobId) return

    try {
      const status = await getExtractionStatus(jobId)
      setPhase(status.phase as ExtractionPhase)
      setStats(status.stats)
      setLogLines(status.logLines)
      setElapsedSeconds(status.elapsedSeconds)

      if (status.complete) {
        stopPolling()
        setRunning(false)

        if (status.phase === 'done') {
          // Fetch full results
          try {
            const res = await getExtractionResults(jobId)
            setResults(res.groups.map((g: ExtractedGroupResult) => ({
              folderName: g.folderName,
              modelCount: g.modelCount,
              textureCount: g.textureCount,
              clipCount: g.clipCount,
              files: g.files,
              expanded: false,
            })))
          } catch {
            // Results may not be available if cancelled/errored
          }
        }

        if (status.phase === 'error') {
          const lastLine = status.logLines[status.logLines.length - 1] || 'Unknown error'
          setError(lastLine)
        }
      }
    } catch (err) {
      // Network error during polling — don't crash, just log
      console.warn('[useExtraction] Poll error:', err)
    }
  }, [stopPolling])

  const start = useCallback(async (params: StartExtractionParams) => {
    // Reset state
    setPhase('parsing')
    setStats({ ...INITIAL_STATS })
    setLogLines([])
    setElapsedSeconds(0)
    setRunning(true)
    setError(null)
    setResults([])

    try {
      const { jobId } = await startExtraction(params)
      jobIdRef.current = jobId

      // Start polling
      pollRef.current = setInterval(pollStatus, POLL_INTERVAL_MS)
    } catch (err) {
      setPhase('error')
      setRunning(false)
      setError(err instanceof Error ? err.message : 'Failed to start extraction')
    }
  }, [pollStatus])

  const cancel = useCallback(async () => {
    const jobId = jobIdRef.current
    if (!jobId) return

    try {
      await cancelExtraction(jobId)
      // Polling will pick up the 'stopped' phase
    } catch (err) {
      console.warn('[useExtraction] Cancel error:', err)
    }
  }, [])

  const reset = useCallback(() => {
    stopPolling()
    jobIdRef.current = null
    setPhase('idle')
    setStats({ ...INITIAL_STATS })
    setLogLines([])
    setElapsedSeconds(0)
    setRunning(false)
    setError(null)
    setResults([])
  }, [stopPolling])

  const toggleResult = useCallback((index: number) => {
    setResults(prev => prev.map((r, i) =>
      i === index ? { ...r, expanded: !r.expanded } : r
    ))
  }, [])

  const state: ExtractionState = {
    phase,
    stats,
    logLines,
    elapsedSeconds,
    running,
    error,
    results,
  }

  const actions: ExtractionActions = {
    start,
    cancel,
    reset,
    toggleResult,
  }

  return [state, actions]
}
