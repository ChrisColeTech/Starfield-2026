import { useState, useRef, useEffect, useCallback } from 'react'
import type {
  ExportMode,
  ExtractionPhase,
  ExtractedGroup,
} from '../types/extraction'
import { useExtractionStore } from '../store/extractionStore'
import { scanArchives, type ScannedArchive } from '../services/extractionService'

// ---------------------------------------------------------------------------
// Phase helpers
// ---------------------------------------------------------------------------

function phaseLabel(phase: ExtractionPhase): string {
  switch (phase) {
    case 'idle': return 'Ready'
    case 'parsing': return 'Parsing entries...'
    case 'grouping': return 'Grouping entries...'
    case 'exporting': return 'Exporting models...'
    case 'done': return 'Complete'
    case 'error': return 'Error'
    case 'stopped': return 'Stopped'
    default: return ''
  }
}

function phaseBarGradient(phase: ExtractionPhase): string {
  switch (phase) {
    case 'parsing': return 'linear-gradient(90deg, #6366f1, #818cf8)'
    case 'grouping': return 'linear-gradient(90deg, #7c5cf7, #a77cff)'
    case 'exporting': return 'linear-gradient(90deg, #3bb078, #55cc88)'
    case 'done': return 'linear-gradient(90deg, #33cc66, #55ee88)'
    case 'error': return 'linear-gradient(90deg, #c74e4e, #e06060)'
    case 'stopped': return 'linear-gradient(90deg, #cc8833, #ffaa44)'
    default: return 'linear-gradient(90deg, #6366f1, #818cf8)'
  }
}

function phaseGlowColor(phase: ExtractionPhase): string {
  switch (phase) {
    case 'parsing': return 'rgba(99, 102, 241, 0.4)'
    case 'grouping': return 'rgba(124, 92, 247, 0.4)'
    case 'exporting': return 'rgba(59, 176, 120, 0.4)'
    case 'done': return 'rgba(51, 204, 102, 0.4)'
    case 'error': return 'rgba(199, 78, 78, 0.4)'
    case 'stopped': return 'rgba(204, 136, 51, 0.4)'
    default: return 'rgba(99, 102, 241, 0.2)'
  }
}

function phaseTextClass(phase: ExtractionPhase): string {
  switch (phase) {
    case 'done': return 'text-success'
    case 'error': return 'text-destructive'
    case 'stopped': return 'text-warning'
    case 'parsing': return 'text-primary'
    case 'grouping': return 'text-[#a77cff]'
    case 'exporting': return 'text-success'
    default: return 'text-muted-foreground'
  }
}

function logLineClass(line: string): string {
  if (line.startsWith('===')) return 'text-success'
  if (line.startsWith('  Error') || line.includes('failed')) return 'text-destructive'
  if (line.startsWith('---')) return 'text-warning'
  if (line.startsWith('Phase')) return 'text-primary'
  return 'text-muted-foreground'
}

function fileColorClass(f: string): string {
  if (f.endsWith('.dae')) return 'text-primary'
  if (f.endsWith('.png')) return 'text-success'
  if (f.endsWith('.json')) return 'text-warning'
  return 'text-muted-foreground'
}

// ---------------------------------------------------------------------------
// Animated stripe CSS (injected once)
// ---------------------------------------------------------------------------

const STRIPE_KEYFRAMES_ID = 'extraction-stripe-keyframes'
function ensureStripeKeyframes() {
  if (document.getElementById(STRIPE_KEYFRAMES_ID)) return
  const style = document.createElement('style')
  style.id = STRIPE_KEYFRAMES_ID
  style.textContent = `
    @keyframes extraction-stripe-move {
      0% { background-position: 0 0; }
      100% { background-position: 40px 0; }
    }
  `
  document.head.appendChild(style)
}


// ---------------------------------------------------------------------------
// Component
// ---------------------------------------------------------------------------

