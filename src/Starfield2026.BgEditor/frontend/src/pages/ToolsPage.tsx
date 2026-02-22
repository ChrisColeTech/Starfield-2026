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
  useEffect(() => { if (!settingsLoaded) return; window.electronAPI.storeSet('manifestInputDir', inputDir) }, [inputDir, settingsLoaded])
  useEffect(() => { if (!settingsLoaded) return; window.electronAPI.storeSet('manifestOutputDir', outputDir) }, [outputDir, settingsLoaded])
  useEffect(() => { if (!settingsLoaded) return; window.electronAPI.storeSet('manifestSameAsInput', sameAsInput) }, [sameAsInput, settingsLoaded])
  useEffect(() => { if (!settingsLoaded) return; window.electronAPI.storeSet('manifestOverwrite', overwrite) }, [overwrite, settingsLoaded])
  useEffect(() => { if (!settingsLoaded) return; window.electronAPI.storeSet('manifestFormats', formats) }, [formats, settingsLoaded])

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
    <div className="flex flex-col h-full overflow-hidden">
      {/* Header */}
      <div className="px-[24px] py-[16px] bg-surface border-b border-border">
        <h1 className="m-0 text-[18px] text-text">Tools</h1>
      </div>

      <div className="flex-1 overflow-auto p-[24px] flex flex-col gap-[24px]">
        {/* Manifest Generator Card */}
        <div className="bg-surface border border-border rounded-[8px] p-[20px]">
          <h2 className="m-0 mb-[16px] text-[15px] text-text">Manifest Generator</h2>

          {/* Input Directory */}
          <div className="mb-[14px]">
            <label className="text-[12px] text-text-secondary mb-[4px] block">Input Directory</label>
            <div className="flex gap-[8px]">
              <input
                type="text"
                value={inputDir}
                onChange={e => setInputDir(e.target.value)}
                placeholder="Path to scan for model folders..."
                className="flex-1 px-[12px] py-[8px] bg-input border border-border rounded text-text text-[13px] outline-none"
              />
              <button onClick={handleBrowseInput} className="px-[16px] py-[8px] bg-input text-text border border-border rounded cursor-pointer text-[13px] whitespace-nowrap hover:bg-hover">Browse...</button>
            </div>
          </div>

          {/* Output Directory */}
          <div className="mb-[14px]">
            <label className="text-[12px] text-text-secondary mb-[4px] flex items-center gap-[8px]">
              Output Directory
              <label className="text-[13px] text-text cursor-pointer flex items-center gap-[6px]">
                <input type="checkbox" checked={sameAsInput} onChange={e => setSameAsInput(e.target.checked)} />
                Same as input
              </label>
            </label>
            <div className="flex gap-[8px]">
              <input
                type="text"
                value={sameAsInput ? inputDir : outputDir}
                onChange={e => setOutputDir(e.target.value)}
                disabled={sameAsInput}
                className="flex-1 px-[12px] py-[8px] bg-input border border-border rounded text-text text-[13px] outline-none disabled:opacity-50"
              />
              <button onClick={handleBrowseOutput} disabled={sameAsInput} className="px-[16px] py-[8px] bg-input text-text border border-border rounded cursor-pointer text-[13px] whitespace-nowrap hover:bg-hover disabled:opacity-50 disabled:cursor-default">Browse...</button>
            </div>
          </div>

          {/* Options Row */}
          <div className="flex gap-[24px] mb-[18px] flex-wrap">
            <div>
              <label className="text-[12px] text-text-secondary mb-[4px] block">Model Formats</label>
              <div className="flex gap-[12px]">
                {Object.keys(formats).map(fmt => (
                  <label key={fmt} className="text-[13px] text-text cursor-pointer flex items-center gap-[6px]">
                    <input type="checkbox" checked={formats[fmt]} onChange={() => toggleFormat(fmt)} />
                    .{fmt}
                  </label>
                ))}
              </div>
            </div>
            <div>
              <label className="text-[12px] text-text-secondary mb-[4px] block">Options</label>
              <label className="text-[13px] text-text cursor-pointer flex items-center gap-[6px]">
                <input type="checkbox" checked={overwrite} onChange={e => setOverwrite(e.target.checked)} />
                Overwrite existing manifests
              </label>
            </div>
          </div>

          {/* Generate Button + Status */}
          <div className="flex items-center gap-[16px]">
            <button
              onClick={handleGenerate}
              disabled={generating || !inputDir}
              className="px-[20px] py-[8px] rounded text-[13px] font-semibold border-none cursor-pointer disabled:cursor-default"
              style={{
                background: (generating || !inputDir) ? 'var(--color-input)' : 'var(--color-accent)',
                color: (generating || !inputDir) ? 'var(--color-text-disabled)' : '#fff',
              }}
            >
              {generating ? 'Generating...' : 'Generate Manifests'}
            </button>
            {lastResult && !error && (
              <span className="text-[13px] text-accent">
                Generated {lastResult.generated} manifests at {lastResult.timestamp}
              </span>
            )}
            {error && (
              <span className="text-[13px] text-danger">{error}</span>
            )}
          </div>
        </div>

        {/* Manifest List Card */}
        <div className="bg-surface border border-border rounded-[8px] p-[20px] flex-1 flex flex-col overflow-hidden">
          <div className="flex justify-between items-center mb-[12px]">
            <h2 className="m-0 text-[15px] text-text">
              Manifests {!loading && <span className="text-text-disabled font-normal">({manifests.length})</span>}
            </h2>
            <div className="flex gap-[8px] items-center">
              <input
                type="text"
                placeholder="Filter..."
                value={filter}
                onChange={e => setFilter(e.target.value)}
                className="w-[200px] px-[12px] py-[8px] bg-input border border-border rounded text-text text-[13px] outline-none"
              />
              <button
                onClick={fetchManifests}
                className="px-[14px] py-[6px] bg-input text-text border border-border rounded cursor-pointer text-[13px] whitespace-nowrap hover:bg-hover"
              >
                Refresh
              </button>
            </div>
          </div>

          {loading ? (
            <div className="text-text-disabled text-[13px] p-[20px] text-center">Loading...</div>
          ) : filtered.length === 0 ? (
            <div className="text-text-disabled text-[13px] p-[20px] text-center">
              {manifests.length === 0 ? 'No manifests found. Configure the settings above and click "Generate Manifests".' : 'No matches.'}
            </div>
          ) : (
            <div className="flex-1 overflow-auto">
              <table className="w-full border-collapse text-[13px]">
                <thead>
                  <tr className="text-text-secondary text-left border-b border-border">
                    <th className="p-[8px_12px] font-medium">Name</th>
                    <th className="p-[8px_12px] font-medium">Format</th>
                    <th className="p-[8px_12px] font-medium">Textures</th>
                    <th className="p-[8px_12px] font-medium">Path</th>
                    <th className="p-[8px_12px] font-medium"></th>
                  </tr>
                </thead>
                <tbody>
                  {filtered.map(m => (
                    <tr
                      key={m.assetsPath}
                      className="border-b border-border cursor-pointer hover:bg-hover"
                      onClick={() => handleLoadManifest(m)}
                    >
                      <td className="p-[8px_12px] text-text">{m.name}</td>
                      <td className="p-[8px_12px] text-text-secondary">{m.modelFormat.toUpperCase()}</td>
                      <td className="p-[8px_12px] text-text-secondary">{m.textures.length}</td>
                      <td className="p-[8px_12px] text-text-disabled max-w-[300px] overflow-hidden text-ellipsis whitespace-nowrap">{m.assetsPath}</td>
                      <td className="p-[8px_12px]">
                        <span className="text-accent text-[12px]">Open</span>
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
