import { useState, useRef, useEffect, useCallback } from 'react'
import type {
  ExportMode,
  ExtractionPhase,
  ExtractedGroup,
} from '../types/extraction'
import { useExtractionStore } from '../store/extractionStore'
import { scanArchives, type ScannedArchive } from '../services/extractionService'

// ---------------------------------------------------------------------------
// Shared styles (matching ToolsPage conventions)
// ---------------------------------------------------------------------------

const inputStyle: React.CSSProperties = {
  padding: '8px 12px',
  background: '#2a2a4a',
  border: '1px solid #3a3a5a',
  borderRadius: 4,
  color: '#e0e0e0',
  fontSize: 13,
  outline: 'none',
  width: '100%',
  boxSizing: 'border-box',
}

const labelStyle: React.CSSProperties = {
  fontSize: 12,
  color: '#888',
  marginBottom: 4,
  display: 'block',
}

const checkboxLabelStyle: React.CSSProperties = {
  fontSize: 13,
  color: '#ccc',
  cursor: 'pointer',
  display: 'flex',
  alignItems: 'center',
  gap: 6,
}

const browseButtonStyle: React.CSSProperties = {
  padding: '8px 16px',
  background: '#2a2a4a',
  color: '#ccc',
  border: '1px solid #3a3a5a',
  borderRadius: 4,
  cursor: 'pointer',
  fontSize: 13,
  whiteSpace: 'nowrap',
}

const selectStyle: React.CSSProperties = {
  padding: '8px 12px',
  background: '#2a2a4a',
  border: '1px solid #3a3a5a',
  borderRadius: 4,
  color: '#e0e0e0',
  fontSize: 13,
  outline: 'none',
  width: '100%',
  boxSizing: 'border-box',
}

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
    case 'parsing':   return 'linear-gradient(90deg, #4a6cf7, #6b8cff)'
    case 'grouping':  return 'linear-gradient(90deg, #7c5cf7, #a77cff)'
    case 'exporting': return 'linear-gradient(90deg, #3bb078, #55cc88)'
    case 'done':      return 'linear-gradient(90deg, #33cc66, #55ee88)'
    case 'error':     return 'linear-gradient(90deg, #e04040, #ff5555)'
    case 'stopped':   return 'linear-gradient(90deg, #cc8833, #ffaa44)'
    default:          return 'linear-gradient(90deg, #4a6cf7, #6b8cff)'
  }
}

function phaseGlowColor(phase: ExtractionPhase): string {
  switch (phase) {
    case 'parsing':   return 'rgba(74, 108, 247, 0.4)'
    case 'grouping':  return 'rgba(124, 92, 247, 0.4)'
    case 'exporting': return 'rgba(59, 176, 120, 0.4)'
    case 'done':      return 'rgba(51, 204, 102, 0.4)'
    case 'error':     return 'rgba(224, 64, 64, 0.4)'
    case 'stopped':   return 'rgba(204, 136, 51, 0.4)'
    default:          return 'rgba(74, 108, 247, 0.2)'
  }
}

