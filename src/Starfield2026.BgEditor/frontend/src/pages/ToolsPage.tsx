import { useState, useEffect, useCallback } from 'react'
import { useNavigate } from 'react-router-dom'
import { useEditorStore } from '../store/editorStore'

const API_BASE = 'http://localhost:3001'

interface ManifestEntry {
  name: string
  dir: string
  assetsPath: string
  modelFile: string
  modelFormat: string
  textures: string[]
}

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

export default function ToolsPage() {
  const [manifests, setManifests] = useState<ManifestEntry[]>([])
  const [generating, setGenerating] = useState(false)
  const [lastResult, setLastResult] = useState<{ generated: number; timestamp: string } | null>(null)
  const [loading, setLoading] = useState(true)
  const [filter, setFilter] = useState('')
  const [error, setError] = useState<string | null>(null)
  const navigate = useNavigate()
  const loadManifest = useEditorStore(s => s.loadManifest)

  // Generator settings
  const [inputDir, setInputDir] = useState('')
  const [outputDir, setOutputDir] = useState('')
  const [sameAsInput, setSameAsInput] = useState(true)
  const [overwrite, setOverwrite] = useState(true)
  const [formats, setFormats] = useState<Record<string, boolean>>({
    fbx: true,
    dae: true,
    obj: true,
  })
  const [settingsLoaded, setSettingsLoaded] = useState(false)

  // Load persisted settings on mount
  useEffect(() => {
    window.electronAPI.storeGetAll().then((all) => {
      if (all.manifestInputDir) setInputDir(all.manifestInputDir as string)
      if (all.manifestOutputDir) setOutputDir(all.manifestOutputDir as string)
      if (all.manifestSameAsInput !== undefined) setSameAsInput(all.manifestSameAsInput as boolean)
      if (all.manifestOverwrite !== undefined) setOverwrite(all.manifestOverwrite as boolean)
      if (all.manifestFormats) setFormats(all.manifestFormats as Record<string, boolean>)
      setSettingsLoaded(true)
    })
  }, [])

  // Persist settings on change (skip until initial load completes)
  useEffect(() => {
    if (!settingsLoaded) return
    window.electronAPI.storeSet('manifestInputDir', inputDir)
  }, [inputDir, settingsLoaded])

  useEffect(() => {
    if (!settingsLoaded) return
    window.electronAPI.storeSet('manifestOutputDir', outputDir)
  }, [outputDir, settingsLoaded])

  useEffect(() => {
    if (!settingsLoaded) return
    window.electronAPI.storeSet('manifestSameAsInput', sameAsInput)
  }, [sameAsInput, settingsLoaded])

  useEffect(() => {
    if (!settingsLoaded) return
    window.electronAPI.storeSet('manifestOverwrite', overwrite)
  }, [overwrite, settingsLoaded])

  useEffect(() => {
    if (!settingsLoaded) return
    window.electronAPI.storeSet('manifestFormats', formats)
  }, [formats, settingsLoaded])

  const fetchManifests = useCallback(async () => {
    setLoading(true)
    try {
      const dir = sameAsInput ? inputDir : outputDir
      const url = dir ? `${API_BASE}/api/manifests?dir=${encodeURIComponent(dir)}` : `${API_BASE}/api/manifests`
      const res = await fetch(url)
      const data = await res.json()
      setManifests(data)
      setError(null)
    } catch (err) {
      console.error('Failed to fetch manifests:', err)
    } finally {
      setLoading(false)
    }
  }, [inputDir, outputDir, sameAsInput])

  useEffect(() => {
    if (inputDir) fetchManifests()
  }, [fetchManifests, inputDir])

  const handleGenerate = async () => {
    setGenerating(true)
    setError(null)
    try {
      const selectedFormats = Object.entries(formats).filter(([, v]) => v).map(([k]) => k)
      const body: Record<string, unknown> = {
        inputDir,
        formats: selectedFormats,
        overwrite,
      }
      if (!sameAsInput && outputDir) {
        body.outputDir = outputDir
      }

      const res = await fetch(`${API_BASE}/api/manifests/generate`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify(body),
      })
      const data = await res.json()
      if (data.error) {
        setError(data.error)
      } else {
        setLastResult({ generated: data.generated, timestamp: new Date().toLocaleTimeString() })
      }
      await fetchManifests()
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Request failed')
    } finally {
      setGenerating(false)
    }
  }

  const handleLoadManifest = async (manifest: ManifestEntry) => {
    const blob = new Blob([JSON.stringify(manifest)], { type: 'application/json' })
    const file = new File([blob], 'manifest.json')
    await loadManifest(file)
    navigate('/')
  }

  const handleBrowseInput = async () => {
    const picked = await window.electronAPI.browseFolder(inputDir)
    if (picked) setInputDir(picked)
  }

  const handleBrowseOutput = async () => {
    const picked = await window.electronAPI.browseFolder(outputDir || inputDir)
    if (picked) setOutputDir(picked)
  }

  const toggleFormat = (fmt: string) => {
    setFormats(prev => ({ ...prev, [fmt]: !prev[fmt] }))
  }

  const filtered = filter
    ? manifests.filter(m => m.name.toLowerCase().includes(filter.toLowerCase()))
    : manifests

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
        <h1 style={{ margin: 0, fontSize: 18, color: '#e0e0e0' }}>Tools</h1>
      </div>

      <div style={{
        flex: 1,
        overflow: 'auto',
        padding: 24,
        display: 'flex',
        flexDirection: 'column',
        gap: 24,
      }}>
        {/* Manifest Generator Card */}
        <div style={{
          background: '#16162a',
          border: '1px solid #2a2a4a',
          borderRadius: 8,
          padding: 20,
        }}>
          <h2 style={{ margin: '0 0 16px', fontSize: 15, color: '#e0e0e0' }}>Manifest Generator</h2>

          {/* Input Directory */}
          <div style={{ marginBottom: 14 }}>
            <label style={labelStyle}>Input Directory</label>
            <div style={{ display: 'flex', gap: 8 }}>
              <input
                type="text"
                value={inputDir}
                onChange={e => setInputDir(e.target.value)}
                placeholder="Path to scan for model folders..."
                style={{ ...inputStyle, flex: 1 }}
              />
              <button onClick={handleBrowseInput} style={browseButtonStyle}>Browse...</button>
            </div>
          </div>

          {/* Output Directory */}
          <div style={{ marginBottom: 14 }}>
            <label style={{ ...labelStyle, display: 'flex', alignItems: 'center', gap: 8 }}>
              Output Directory
              <label style={checkboxLabelStyle}>
                <input
                  type="checkbox"
                  checked={sameAsInput}
                  onChange={e => setSameAsInput(e.target.checked)}
                />
                Same as input
              </label>
            </label>
            <div style={{ display: 'flex', gap: 8 }}>
              <input
                type="text"
                value={sameAsInput ? inputDir : outputDir}
                onChange={e => setOutputDir(e.target.value)}
                disabled={sameAsInput}
                style={{ ...inputStyle, flex: 1, opacity: sameAsInput ? 0.5 : 1 }}
              />
              <button onClick={handleBrowseOutput} disabled={sameAsInput} style={{ ...browseButtonStyle, opacity: sameAsInput ? 0.5 : 1 }}>Browse...</button>
            </div>
          </div>

          {/* Options Row */}
          <div style={{ display: 'flex', gap: 24, marginBottom: 18, flexWrap: 'wrap' }}>
            {/* Model Formats */}
            <div>
              <label style={labelStyle}>Model Formats</label>
              <div style={{ display: 'flex', gap: 12 }}>
                {Object.keys(formats).map(fmt => (
                  <label key={fmt} style={checkboxLabelStyle}>
                    <input
                      type="checkbox"
                      checked={formats[fmt]}
                      onChange={() => toggleFormat(fmt)}
                    />
                    .{fmt}
                  </label>
                ))}
              </div>
            </div>

            {/* Overwrite */}
            <div>
              <label style={labelStyle}>Options</label>
              <label style={checkboxLabelStyle}>
                <input
                  type="checkbox"
                  checked={overwrite}
                  onChange={e => setOverwrite(e.target.checked)}
                />
                Overwrite existing manifests
              </label>
            </div>
          </div>

          {/* Generate Button + Status */}
          <div style={{ display: 'flex', alignItems: 'center', gap: 16 }}>
            <button
              onClick={handleGenerate}
              disabled={generating || !inputDir}
              style={{
                padding: '8px 20px',
                background: (generating || !inputDir) ? '#333' : '#8c8cff',
                color: '#fff',
                border: 'none',
                borderRadius: 4,
                cursor: (generating || !inputDir) ? 'default' : 'pointer',
                fontSize: 13,
                fontWeight: 600,
              }}
            >
              {generating ? 'Generating...' : 'Generate Manifests'}
            </button>

            {lastResult && !error && (
              <span style={{ fontSize: 13, color: '#8c8cff' }}>
                Generated {lastResult.generated} manifests at {lastResult.timestamp}
              </span>
            )}
            {error && (
              <span style={{ fontSize: 13, color: '#ff6666' }}>{error}</span>
            )}
          </div>
        </div>

        {/* Manifest List Card */}
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
              Manifests {!loading && <span style={{ color: '#666', fontWeight: 400 }}>({manifests.length})</span>}
            </h2>
            <div style={{ display: 'flex', gap: 8, alignItems: 'center' }}>
              <input
                type="text"
                placeholder="Filter..."
                value={filter}
                onChange={e => setFilter(e.target.value)}
                style={{ ...inputStyle, width: 200 }}
              />
              <button
                onClick={fetchManifests}
                style={{
                  padding: '6px 14px',
                  background: '#2a2a4a',
                  color: '#ccc',
                  border: '1px solid #3a3a5a',
                  borderRadius: 4,
                  cursor: 'pointer',
                  fontSize: 13,
                  whiteSpace: 'nowrap',
                }}
              >
                Refresh
              </button>
            </div>
          </div>

          {loading ? (
            <div style={{ color: '#666', fontSize: 13, padding: 20, textAlign: 'center' }}>Loading...</div>
          ) : filtered.length === 0 ? (
            <div style={{ color: '#666', fontSize: 13, padding: 20, textAlign: 'center' }}>
              {manifests.length === 0 ? 'No manifests found. Configure the settings above and click "Generate Manifests".' : 'No matches.'}
            </div>
          ) : (
            <div style={{ flex: 1, overflow: 'auto' }}>
              <table style={{ width: '100%', borderCollapse: 'collapse', fontSize: 13 }}>
                <thead>
                  <tr style={{ color: '#888', textAlign: 'left', borderBottom: '1px solid #2a2a4a' }}>
                    <th style={{ padding: '8px 12px', fontWeight: 500 }}>Name</th>
                    <th style={{ padding: '8px 12px', fontWeight: 500 }}>Format</th>
                    <th style={{ padding: '8px 12px', fontWeight: 500 }}>Textures</th>
                    <th style={{ padding: '8px 12px', fontWeight: 500 }}>Path</th>
                    <th style={{ padding: '8px 12px', fontWeight: 500 }}></th>
                  </tr>
                </thead>
                <tbody>
                  {filtered.map(m => (
                    <tr
                      key={m.assetsPath}
                      style={{ borderBottom: '1px solid #1e1e3a', cursor: 'pointer' }}
                      onClick={() => handleLoadManifest(m)}
                      onMouseEnter={e => (e.currentTarget.style.background = '#1e1e3a')}
                      onMouseLeave={e => (e.currentTarget.style.background = 'transparent')}
                    >
                      <td style={{ padding: '8px 12px', color: '#e0e0e0' }}>{m.name}</td>
                      <td style={{ padding: '8px 12px', color: '#888' }}>{m.modelFormat.toUpperCase()}</td>
                      <td style={{ padding: '8px 12px', color: '#888' }}>{m.textures.length}</td>
                      <td style={{ padding: '8px 12px', color: '#666', maxWidth: 300, overflow: 'hidden', textOverflow: 'ellipsis', whiteSpace: 'nowrap' }}>{m.assetsPath}</td>
                      <td style={{ padding: '8px 12px' }}>
                        <span style={{ color: '#8c8cff', fontSize: 12 }}>Open</span>
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          )}
        </div>
      </div>
    </div>
  )
}
