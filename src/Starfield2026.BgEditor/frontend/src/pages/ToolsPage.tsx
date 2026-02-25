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
    const api = (window as any).electronAPI
    if (api?.storeGetAll) {
      api.storeGetAll().then((all: any) => {
        if (all.manifestInputDir) setInputDir(all.manifestInputDir as string)
        if (all.manifestOutputDir) setOutputDir(all.manifestOutputDir as string)
        if (all.manifestSameAsInput !== undefined) setSameAsInput(all.manifestSameAsInput as boolean)
        if (all.manifestOverwrite !== undefined) setOverwrite(all.manifestOverwrite as boolean)
        if (all.manifestFormats) setFormats(all.manifestFormats as Record<string, boolean>)
        setSettingsLoaded(true)
      })
    } else {
      setSettingsLoaded(true)
    }
  }, [])

  // Persist settings on change (skip until initial load completes)
  useEffect(() => { if (!settingsLoaded) return; (window as any).electronAPI?.storeSet?.('manifestInputDir', inputDir) }, [inputDir, settingsLoaded])
  useEffect(() => { if (!settingsLoaded) return; (window as any).electronAPI?.storeSet?.('manifestOutputDir', outputDir) }, [outputDir, settingsLoaded])
  useEffect(() => { if (!settingsLoaded) return; (window as any).electronAPI?.storeSet?.('manifestSameAsInput', sameAsInput) }, [sameAsInput, settingsLoaded])
  useEffect(() => { if (!settingsLoaded) return; (window as any).electronAPI?.storeSet?.('manifestOverwrite', overwrite) }, [overwrite, settingsLoaded])
  useEffect(() => { if (!settingsLoaded) return; (window as any).electronAPI?.storeSet?.('manifestFormats', formats) }, [formats, settingsLoaded])

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
      const body: Record<string, unknown> = { inputDir, formats: selectedFormats, overwrite }
      if (!sameAsInput && outputDir) body.outputDir = outputDir

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
    const picked = await (window as any).electronAPI?.browseFolder?.(inputDir)
    if (picked) setInputDir(picked)
  }

  const handleBrowseOutput = async () => {
    const picked = await (window as any).electronAPI?.browseFolder?.(outputDir || inputDir)
    if (picked) setOutputDir(picked)
  }

  const toggleFormat = (fmt: string) => {
    setFormats(prev => ({ ...prev, [fmt]: !prev[fmt] }))
  }

  const filtered = filter
    ? manifests.filter(m => m.name.toLowerCase().includes(filter.toLowerCase()))
    : manifests

  const canGenerate = !generating && !!inputDir

  return (
    <div className="flex flex-col h-full overflow-hidden">
      {/* Header */}
      <div className="px-6 py-4 bg-card border-b border-border">
        <h1 className="m-0 text-lg text-foreground">Tools</h1>
      </div>

      <div className="flex-1 overflow-auto p-6 flex flex-col gap-6">
        {/* Manifest Generator Card */}
        <div className="bg-card border border-border rounded-lg p-5">
          <h2 className="m-0 mb-4 text-[15px] text-foreground">Manifest Generator</h2>

          {/* Input Directory */}
          <div className="mb-3.5">
            <label className="text-xs text-muted-foreground mb-1 block">Input Directory</label>
            <div className="flex gap-2">
              <input
                type="text"
                value={inputDir}
                onChange={e => setInputDir(e.target.value)}
                placeholder="Path to scan for model folders..."
                className="flex-1 px-3 py-2 bg-input border border-border rounded text-foreground text-[13px] outline-none"
              />
              <button onClick={handleBrowseInput} className="px-4 py-2 bg-input text-foreground border border-border rounded cursor-pointer text-[13px] whitespace-nowrap hover:bg-muted">Browse...</button>
            </div>
          </div>

          {/* Output Directory */}
          <div className="mb-3.5">
            <label className="text-xs text-muted-foreground mb-1 flex items-center gap-2">
              Output Directory
              <label className="text-[13px] text-foreground cursor-pointer flex items-center gap-1.5">
                <input type="checkbox" checked={sameAsInput} onChange={e => setSameAsInput(e.target.checked)} className="accent-primary" />
                Same as input
              </label>
            </label>
            <div className="flex gap-2">
              <input
                type="text"
                value={sameAsInput ? inputDir : outputDir}
                onChange={e => setOutputDir(e.target.value)}
                disabled={sameAsInput}
                className="flex-1 px-3 py-2 bg-input border border-border rounded text-foreground text-[13px] outline-none disabled:opacity-50"
              />
              <button onClick={handleBrowseOutput} disabled={sameAsInput} className="px-4 py-2 bg-input text-foreground border border-border rounded cursor-pointer text-[13px] whitespace-nowrap hover:bg-muted disabled:opacity-50 disabled:cursor-default">Browse...</button>
            </div>
          </div>

          {/* Options Row */}
          <div className="flex gap-6 mb-4 flex-wrap">
            <div>
              <label className="text-xs text-muted-foreground mb-1 block">Model Formats</label>
              <div className="flex gap-3">
                {Object.keys(formats).map(fmt => (
                  <label key={fmt} className="text-[13px] text-foreground cursor-pointer flex items-center gap-1.5">
                    <input type="checkbox" checked={formats[fmt]} onChange={() => toggleFormat(fmt)} className="accent-primary" />
                    .{fmt}
                  </label>
                ))}
              </div>
            </div>
            <div>
              <label className="text-xs text-muted-foreground mb-1 block">Options</label>
              <label className="text-[13px] text-foreground cursor-pointer flex items-center gap-1.5">
                <input type="checkbox" checked={overwrite} onChange={e => setOverwrite(e.target.checked)} className="accent-primary" />
                Overwrite existing manifests
              </label>
            </div>
          </div>

          {/* Generate Button + Status */}
          <div className="flex items-center gap-4">
            <button
              onClick={handleGenerate}
              disabled={!canGenerate}
              className={`px-5 py-2 rounded text-[13px] font-semibold border-none cursor-pointer disabled:cursor-default ${canGenerate
                  ? 'bg-primary text-primary-foreground'
                  : 'bg-input text-muted-foreground/50'
                }`}
            >
              {generating ? 'Generating...' : 'Generate Manifests'}
            </button>
            {lastResult && !error && (
              <span className="text-[13px] text-primary">
                Generated {lastResult.generated} manifests at {lastResult.timestamp}
              </span>
            )}
            {error && (
              <span className="text-[13px] text-destructive">{error}</span>
            )}
          </div>
        </div>

        {/* Manifest List Card */}
        <div className="bg-card border border-border rounded-lg p-5 flex-1 flex flex-col overflow-hidden">
          <div className="flex justify-between items-center mb-3">
            <h2 className="m-0 text-[15px] text-foreground">
              Manifests {!loading && <span className="text-muted-foreground/50 font-normal">({manifests.length})</span>}
            </h2>
            <div className="flex gap-2 items-center">
              <input
                type="text"
                placeholder="Filter..."
                value={filter}
                onChange={e => setFilter(e.target.value)}
                className="w-[200px] px-3 py-2 bg-input border border-border rounded text-foreground text-[13px] outline-none"
              />
              <button
                onClick={fetchManifests}
                className="px-3.5 py-1.5 bg-input text-foreground border border-border rounded cursor-pointer text-[13px] whitespace-nowrap hover:bg-muted"
              >
                Refresh
              </button>
            </div>
          </div>

          {loading ? (
            <div className="text-muted-foreground/50 text-[13px] p-5 text-center">Loading...</div>
          ) : filtered.length === 0 ? (
            <div className="text-muted-foreground/50 text-[13px] p-5 text-center">
              {manifests.length === 0 ? 'No manifests found. Configure the settings above and click "Generate Manifests".' : 'No matches.'}
            </div>
          ) : (
            <div className="flex-1 overflow-auto">
              <table className="w-full border-collapse text-[13px]">
                <thead>
                  <tr className="text-muted-foreground text-left border-b border-border">
                    <th className="px-3 py-2 font-medium">Name</th>
                    <th className="px-3 py-2 font-medium">Format</th>
                    <th className="px-3 py-2 font-medium">Textures</th>
                    <th className="px-3 py-2 font-medium">Path</th>
                    <th className="px-3 py-2 font-medium"></th>
                  </tr>
                </thead>
                <tbody>
                  {filtered.map(m => (
                    <tr
                      key={m.assetsPath}
                      className="border-b border-border cursor-pointer hover:bg-muted"
                      onClick={() => handleLoadManifest(m)}
                    >
                      <td className="px-3 py-2 text-foreground">{m.name}</td>
                      <td className="px-3 py-2 text-muted-foreground">{m.modelFormat.toUpperCase()}</td>
                      <td className="px-3 py-2 text-muted-foreground">{m.textures.length}</td>
                      <td className="px-3 py-2 text-muted-foreground/50 max-w-[300px] overflow-hidden text-ellipsis whitespace-nowrap">{m.assetsPath}</td>
                      <td className="px-3 py-2">
                        <span className="text-primary text-xs">Open</span>
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