function phaseTextColor(phase: ExtractionPhase): string {
  switch (phase) {
    case 'done': return '#55cc55'
    case 'error': return '#ff5555'
    case 'stopped': return '#ffaa33'
    case 'parsing': return '#6b8cff'
    case 'grouping': return '#a77cff'
    case 'exporting': return '#55cc88'
    default: return '#888'
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
        // Auto-select first archive if none selected
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
    <div style={{
      display: 'flex',
      flexDirection: 'column',
      height: '100%',
      overflow: 'hidden',
    }}>
      {/* Header */}
      <div style={{
        padding: '16px 24px',
        background: '#12122a',
        borderBottom: '1px solid #2a2a4a',
      }}>
        <h1 style={{ margin: 0, fontSize: 18, color: '#e0e0e0' }}>GARC Extraction</h1>
      </div>

      <div style={{
        flex: 1,
        overflow: 'auto',
        padding: 24,
        display: 'flex',
        flexDirection: 'column',
        gap: 24,
      }}>
        {/* ── Source + Output Card ── */}
        <div style={{
          background: '#16162a',
          border: '1px solid #2a2a4a',
          borderRadius: 8,
          padding: 20,
        }}>
          <h2 style={{ margin: '0 0 16px', fontSize: 15, color: '#e0e0e0' }}>Extraction Settings</h2>

          {/* RomFS base path */}
          <div style={{ marginBottom: 14 }}>
            <label style={labelStyle}>RomFS Base Path</label>
            <div style={{ display: 'flex', gap: 8 }}>
              <input
                type="text"
                value={garcPath}
                onChange={e => setGarcPath(e.target.value)}
                placeholder="Path to RomFS root (e.g. D:/dump/RomFS)..."
                style={{ ...inputStyle, flex: 1 }}
              />
              <button onClick={handleBrowseGarc} style={browseButtonStyle}>Browse...</button>
            </div>
          </div>

          {/* GARC Archive dropdown — dynamically scanned */}
          <div style={{ marginBottom: 14 }}>
            <label style={labelStyle}>
              GARC Archive
              {scanning && <span style={{ color: '#6b8cff', marginLeft: 8 }}>Scanning...</span>}
              {!scanning && scannedArchives.length > 0 && (
                <span style={{ color: '#666', marginLeft: 8 }}>
                  {scannedArchives.length} archives found
                </span>
              )}
            </label>
            {scanError && (
              <div style={{ fontSize: 12, color: '#ff6666', marginBottom: 6 }}>{scanError}</div>
            )}
            <select
              value={selectedSubpath}
              onChange={e => setSelectedSubpath(e.target.value)}
              disabled={scannedArchives.length === 0}
              style={{
                ...selectStyle,
                opacity: scannedArchives.length === 0 ? 0.5 : 1,
              }}
            >
              {scannedArchives.length === 0 ? (
                <option value="">
                  {garcPath ? (scanning ? 'Scanning...' : 'No archives found') : 'Enter RomFS path to scan'}
                </option>
              ) : (
                scannedArchives.map(a => (
                  <option key={a.subpath} value={a.subpath} style={{ background: '#2a2a4a', color: '#e0e0e0' }}>
                    {a.subpath}  ({a.sizeLabel})
                  </option>
                ))
              )}
            </select>
          </div>

          {/* Resolved full path preview */}
          {garcPath && selectedSubpath && (
            <div style={{
              fontSize: 12,
              color: '#555',
              marginBottom: 16,
              padding: '6px 10px',
              background: '#0e0e1e',
              borderRadius: 4,
              border: '1px solid #1e1e3a',
              fontFamily: 'Consolas, "Courier New", monospace',
            }}>
              Full path: <span style={{ color: '#8c8cff' }}>{fullGarcPath}</span>
            </div>
          )}

          {/* Output Directory */}
          <div style={{ marginBottom: 14 }}>
            <label style={labelStyle}>Output Directory</label>
            <div style={{ display: 'flex', gap: 8 }}>
              <input
                type="text"
                value={outputDir}
                onChange={e => setOutputDir(e.target.value)}
                placeholder="Directory for extracted files..."
                style={{ ...inputStyle, flex: 1 }}
              />
              <button onClick={handleBrowseOutput} style={browseButtonStyle}>Browse...</button>
            </div>
          </div>

          {/* Options Row */}
          <div style={{ display: 'flex', gap: 24, marginBottom: 18, flexWrap: 'wrap' }}>
            {/* Export mode */}
            <div>
              <label style={labelStyle}>Export Mode</label>
              <div style={{ display: 'flex', gap: 12 }}>
                <label style={checkboxLabelStyle}>
                  <input
                    type="radio"
                    name="exportMode"
                    checked={exportMode === 'split'}
                    onChange={() => setExportMode('split')}
                  />
                  Split (mesh DAE + clip DAEs)
                </label>
                <label style={checkboxLabelStyle}>
                  <input
                    type="radio"
                    name="exportMode"
                    checked={exportMode === 'individual'}
                    onChange={() => setExportMode('individual')}
                  />
                  Individual (baked DAEs)
                </label>
              </div>
              {exportMode === 'split' && (
                <div style={{ fontSize: 11, color: '#666', marginTop: 4 }}>Recommended. One mesh-only DAE + separate animation clip DAEs per model.</div>
              )}
              {exportMode === 'individual' && (
                <div style={{ fontSize: 11, color: '#666', marginTop: 4 }}>One DAE per model with all animations baked in. No manifest or clip files.</div>
              )}
            </div>

            {/* Entry limit */}
            <div style={{ width: 130 }}>
              <label style={labelStyle}>Entry Limit</label>
              <input
                type="number"
                value={entryLimit}
                onChange={e => setEntryLimit(e.target.value)}
                placeholder="e.g. 100"
                min={1}
                style={inputStyle}
              />
            </div>

            {/* Derive folder names */}
            <div style={{ display: 'flex', alignItems: 'flex-end' }}>
              <label style={checkboxLabelStyle}>
                <input
                  type="checkbox"
                  checked={deriveFolderNames}
                  onChange={e => setDeriveFolderNames(e.target.checked)}
                />
                Derive folder names from textures
              </label>
            </div>
          </div>

          {/* Start / Stop buttons + status */}
          <div style={{ display: 'flex', alignItems: 'center', gap: 12 }}>
            <button
              onClick={handleStart}
              disabled={!canStart}
              style={{
                padding: '8px 20px',
                background: !canStart ? '#333' : '#8c8cff',
                color: '#fff',
                border: 'none',
                borderRadius: 4,
                cursor: !canStart ? 'default' : 'pointer',
                fontSize: 13,
                fontWeight: 600,
              }}
            >
              {isRunning ? 'Running...' : 'Start Extraction'}
            </button>
            <button
              onClick={handleStop}
              disabled={!isRunning}
              style={{
                padding: '8px 20px',
                background: !isRunning ? '#333' : '#ff5555',
                color: '#fff',
                border: 'none',
                borderRadius: 4,
                cursor: !isRunning ? 'default' : 'pointer',
                fontSize: 13,
                fontWeight: 600,
              }}
            >
              Stop
            </button>

            {isDone && !isRunning && phase === 'done' && (
              <span style={{ fontSize: 13, color: '#55cc55' }}>
                Extracted {results.length} groups in {elapsedSeconds.toFixed(1)}s
              </span>
            )}
            {isDone && phase === 'error' && (
              <span style={{ fontSize: 13, color: '#ff6666' }}>Extraction failed</span>
            )}
            {isDone && phase === 'stopped' && (
              <span style={{ fontSize: 13, color: '#ffaa33' }}>Stopped by user</span>
            )}
          </div>
        </div>

        {/* ── Progress Card ── */}
        {(isRunning || isDone) && (
          <div style={{
            background: '#16162a',
            border: '1px solid #2a2a4a',
            borderRadius: 8,
            padding: 20,
          }}>
            <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: 14 }}>
              <h2 style={{ margin: 0, fontSize: 15, color: '#e0e0e0' }}>Progress</h2>
              <span style={{ fontSize: 12, color: '#888' }}>
                {stats.totalEntries > 0
                  ? `${stats.processedEntries.toLocaleString()} / ${stats.totalEntries.toLocaleString()} entries`
                  : '\u2014'}
                {elapsedSeconds > 0 && (
                  <span style={{ marginLeft: 12 }}>{elapsedSeconds.toFixed(1)}s</span>
                )}
              </span>
            </div>

            {/* Modern progress bar */}
            <div style={{ marginBottom: 14 }}>
              <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'baseline', marginBottom: 6 }}>
                <span style={{ fontSize: 13, color: phaseTextColor(phase), fontWeight: 500 }}>
                  {phaseLabel(phase)}
                </span>
                <span style={{ fontSize: 13, color: '#e0e0e0', fontWeight: 600, fontVariantNumeric: 'tabular-nums' }}>
                  {progressPercent}%
                </span>
              </div>
              <div style={{
                height: 10,
                background: '#1a1a2e',
                borderRadius: 5,
                overflow: 'hidden',
                position: 'relative',
              }}>
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
            <div style={{
              display: 'grid',
              gridTemplateColumns: 'repeat(auto-fill, minmax(140px, 1fr))',
              gap: 8,
              marginBottom: 14,
            }}>
              {[
                { label: 'Groups Found', value: stats.groupsFound },
                { label: 'Models Exported', value: stats.modelsExported },
                { label: 'Textures Exported', value: stats.texturesExported },
                { label: 'Clips Exported', value: stats.clipsExported },
                { label: 'Parse Errors', value: stats.parseErrors, warn: true },
                { label: 'Export Errors', value: stats.exportErrors, warn: true },
              ].map(stat => (
                <div key={stat.label} style={{
                  background: '#1e1e3a',
                  borderRadius: 4,
                  padding: '8px 12px',
                }}>
                  <div style={{ fontSize: 11, color: '#666', marginBottom: 2 }}>{stat.label}</div>
                  <div style={{
                    fontSize: 16,
                    fontWeight: 600,
                    color: stat.warn && stat.value > 0 ? '#ff8855' : '#e0e0e0',
                  }}>
                    {stat.value.toLocaleString()}
                  </div>
                </div>
              ))}
            </div>

            {/* Log area */}
            <div style={{
              background: '#0e0e1e',
              border: '1px solid #2a2a4a',
              borderRadius: 4,
              padding: 12,
              maxHeight: 200,
              overflowY: 'auto',
              fontFamily: 'Consolas, "Courier New", monospace',
              fontSize: 11,
              lineHeight: 1.6,
              color: '#aaa',
            }}>
              {logLines.length === 0 ? (
                <div style={{ color: '#555' }}>Waiting to start...</div>
              ) : (
                logLines.map((line, i) => (
                  <div key={i} style={{
                    color: line.startsWith('===') ? '#55cc55'
                      : line.startsWith('  Error') || line.includes('failed') ? '#ff6666'
                      : line.startsWith('---') ? '#ffaa33'
                      : line.startsWith('Phase') ? '#8c8cff'
                      : '#aaa',
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
          <div style={{
            background: '#16162a',
            border: '1px solid #2a2a4a',
            borderRadius: 8,
            padding: 20,
            flex: 1,
            display: 'flex',
            flexDirection: 'column',
            overflow: 'hidden',
          }}>
            <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: 12 }}>
              <h2 style={{ margin: 0, fontSize: 15, color: '#e0e0e0' }}>
                Results {results.length > 0 && <span style={{ color: '#666', fontWeight: 400 }}>({results.length} groups)</span>}
              </h2>
            </div>

            {results.length === 0 ? (
              <div style={{ color: '#666', fontSize: 13, padding: 20, textAlign: 'center' }}>
                No results yet.
              </div>
            ) : (
              <div style={{ flex: 1, overflow: 'auto' }}>
                <table style={{ width: '100%', borderCollapse: 'collapse', fontSize: 13 }}>
                  <thead>
                    <tr style={{ color: '#888', textAlign: 'left', borderBottom: '1px solid #2a2a4a' }}>
                      <th style={{ padding: '8px 12px', fontWeight: 500, width: 24 }}></th>
                      <th style={{ padding: '8px 12px', fontWeight: 500 }}>Folder</th>
                      <th style={{ padding: '8px 12px', fontWeight: 500 }}>Models</th>
                      <th style={{ padding: '8px 12px', fontWeight: 500 }}>Textures</th>
                      <th style={{ padding: '8px 12px', fontWeight: 500 }}>Clips</th>
                      <th style={{ padding: '8px 12px', fontWeight: 500 }}></th>
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
        style={{ borderBottom: '1px solid #1e1e3a', cursor: 'pointer' }}
        onClick={onToggle}
        onMouseEnter={e => (e.currentTarget.style.background = '#1e1e3a')}
        onMouseLeave={e => (e.currentTarget.style.background = 'transparent')}
      >
        <td style={{ padding: '8px 12px', color: '#666', fontSize: 11 }}>
          {group.expanded ? '\u25BC' : '\u25B6'}
        </td>
        <td style={{ padding: '8px 12px', color: '#e0e0e0', fontFamily: 'Consolas, monospace' }}>
          {group.folderName}
        </td>
        <td style={{ padding: '8px 12px', color: '#888' }}>{group.modelCount}</td>
        <td style={{ padding: '8px 12px', color: '#888' }}>{group.textureCount}</td>
        <td style={{ padding: '8px 12px', color: '#888' }}>{group.clipCount}</td>
        <td style={{ padding: '8px 12px' }}>
          <button
            onClick={e => { e.stopPropagation(); onOpenExplorer() }}
            style={{
              padding: '3px 10px',
              background: '#2a2a4a',
              color: '#aaa',
              border: '1px solid #3a3a5a',
              borderRadius: 3,
              cursor: 'pointer',
              fontSize: 11,
            }}
          >
            Open in Explorer
          </button>
        </td>
      </tr>
      {group.expanded && (
        <tr>
          <td colSpan={6} style={{ padding: '0 12px 12px 40px', background: '#12122a' }}>
            <div style={{
              fontFamily: 'Consolas, "Courier New", monospace',
              fontSize: 11,
              lineHeight: 1.8,
              color: '#888',
              padding: '8px 0',
            }}>
              {group.files.map(f => (
                <div key={f} style={{
                  color: f.endsWith('.dae') ? '#8c8cff'
                    : f.endsWith('.png') ? '#55cc88'
                    : f.endsWith('.json') ? '#ccaa55'
                    : '#888',
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