export default function ExtractionPage() {
  // Store
  const {
    phase, stats, logLines, elapsedSeconds, running, error, results,
    start, cancel, reset, toggleResult,
  } = useExtractionStore()

  // Source selection
  const [garcPath, setGarcPath] = useState('')
  const [scannedArchives, setScannedArchives] = useState<ScannedArchive[]>([])
  const [selectedSubpath, setSelectedSubpath] = useState('')
  const [scanning, setScanning] = useState(false)
  const [scanError, setScanError] = useState<string | null>(null)

  // Output config
  const [outputDir, setOutputDir] = useState('')
  const [exportMode, setExportMode] = useState<ExportMode>('split')
  const [entryLimit, setEntryLimit] = useState('')
  const [deriveFolderNames, setDeriveFolderNames] = useState(true)

  const logEndRef = useRef<HTMLDivElement>(null)

  // Inject stripe animation CSS
  useEffect(() => { ensureStripeKeyframes() }, [])

  // Auto-scroll log
  useEffect(() => {
    logEndRef.current?.scrollIntoView({ behavior: 'smooth' })
  }, [logLines.length])

  // Scan RomFS when path changes (debounced)
  useEffect(() => {
    if (!garcPath.trim()) {
      setScannedArchives([])
      setSelectedSubpath('')
      setScanError(null)
      return
    }

    const timer = setTimeout(async () => {
      setScanning(true)
      setScanError(null)
      try {
        const result = await scanArchives(garcPath.trim())
        setScannedArchives(result.archives)
        if (result.archives.length > 0 && !selectedSubpath) {
          setSelectedSubpath(result.archives[0].subpath)
        }
      } catch (err) {
        setScanError(err instanceof Error ? err.message : 'Scan failed')
        setScannedArchives([])
      } finally {
        setScanning(false)
      }
    }, 500)

    return () => clearTimeout(timer)
  }, [garcPath])

  // Derived values
  const fullGarcPath = garcPath && selectedSubpath
    ? `${garcPath.replace(/[\\/]+$/, '')}/${selectedSubpath}`
    : garcPath

  // Browse
  const handleBrowseGarc = async () => {
    const picked = await window.electronAPI.browseFolder(garcPath)
    if (picked) setGarcPath(picked)
  }

  const handleBrowseOutput = async () => {
    const picked = await window.electronAPI.browseFolder(outputDir || garcPath)
    if (picked) setOutputDir(picked)
  }

  // Start extraction
  const handleStart = useCallback(() => {
    start({
      garcPath: fullGarcPath || garcPath,
      outputDir,
      splitModelAnims: exportMode === 'split',
      entryLimit: entryLimit ? parseInt(entryLimit, 10) : undefined,
      deriveFolderNames,
    })
  }, [start, fullGarcPath, garcPath, outputDir, exportMode, entryLimit, deriveFolderNames])

  // Stop extraction
  const handleStop = useCallback(() => {
    cancel()
  }, [cancel])

  // Open in explorer
  const handleOpenExplorer = useCallback((_folderName: string) => {
    // TODO: shell.openPath(path.join(outputDir, folderName))
  }, [])

  const isRunning = running
  const isDone = phase === 'done' || phase === 'error' || phase === 'stopped'
  const canStart = !isRunning && garcPath.trim() !== '' && outputDir.trim() !== '' && selectedSubpath !== ''
  const progressPercent = stats.totalEntries > 0
    ? Math.round((stats.processedEntries / stats.totalEntries) * 100)
    : 0

  const isActive = isRunning && phase !== 'idle'

  return (
    <div className="flex flex-col h-full overflow-hidden">
      {/* Header */}
      <div className="px-6 py-4 bg-card border-b border-border">
        <h1 className="m-0 text-lg text-foreground">GARC Extraction</h1>
      </div>

      <div className="flex-1 overflow-auto p-6 flex flex-col gap-6">
        {/* ── Source + Output Card ── */}
        <div className="bg-card border border-border rounded-lg p-5">
          <h2 className="m-0 mb-4 text-[15px] text-foreground">Extraction Settings</h2>

          {/* RomFS base path */}
          <div className="mb-3.5">
            <label className="text-xs text-muted-foreground mb-1 block">RomFS Base Path</label>
            <div className="flex gap-2">
              <input
                type="text"
                value={garcPath}
                onChange={e => setGarcPath(e.target.value)}
                placeholder="Path to RomFS root (e.g. D:/dump/RomFS)..."
                className="flex-1 px-3 py-2 bg-input border border-border rounded text-foreground text-[13px] outline-none"
              />
              <button onClick={handleBrowseGarc} className="px-4 py-2 bg-input text-foreground border border-border rounded cursor-pointer text-[13px] whitespace-nowrap hover:bg-muted">Browse...</button>
            </div>
          </div>

          {/* GARC Archive dropdown */}
          <div className="mb-3.5">
            <label className="text-xs text-muted-foreground mb-1 block">
              GARC Archive
              {scanning && <span className="text-primary ml-2">Scanning...</span>}
              {!scanning && scannedArchives.length > 0 && (
                <span className="text-muted-foreground/50 ml-2">
                  {scannedArchives.length} archives found
                </span>
              )}
            </label>
            {scanError && (
              <div className="text-xs text-destructive mb-1.5">{scanError}</div>
            )}
            <select
              value={selectedSubpath}
              onChange={e => setSelectedSubpath(e.target.value)}
              disabled={scannedArchives.length === 0}
              className="w-full px-3 py-2 bg-input border border-border rounded text-foreground text-[13px] outline-none disabled:opacity-50"
            >
              {scannedArchives.length === 0 ? (
                <option value="">
                  {garcPath ? (scanning ? 'Scanning...' : 'No archives found') : 'Enter RomFS path to scan'}
                </option>
              ) : (
                scannedArchives.map(a => (
                  <option key={a.subpath} value={a.subpath}>
                    {a.subpath}  ({a.sizeLabel})
                  </option>
                ))
              )}
            </select>
          </div>

          {/* Resolved full path preview */}
          {garcPath && selectedSubpath && (
            <div className="text-xs text-muted-foreground/50 mb-4 px-2.5 py-1.5 bg-background rounded border border-border font-mono">
              Full path: <span className="text-primary">{fullGarcPath}</span>
            </div>
          )}

          {/* Output Directory */}
          <div className="mb-3.5">
            <label className="text-xs text-muted-foreground mb-1 block">Output Directory</label>
            <div className="flex gap-2">
              <input
                type="text"
                value={outputDir}
                onChange={e => setOutputDir(e.target.value)}
                placeholder="Directory for extracted files..."
                className="flex-1 px-3 py-2 bg-input border border-border rounded text-foreground text-[13px] outline-none"
              />
              <button onClick={handleBrowseOutput} className="px-4 py-2 bg-input text-foreground border border-border rounded cursor-pointer text-[13px] whitespace-nowrap hover:bg-muted">Browse...</button>
            </div>
          </div>

          {/* Options Row */}
          <div className="flex gap-6 mb-4 flex-wrap">
            <div>
              <label className="text-xs text-muted-foreground mb-1 block">Export Mode</label>
              <div className="flex gap-3">
                <label className="text-[13px] text-foreground cursor-pointer flex items-center gap-1.5">
                  <input type="radio" name="exportMode" checked={exportMode === 'split'} onChange={() => setExportMode('split')} className="accent-primary" />
                  Split (mesh DAE + clip DAEs)
                </label>
                <label className="text-[13px] text-foreground cursor-pointer flex items-center gap-1.5">
                  <input type="radio" name="exportMode" checked={exportMode === 'individual'} onChange={() => setExportMode('individual')} className="accent-primary" />
                  Individual (baked DAEs)
                </label>
              </div>
              {exportMode === 'split' && (
                <div className="text-[11px] text-muted-foreground/50 mt-1">Recommended. One mesh-only DAE + separate animation clip DAEs per model.</div>
              )}
              {exportMode === 'individual' && (
                <div className="text-[11px] text-muted-foreground/50 mt-1">One DAE per model with all animations baked in. No manifest or clip files.</div>
              )}
            </div>
            <div className="w-32">
              <label className="text-xs text-muted-foreground mb-1 block">Entry Limit</label>
              <input
                type="number"
                value={entryLimit}
                onChange={e => setEntryLimit(e.target.value)}
                placeholder="e.g. 100"
                min={1}
                className="w-full px-3 py-2 bg-input border border-border rounded text-foreground text-[13px] outline-none"
              />
            </div>
            <div className="flex items-end">
              <label className="text-[13px] text-foreground cursor-pointer flex items-center gap-1.5">
                <input type="checkbox" checked={deriveFolderNames} onChange={e => setDeriveFolderNames(e.target.checked)} className="accent-primary" />
                Derive folder names from textures
              </label>
            </div>
          </div>

          {/* Start / Stop buttons + status */}
          <div className="flex items-center gap-3">
            <button
              onClick={handleStart}
              disabled={!canStart}
              className={`px-5 py-2 rounded text-[13px] font-semibold border-none cursor-pointer disabled:cursor-default ${canStart
                  ? 'bg-primary text-primary-foreground'
                  : 'bg-input text-muted-foreground/50'
                }`}
            >
              {isRunning ? 'Running...' : 'Start Extraction'}
            </button>
            <button
              onClick={handleStop}
              disabled={!isRunning}
              className={`px-5 py-2 rounded text-[13px] font-semibold border-none cursor-pointer disabled:cursor-default disabled:opacity-50 ${isRunning ? 'bg-destructive text-white' : 'bg-input text-white'
                }`}
            >
              Stop
            </button>
            {isDone && !isRunning && phase === 'done' && (
              <span className="text-[13px] text-success">
                Extracted {results.length} groups in {elapsedSeconds.toFixed(1)}s
              </span>
            )}
            {isDone && phase === 'error' && (
              <span className="text-[13px] text-destructive">Extraction failed</span>
            )}
            {isDone && phase === 'stopped' && (
              <span className="text-[13px] text-warning">Stopped by user</span>
            )}
          </div>
        </div>

        {/* ── Progress Card ── */}
        {(isRunning || isDone) && (
          <div className="bg-card border border-border rounded-lg p-5">
            <div className="flex justify-between items-center mb-3.5">
              <h2 className="m-0 text-[15px] text-foreground">Progress</h2>
              <span className="text-xs text-muted-foreground">
                {stats.totalEntries > 0
                  ? `${stats.processedEntries.toLocaleString()} / ${stats.totalEntries.toLocaleString()} entries`
                  : '\u2014'}
                {elapsedSeconds > 0 && (
                  <span className="ml-3">{elapsedSeconds.toFixed(1)}s</span>
                )}
              </span>
            </div>

            {/* Progress bar */}
            <div className="mb-3.5">
              <div className="flex justify-between items-baseline mb-1.5">
                <span className={`text-[13px] font-medium ${phaseTextClass(phase)}`}>
                  {phaseLabel(phase)}
                </span>
                <span className="text-[13px] text-foreground font-semibold" style={{ fontVariantNumeric: 'tabular-nums' }}>
                  {progressPercent}%
                </span>
              </div>
              <div className="h-2.5 bg-background rounded overflow-hidden relative">
                <div style={{
                  height: '100%',
                  width: `${progressPercent}%`,
                  background: phaseBarGradient(phase),
                  borderRadius: 5,
                  transition: 'width 0.4s ease, background 0.6s ease',
                  boxShadow: `0 0 8px ${phaseGlowColor(phase)}, inset 0 1px 0 rgba(255,255,255,0.15)`,
                  position: 'relative',
                  ...(isActive ? {
                    backgroundImage: `${phaseBarGradient(phase)}, repeating-linear-gradient(
                      -45deg,
                      transparent,
                      transparent 8px,
                      rgba(255,255,255,0.07) 8px,
                      rgba(255,255,255,0.07) 16px
                    )`,
                    backgroundSize: '100% 100%, 40px 40px',
                    animation: 'extraction-stripe-move 1s linear infinite',
                  } : {}),
                }} />
              </div>
            </div>

            {/* Stats grid */}
            <div className="grid gap-2 mb-3.5" style={{ gridTemplateColumns: 'repeat(auto-fill, minmax(140px, 1fr))' }}>
              {[
                { label: 'Groups Found', value: stats.groupsFound },
                { label: 'Models Exported', value: stats.modelsExported },
                { label: 'Textures Exported', value: stats.texturesExported },
                { label: 'Clips Exported', value: stats.clipsExported },
                { label: 'Parse Errors', value: stats.parseErrors, warn: true },
                { label: 'Export Errors', value: stats.exportErrors, warn: true },
              ].map(stat => (
                <div key={stat.label} className="bg-background rounded px-3 py-2">
                  <div className="text-[11px] text-muted-foreground/50 mb-0.5">{stat.label}</div>
                  <div className={`text-base font-semibold ${stat.warn && stat.value > 0 ? 'text-warning' : 'text-foreground'}`}>
                    {stat.value.toLocaleString()}
                  </div>
                </div>
              ))}
            </div>

            {/* Log area */}
            <div className="bg-background border border-border rounded p-3 max-h-[200px] overflow-y-auto font-mono text-[11px] leading-relaxed text-muted-foreground">
              {logLines.length === 0 ? (
                <div className="text-muted-foreground/50">Waiting to start...</div>
              ) : (
                logLines.map((line, i) => (
                  <div key={i} className={logLineClass(line)} style={{
                    whiteSpace: 'pre-wrap',
                    minHeight: line === '' ? 8 : undefined,
                  }}>
                    {line}
                  </div>
                ))
              )}
              <div ref={logEndRef} />
            </div>
          </div>
        )}

        {/* ── Results Card ── */}
        {(isDone || results.length > 0) && (
          <div className="bg-card border border-border rounded-lg p-5 flex-1 flex flex-col overflow-hidden">
            <div className="flex justify-between items-center mb-3">
              <h2 className="m-0 text-[15px] text-foreground">
                Results {results.length > 0 && <span className="text-muted-foreground/50 font-normal">({results.length} groups)</span>}
              </h2>
            </div>

            {results.length === 0 ? (
              <div className="text-muted-foreground/50 text-[13px] p-5 text-center">
                No results yet.
              </div>
            ) : (
              <div className="flex-1 overflow-auto">
                <table className="w-full border-collapse text-[13px]">
                  <thead>
                    <tr className="text-muted-foreground text-left border-b border-border">
                      <th className="px-3 py-2 font-medium w-6"></th>
                      <th className="px-3 py-2 font-medium">Folder</th>
                      <th className="px-3 py-2 font-medium">Models</th>
                      <th className="px-3 py-2 font-medium">Textures</th>
                      <th className="px-3 py-2 font-medium">Clips</th>
                      <th className="px-3 py-2 font-medium"></th>
                    </tr>
                  </thead>
                  <tbody>
                    {results.map((group, i) => (
                      <ResultRow
                        key={group.folderName}
                        group={group}
                        onToggle={() => toggleResult(i)}
                        onOpenExplorer={() => handleOpenExplorer(group.folderName)}
                      />
                    ))}
                  </tbody>
                </table>
              </div>
            )}
          </div>
        )}
      </div>
    </div>
  )
}

