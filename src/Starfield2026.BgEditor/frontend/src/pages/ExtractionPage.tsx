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
    case 'parsing': return 'linear-gradient(90deg, #569cd6, #7ab4e6)'
    case 'grouping': return 'linear-gradient(90deg, #7c5cf7, #a77cff)'
    case 'exporting': return 'linear-gradient(90deg, #3bb078, #55cc88)'
    case 'done': return 'linear-gradient(90deg, #33cc66, #55ee88)'
    case 'error': return 'linear-gradient(90deg, #c74e4e, #e06060)'
    case 'stopped': return 'linear-gradient(90deg, #cc8833, #ffaa44)'
    default: return 'linear-gradient(90deg, #569cd6, #7ab4e6)'
  }
}

function phaseGlowColor(phase: ExtractionPhase): string {
  switch (phase) {
    case 'parsing': return 'rgba(86, 156, 214, 0.4)'
    case 'grouping': return 'rgba(124, 92, 247, 0.4)'
    case 'exporting': return 'rgba(59, 176, 120, 0.4)'
    case 'done': return 'rgba(51, 204, 102, 0.4)'
    case 'error': return 'rgba(199, 78, 78, 0.4)'
    case 'stopped': return 'rgba(204, 136, 51, 0.4)'
    default: return 'rgba(86, 156, 214, 0.2)'
  }
}