// ---------------------------------------------------------------------------
// Result row sub-component
// ---------------------------------------------------------------------------

function ResultRow({
  group,
  onToggle,
  onOpenExplorer,
}: {
  group: ExtractedGroup
  onToggle: () => void
  onOpenExplorer: () => void
}) {
  return (
    <>
      <tr
        className="border-b border-border cursor-pointer hover:bg-muted"
        onClick={onToggle}
      >
        <td className="px-3 py-2 text-muted-foreground/50 text-[11px]">
          {group.expanded ? '\u25BC' : '\u25B6'}
        </td>
        <td className="px-3 py-2 text-foreground font-mono">
          {group.folderName}
        </td>
        <td className="px-3 py-2 text-muted-foreground">{group.modelCount}</td>
        <td className="px-3 py-2 text-muted-foreground">{group.textureCount}</td>
        <td className="px-3 py-2 text-muted-foreground">{group.clipCount}</td>
        <td className="px-3 py-2">
          <button
            onClick={e => { e.stopPropagation(); onOpenExplorer() }}
            className="px-2.5 py-1 bg-input text-muted-foreground border border-border rounded cursor-pointer text-[11px] hover:bg-muted"
          >
            Open in Explorer
          </button>
        </td>
      </tr>
      {group.expanded && (
        <tr>
          <td colSpan={6} className="py-0 px-3 pb-3 pl-10 bg-background">
            <div className="font-mono text-[11px] leading-relaxed text-muted-foreground py-2">
              {group.files.map(f => (
                <div key={f} className={fileColorClass(f)} style={{ whiteSpace: 'pre-wrap' }}>
                  {f}
                </div>
              ))}
            </div>
          </td>
        </tr>
      )}
    </>
  )
}