function phaseTextColor(phase: ExtractionPhase): string {
  switch (phase) {
    case 'done': return 'var(--color-success)'
    case 'error': return 'var(--color-danger)'
    case 'stopped': return 'var(--color-warning)'
    case 'parsing': return 'var(--color-accent)'
    case 'grouping': return '#a77cff'
    case 'exporting': return 'var(--color-success)'
    default: return 'var(--color-text-secondary)'
  }
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
      <div className="px-[24px] py-[16px] bg-surface border-b border-border">
        <h1 className="m-0 text-[18px] text-text">GARC Extraction</h1>
      </div>

      <div className="flex-1 overflow-auto p-[24px] flex flex-col gap-[24px]">
        {/* ── Source + Output Card ── */}
        <div className="bg-surface border border-border rounded-[8px] p-[20px]">
          <h2 className="m-0 mb-[16px] text-[15px] text-text">Extraction Settings</h2>

          {/* RomFS base path */}
          <div className="mb-[14px]">
            <label className="text-[12px] text-text-secondary mb-[4px] block">RomFS Base Path</label>
            <div className="flex gap-[8px]">
              <input
                type="text"
                value={garcPath}
                onChange={e => setGarcPath(e.target.value)}
                placeholder="Path to RomFS root (e.g. D:/dump/RomFS)..."
                className="flex-1 px-[12px] py-[8px] bg-input border border-border rounded text-text text-[13px] outline-none"
              />
              <button onClick={handleBrowseGarc} className="px-[16px] py-[8px] bg-input text-text border border-border rounded cursor-pointer text-[13px] whitespace-nowrap hover:bg-hover">Browse...</button>
            </div>
          </div>

          {/* GARC Archive dropdown */}
          <div className="mb-[14px]">
            <label className="text-[12px] text-text-secondary mb-[4px] block">
              GARC Archive
              {scanning && <span className="text-accent ml-[8px]">Scanning...</span>}
              {!scanning && scannedArchives.length > 0 && (
                <span className="text-text-disabled ml-[8px]">
                  {scannedArchives.length} archives found
                </span>
              )}
            </label>
            {scanError && (
              <div className="text-[12px] text-danger mb-[6px]">{scanError}</div>
            )}
            <select
              value={selectedSubpath}
              onChange={e => setSelectedSubpath(e.target.value)}
              disabled={scannedArchives.length === 0}
              className="w-full px-[12px] py-[8px] bg-input border border-border rounded text-text text-[13px] outline-none disabled:opacity-50"
            >
              {scannedArchives.length === 0 ? (
                <option value="">
                  {garcPath ? (scanning ? 'Scanning...' : 'No archives found') : 'Enter RomFS path to scan'}
                </option>
              ) : (
                scannedArchives.map(a => (
                  <option key={a.subpath} value={a.subpath} style={{ background: 'var(--color-input)', color: 'var(--color-text)' }}>
                    {a.subpath}  ({a.sizeLabel})
                  </option>
                ))
              )}
            </select>
          </div>

          {/* Resolved full path preview */}
          {garcPath && selectedSubpath && (
            <div className="text-[12px] text-text-disabled mb-[16px] px-[10px] py-[6px] bg-bg rounded border border-border font-mono">
              Full path: <span className="text-accent">{fullGarcPath}</span>
            </div>
          )}

          {/* Output Directory */}
          <div className="mb-[14px]">
            <label className="text-[12px] text-text-secondary mb-[4px] block">Output Directory</label>
            <div className="flex gap-[8px]">
              <input
                type="text"
                value={outputDir}
                onChange={e => setOutputDir(e.target.value)}
                placeholder="Directory for extracted files..."
                className="flex-1 px-[12px] py-[8px] bg-input border border-border rounded text-text text-[13px] outline-none"
              />
              <button onClick={handleBrowseOutput} className="px-[16px] py-[8px] bg-input text-text border border-border rounded cursor-pointer text-[13px] whitespace-nowrap hover:bg-hover">Browse...</button>
            </div>
          </div>

          {/* Options Row */}
          <div className="flex gap-[24px] mb-[18px] flex-wrap">
            <div>
              <label className="text-[12px] text-text-secondary mb-[4px] block">Export Mode</label>
              <div className="flex gap-[12px]">
                <label className="text-[13px] text-text cursor-pointer flex items-center gap-[6px]">
                  <input type="radio" name="exportMode" checked={exportMode === 'split'} onChange={() => setExportMode('split')} />
                  Split (mesh DAE + clip DAEs)
                </label>
                <label className="text-[13px] text-text cursor-pointer flex items-center gap-[6px]">
                  <input type="radio" name="exportMode" checked={exportMode === 'individual'} onChange={() => setExportMode('individual')} />
                  Individual (baked DAEs)
                </label>
              </div>
              {exportMode === 'split' && (
                <div className="text-[11px] text-text-disabled mt-[4px]">Recommended. One mesh-only DAE + separate animation clip DAEs per model.</div>
              )}
              {exportMode === 'individual' && (
                <div className="text-[11px] text-text-disabled mt-[4px]">One DAE per model with all animations baked in. No manifest or clip files.</div>
              )}
            </div>
            <div className="w-[130px]">
              <label className="text-[12px] text-text-secondary mb-[4px] block">Entry Limit</label>
              <input
                type="number"
                value={entryLimit}
                onChange={e => setEntryLimit(e.target.value)}
                placeholder="e.g. 100"
                min={1}
                className="w-full px-[12px] py-[8px] bg-input border border-border rounded text-text text-[13px] outline-none"
              />
            </div>
            <div className="flex items-end">
              <label className="text-[13px] text-text cursor-pointer flex items-center gap-[6px]">
                <input type="checkbox" checked={deriveFolderNames} onChange={e => setDeriveFolderNames(e.target.checked)} />
                Derive folder names from textures
              </label>
            </div>
          </div>

          {/* Start / Stop buttons + status */}
          <div className="flex items-center gap-[12px]">
            <button
              onClick={handleStart}
              disabled={!canStart}
              className="px-[20px] py-[8px] rounded text-[13px] font-semibold border-none cursor-pointer disabled:cursor-default"
              style={{
                background: !canStart ? 'var(--color-input)' : 'var(--color-accent)',
                color: !canStart ? 'var(--color-text-disabled)' : '#fff',
              }}
            >
              {isRunning ? 'Running...' : 'Start Extraction'}
            </button>
            <button
              onClick={handleStop}
              disabled={!isRunning}
              className="px-[20px] py-[8px] rounded text-[13px] font-semibold border-none cursor-pointer disabled:cursor-default disabled:opacity-50"
              style={{
                background: !isRunning ? 'var(--color-input)' : 'var(--color-danger)',
                color: '#fff',
              }}
            >
              Stop
            </button>
            {isDone && !isRunning && phase === 'done' && (
              <span className="text-[13px] text-success">
                Extracted {results.length} groups in {elapsedSeconds.toFixed(1)}s
              </span>
            )}
            {isDone && phase === 'error' && (
              <span className="text-[13px] text-danger">Extraction failed</span>
            )}
            {isDone && phase === 'stopped' && (
              <span className="text-[13px] text-warning">Stopped by user</span>
            )}
          </div>
        </div>

        {/* ── Progress Card ── */}
        {(isRunning || isDone) && (
          <div className="bg-surface border border-border rounded-[8px] p-[20px]">
            <div className="flex justify-between items-center mb-[14px]">
              <h2 className="m-0 text-[15px] text-text">Progress</h2>
              <span className="text-[12px] text-text-secondary">
                {stats.totalEntries > 0
                  ? `${stats.processedEntries.toLocaleString()} / ${stats.totalEntries.toLocaleString()} entries`
                  : '\u2014'}
                {elapsedSeconds > 0 && (
                  <span className="ml-[12px]">{elapsedSeconds.toFixed(1)}s</span>
                )}
              </span>
            </div>

            {/* Progress bar */}
            <div className="mb-[14px]">
              <div className="flex justify-between items-baseline mb-[6px]">
                <span className="text-[13px] font-medium" style={{ color: phaseTextColor(phase) }}>
                  {phaseLabel(phase)}
                </span>
                <span className="text-[13px] text-text font-semibold" style={{ fontVariantNumeric: 'tabular-nums' }}>
                  {progressPercent}%
                </span>
              </div>
              <div className="h-[10px] bg-bg rounded-[5px] overflow-hidden relative">
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
            <div className="grid gap-[8px] mb-[14px]" style={{ gridTemplateColumns: 'repeat(auto-fill, minmax(140px, 1fr))' }}>
              {[
                { label: 'Groups Found', value: stats.groupsFound },
                { label: 'Models Exported', value: stats.modelsExported },
                { label: 'Textures Exported', value: stats.texturesExported },
                { label: 'Clips Exported', value: stats.clipsExported },
                { label: 'Parse Errors', value: stats.parseErrors, warn: true },
                { label: 'Export Errors', value: stats.exportErrors, warn: true },
              ].map(stat => (
                <div key={stat.label} className="bg-bg rounded p-[8px_12px]">
                  <div className="text-[11px] text-text-disabled mb-[2px]">{stat.label}</div>
                  <div className="text-[16px] font-semibold"
                    style={{ color: stat.warn && stat.value > 0 ? 'var(--color-warning)' : 'var(--color-text)' }}
                  >
                    {stat.value.toLocaleString()}
                  </div>
                </div>
              ))}
            </div>

            {/* Log area */}
            <div className="bg-bg border border-border rounded p-[12px] max-h-[200px] overflow-y-auto font-mono text-[11px] leading-[1.6] text-text-secondary">
              {logLines.length === 0 ? (
                <div className="text-text-disabled">Waiting to start...</div>
              ) : (
                logLines.map((line, i) => (
                  <div key={i} style={{
                    color: line.startsWith('===') ? 'var(--color-success)'
                      : line.startsWith('  Error') || line.includes('failed') ? 'var(--color-danger)'
                        : line.startsWith('---') ? 'var(--color-warning)'
                          : line.startsWith('Phase') ? 'var(--color-accent)'
                            : 'var(--color-text-secondary)',
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
          <div className="bg-surface border border-border rounded-[8px] p-[20px] flex-1 flex flex-col overflow-hidden">
            <div className="flex justify-between items-center mb-[12px]">
              <h2 className="m-0 text-[15px] text-text">
                Results {results.length > 0 && <span className="text-text-disabled font-normal">({results.length} groups)</span>}
              </h2>
            </div>

            {results.length === 0 ? (
              <div className="text-text-disabled text-[13px] p-[20px] text-center">
                No results yet.
              </div>
            ) : (
              <div className="flex-1 overflow-auto">
                <table className="w-full border-collapse text-[13px]">
                  <thead>
                    <tr className="text-text-secondary text-left border-b border-border">
                      <th className="p-[8px_12px] font-medium w-[24px]"></th>
                      <th className="p-[8px_12px] font-medium">Folder</th>
                      <th className="p-[8px_12px] font-medium">Models</th>
                      <th className="p-[8px_12px] font-medium">Textures</th>
                      <th className="p-[8px_12px] font-medium">Clips</th>
                      <th className="p-[8px_12px] font-medium"></th>
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
        className="border-b border-border cursor-pointer hover:bg-hover"
        onClick={onToggle}
      >
        <td className="p-[8px_12px] text-text-disabled text-[11px]">
          {group.expanded ? '\u25BC' : '\u25B6'}
        </td>
        <td className="p-[8px_12px] text-text font-mono">
          {group.folderName}
        </td>
        <td className="p-[8px_12px] text-text-secondary">{group.modelCount}</td>
        <td className="p-[8px_12px] text-text-secondary">{group.textureCount}</td>
        <td className="p-[8px_12px] text-text-secondary">{group.clipCount}</td>
        <td className="p-[8px_12px]">
          <button
            onClick={e => { e.stopPropagation(); onOpenExplorer() }}
            className="px-[10px] py-[3px] bg-input text-text-secondary border border-border rounded-[3px] cursor-pointer text-[11px] hover:bg-hover"
          >
            Open in Explorer
          </button>
        </td>
      </tr>
      {group.expanded && (
        <tr>
          <td colSpan={6} className="p-[0_12px_12px_40px] bg-bg">
            <div className="font-mono text-[11px] leading-[1.8] text-text-secondary py-[8px]">
              {group.files.map(f => (
                <div key={f} style={{
                  color: f.endsWith('.dae') ? 'var(--color-accent)'
                    : f.endsWith('.png') ? 'var(--color-success)'
                      : f.endsWith('.json') ? 'var(--color-warning)'
                        : 'var(--color-text-secondary)',
                  whiteSpace: 'pre-wrap',
                }}>
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
