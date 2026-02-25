import { useState, useEffect } from 'react'
import { useEditorStore } from '../store/editorStore'
import Viewport from '../components/Viewport'
import { SEMANTIC_TAGS } from '../types/animation'
import type { SplitManifestClip } from '../types/animation'
import {
  PanelLeftClose, PanelLeftOpen, PanelRightClose, PanelRightOpen,
  RefreshCw, Loader, Tag,
  Play, Pause, SkipBack, SkipForward, ChevronLeft, ChevronRight, ChevronDown,
  Move, ZoomIn,
} from 'lucide-react'
import { Switch } from '@headlessui/react'


interface RenderSettings {
  showWireframe: boolean
  showSkeleton: boolean
  showGrid: boolean
  showTextures: boolean
  lightIntensity: number
}

function RenderToggle({ label, enabled, onChange }: { label: string; enabled: boolean; onChange: (v: boolean) => void }) {
  return (
    <div className="flex items-center justify-between">
      <span className="text-xs text-foreground">{label}</span>
      <Switch
        checked={enabled}
        onChange={onChange}
        className={`relative inline-flex h-[18px] w-8 shrink-0 cursor-pointer rounded-full border-2 border-transparent transition-colors duration-200 ease-in-out focus:outline-none ${enabled ? 'bg-primary' : 'bg-border'}`}
      >
        <span
          className={`pointer-events-none inline-block h-[14px] w-[14px] transform rounded-full bg-white shadow ring-0 transition duration-200 ease-in-out ${enabled ? 'translate-x-[14px]' : 'translate-x-0'}`}
        />
      </Switch>
    </div>
  )
}

export default function AnimationsPage() {
  const folderPath = useEditorStore(s => s.folderPath)
  const manifest = useEditorStore(s => s.animManifest)
  const loading = useEditorStore(s => s.loading)
  const error = useEditorStore(s => s.error)
  const activeModelIndex = useEditorStore(s => s.activeModelIndex)
  const activeClipIndex = useEditorStore(s => s.activeClipIndex)
  const clipLoading = useEditorStore(s => s.clipLoading)
  const loadFolder = useEditorStore(s => s.loadFolder)
  const selectClip = useEditorStore(s => s.selectClip)
  const tagClip = useEditorStore(s => s.tagClip)

  const animationPlaying = useEditorStore(s => s.animationPlaying)
  const setAnimationPlaying = useEditorStore(s => s.setAnimationPlaying)

  // Scan browser state from store (persists across page nav)
  const scanDir = useEditorStore(s => s.scanDir)
  const manifests = useEditorStore(s => s.manifests)
  const scanning = useEditorStore(s => s.scanning)
  const selectedManifestIndex = useEditorStore(s => s.selectedManifestIndex)
  const scanFolder = useEditorStore(s => s.scanFolder)
  const selectManifest = useEditorStore(s => s.selectManifest)

  // Panel state
  const [leftOpen, setLeftOpen] = useState(true)
  const [rightOpen, setRightOpen] = useState(true)

  // Collapsible sections
  const [sectionsOpen, setSectionsOpen] = useState({
    clips: true,
    tag: true,
    playback: true,
    render: false,
    controls: false,
  })
  const toggleSection = (key: keyof typeof sectionsOpen) =>
    setSectionsOpen(prev => ({ ...prev, [key]: !prev[key] }))

  // Render settings
  const [renderSettings, setRenderSettings] = useState<RenderSettings>({
    showWireframe: false,
    showSkeleton: false,
    showGrid: true,
    showTextures: true,
    lightIntensity: 1.0,
  })
  const setRenderSetting = <K extends keyof RenderSettings>(key: K, value: RenderSettings[K]) => {
    setRenderSettings(prev => ({ ...prev, [key]: value }))
  }

  // Playback controls
  const [playbackSpeed, setPlaybackSpeed] = useState(1.0)
  const [loopMode, setLoopMode] = useState<'loop' | 'once' | 'pingpong'>('loop')

  const model = manifest?.models[activeModelIndex]
  const clips = model?.clips ?? []
  const selectedClip = activeClipIndex >= 0 ? clips[activeClipIndex] : null

  // Listen for menu bar browse event
  useEffect(() => {
    const handler = (e: Event) => {
      const dir = (e as CustomEvent).detail as string
      if (dir) scanFolder(dir)
    }
    window.addEventListener('animations:browse', handler)
    return () => window.removeEventListener('animations:browse', handler)
  }, [scanFolder])

  return (
    <div className="flex h-full">
      {/* ───── Left: Model Browser ───── */}
      <div
        className="bg-card border-r border-border flex flex-col shrink-0 overflow-hidden"
        style={{ width: leftOpen ? 220 : 28 }}
      >
        <div className="h-7 flex items-center justify-between px-1.5 bg-background border-b border-border">
          {leftOpen && (
            <span className="text-[11px] font-bold uppercase tracking-wider text-muted-foreground ml-1">
              Models {manifests.length > 0 && `(${manifests.length})`}
            </span>
          )}
          <div className="flex items-center gap-0.5">
            {leftOpen && scanDir && (
              <button
                onClick={() => scanFolder(scanDir)}
                className="text-muted-foreground hover:text-foreground bg-transparent border-none cursor-pointer"
                title="Refresh"
              >
                <RefreshCw size={11} />
              </button>
            )}
            <button
              onClick={() => setLeftOpen(!leftOpen)}
              className="text-muted-foreground hover:text-foreground bg-transparent border-none cursor-pointer"
            >
              {leftOpen ? <PanelLeftClose size={14} /> : <PanelLeftOpen size={14} />}
            </button>
          </div>
        </div>

        {leftOpen && (
          <>
            {/* List view */}
            <div className="flex-1 overflow-y-auto border-t border-border">
              <table className="w-full border-collapse text-[11px]">
                <thead className="sticky top-0 z-10">
                  <tr className="bg-background text-muted-foreground text-left border-b border-border">
                    <th className="px-2 py-1 font-medium">Name</th>
                    <th className="px-1 py-1 font-medium w-9 text-right">Clips</th>
                  </tr>
                </thead>
                <tbody>
                  {scanning && (
                    <tr><td colSpan={2} className="px-2 py-3 text-muted-foreground/50 text-center">
                      <Loader size={12} className="spin inline mr-1" />Scanning...
                    </td></tr>
                  )}
                  {!scanning && manifests.length === 0 && (
                    <tr><td colSpan={2} className="px-2 py-3 text-muted-foreground/50 text-center">
                      {scanDir ? 'No manifests found' : 'No folder selected'}
                    </td></tr>
                  )}
                  {!scanning && manifests.map((m, i) => {
                    const active = i === selectedManifestIndex
                    return (
                      <tr
                        key={m.dir + m.name}
                        onClick={() => selectManifest(i)}
                        className={`cursor-pointer border-b border-border transition-colors ${active ? 'bg-primary/15' : 'hover:bg-muted'
                          }`}
                      >
                        <td className={`px-2 py-1 truncate ${active ? 'text-foreground' : 'text-muted-foreground'}`}>
                          {m.name}
                        </td>
                        <td className="px-1 py-1 text-muted-foreground/50 text-right">{m.clipCount}</td>
                      </tr>
                    )
                  })}
                </tbody>
              </table>
            </div>

            {/* Footer: folder path */}
            <div className="h-6 flex items-center px-2.5 border-t border-border shrink-0">
              <span className="text-[10px] text-muted-foreground/50 truncate">
                {scanDir || 'No folder'}
              </span>
            </div>
          </>
        )}
      </div>

      {/* ───── Center: Viewport + TransportBar ───── */}
      <div className="flex-1 flex flex-col min-w-0">
        <div className="flex-1 bg-background relative">
          <Viewport />
          {clipLoading && (
            <div className="absolute top-2 left-2 bg-black/70 text-muted-foreground px-2.5 py-1 rounded text-[11px] flex items-center gap-1.5">
              <Loader size={12} className="spin" /> Loading clip...
            </div>
          )}
          {loading && (
            <div className="absolute top-2 left-2 bg-black/70 text-muted-foreground px-2.5 py-1 rounded text-[11px] flex items-center gap-1.5">
              <Loader size={12} className="spin" /> Loading model...
            </div>
          )}
          {error && (
            <div className="absolute top-2 left-2 bg-black/70 text-destructive px-2.5 py-1 rounded text-[11px]">
              {error}
            </div>
          )}
        </div>

        {/* Transport Bar */}
        <div className="h-11 bg-card border-t border-border flex items-center px-4 gap-2 shrink-0">
          <button
            onClick={() => { if (activeClipIndex > 0) selectClip(0) }}
            disabled={activeClipIndex <= 0}
            className="w-7 h-7 flex items-center justify-center bg-transparent border border-border rounded text-muted-foreground hover:bg-muted hover:text-foreground cursor-pointer disabled:opacity-30"
          >
            <SkipBack size={14} />
          </button>
          <button
            onClick={() => { if (activeClipIndex > 0) selectClip(activeClipIndex - 1) }}
            disabled={activeClipIndex <= 0}
            className="w-7 h-7 flex items-center justify-center bg-transparent border border-border rounded text-muted-foreground hover:bg-muted hover:text-foreground cursor-pointer disabled:opacity-30"
          >
            <ChevronLeft size={14} />
          </button>
          <button
            onClick={() => setAnimationPlaying(!animationPlaying)}
            disabled={activeClipIndex < 0}
            className="w-8 h-8 flex items-center justify-center bg-primary/10 border-none rounded text-foreground cursor-pointer hover:opacity-90 disabled:opacity-30"
          >
            {animationPlaying ? <Pause size={16} /> : <Play size={16} />}
          </button>
          <button
            onClick={() => { if (activeClipIndex < clips.length - 1) selectClip(activeClipIndex + 1) }}
            disabled={activeClipIndex >= clips.length - 1}
            className="w-7 h-7 flex items-center justify-center bg-transparent border border-border rounded text-muted-foreground hover:bg-muted hover:text-foreground cursor-pointer disabled:opacity-30"
          >
            <ChevronRight size={14} />
          </button>
          <button
            onClick={() => { if (clips.length > 0) selectClip(clips.length - 1) }}
            disabled={activeClipIndex >= clips.length - 1}
            className="w-7 h-7 flex items-center justify-center bg-transparent border border-border rounded text-muted-foreground hover:bg-muted hover:text-foreground cursor-pointer disabled:opacity-30"
          >
            <SkipForward size={14} />
          </button>

          <div className="flex-1 mx-3">
            <div className="h-1 bg-background rounded overflow-hidden">
              <div
                className="h-full bg-primary rounded"
                style={{ width: clips.length > 0 && activeClipIndex >= 0 ? `${((activeClipIndex + 1) / clips.length) * 100}%` : '0%' }}
              />
            </div>
          </div>

          <span className="text-[11px] text-muted-foreground font-mono w-20 text-right">
            {activeClipIndex >= 0 ? `${activeClipIndex + 1} / ${clips.length}` : '— / —'}
          </span>
        </div>
      </div>

      {/* ───── Right: Clips Panel ───── */}
      <div
        className="bg-card border-l border-border flex flex-col shrink-0 overflow-hidden"
        style={{ width: rightOpen ? 280 : 28 }}
      >
        <div className="h-7 flex items-center justify-between px-1.5 bg-background border-b border-border">
          <button
            onClick={() => setRightOpen(!rightOpen)}
            className="text-muted-foreground hover:text-foreground bg-transparent border-none cursor-pointer"
          >
            {rightOpen ? <PanelRightClose size={14} /> : <PanelRightOpen size={14} />}
          </button>
          {rightOpen && (
            <span className="text-[11px] font-bold uppercase tracking-wider text-muted-foreground mr-1">
              Clips
            </span>
          )}
        </div>

        {rightOpen && (
          <>
            {/* Clip list section */}
            <div className={`border-b border-border ${sectionsOpen.clips ? 'flex-1 flex flex-col overflow-hidden' : ''}`}>
              <button
                onClick={() => toggleSection('clips')}
                className="h-6 w-full flex items-center px-2.5 bg-background border-b border-border shrink-0 cursor-pointer border-x-0 border-t-0"
              >
                {sectionsOpen.clips ? <ChevronDown size={10} className="text-muted-foreground mr-1" /> : <ChevronRight size={10} className="text-muted-foreground mr-1" />}
                <span className="text-[11px] font-bold uppercase tracking-wider text-muted-foreground">
                  {model ? `${model.name} (${clips.length})` : 'Animation Clips'}
                </span>
              </button>
              {sectionsOpen.clips && (
                <div className="flex-1 overflow-y-auto">
                  {clips.length === 0 && (
                    <div className="p-2.5 text-[11px] text-muted-foreground/50">
                      {model ? 'No clips in manifest' : 'No model loaded'}
                    </div>
                  )}
                  {clips.map((clip, i) => {
                    const active = i === activeClipIndex
                    const tagged = !!clip.semanticName
                    return (
                      <button
                        key={clip.id}
                        onClick={() => selectClip(i)}
                        className={`w-full px-2.5 py-1 bg-transparent border-none text-left text-xs cursor-pointer flex flex-col transition-colors ${active ? 'bg-primary/15 text-foreground' : 'text-muted-foreground hover:bg-muted'
                          }`}
                      >
                        <div className="flex items-center gap-1.5">
                          <span className="font-mono text-[11px]">{clip.id}</span>
                          {tagged && (
                            <span className="text-[9px] px-1 rounded bg-success/15 text-success">
                              {clip.semanticName}
                            </span>
                          )}
                        </div>
                        <div className="text-[10px] text-muted-foreground/50">
                          {clip.frameCount} frames · {clip.sourceName}
                        </div>
                      </button>
                    )
                  })}
                </div>
              )}
            </div>

            {/* Tag section */}
            <div className="border-b border-border">
              <button
                onClick={() => toggleSection('tag')}
                className="h-6 w-full flex items-center px-2.5 bg-background border-b border-border shrink-0 cursor-pointer border-x-0 border-t-0"
              >
                {sectionsOpen.tag ? <ChevronDown size={10} className="text-muted-foreground mr-1" /> : <ChevronRight size={10} className="text-muted-foreground mr-1" />}
                <span className="text-[11px] font-bold uppercase tracking-wider text-muted-foreground">Tag</span>
              </button>
              {sectionsOpen.tag && (
                <div className="p-2.5 text-xs">
                  <div className="text-[10px] text-muted-foreground mb-1 flex items-center gap-1">
                    <Tag size={10} />{selectedClip ? selectedClip.id : '—'}
                  </div>
                  <select
                    value={selectedClip?.semanticName ?? ''}
                    onChange={e => {
                      if (activeClipIndex >= 0) {
                        const v = e.target.value
                        tagClip(activeClipIndex, v === '__clear__' ? null : v || null)
                      }
                    }}
                    disabled={!selectedClip}
                    className="w-full px-1.5 py-1 text-[11px] bg-input border border-border rounded text-foreground outline-none disabled:opacity-40 text-center"
                  >
                    <option value="">— No tag —</option>
                    {SEMANTIC_TAGS.map(tag => (
                      <option key={tag} value={tag}>{tag}</option>
                    ))}
                    {selectedClip?.semanticName && <option value="__clear__">Clear tag</option>}
                  </select>
                </div>
              )}
            </div>

            {/* Playback section */}
            <div className="border-b border-border">
              <button
                onClick={() => toggleSection('playback')}
                className="h-6 w-full flex items-center px-2.5 bg-background border-b border-border shrink-0 cursor-pointer border-x-0 border-t-0"
              >
                {sectionsOpen.playback ? <ChevronDown size={10} className="text-muted-foreground mr-1" /> : <ChevronRight size={10} className="text-muted-foreground mr-1" />}
                <span className="text-[11px] font-bold uppercase tracking-wider text-muted-foreground">Playback</span>
              </button>
              {sectionsOpen.playback && (
                <div className="p-2.5 text-xs">
                  <div className="mb-2">
                    <div className="flex items-center justify-between mb-1">
                      <label className="text-[11px] text-muted-foreground">Speed</label>
                      <span className="text-[11px] text-foreground">{playbackSpeed.toFixed(1)}x</span>
                    </div>
                    <input type="range" min={0.1} max={3.0} step={0.1} value={playbackSpeed} onChange={e => setPlaybackSpeed(parseFloat(e.target.value))} className="w-full accent-primary" />
                  </div>
                  <div>
                    <label className="text-[11px] text-muted-foreground block mb-1">Loop</label>
                    <select
                      value={loopMode}
                      onChange={e => setLoopMode(e.target.value as 'loop' | 'once' | 'pingpong')}
                      className="w-full px-1.5 py-1 text-[11px] bg-input border border-border rounded text-foreground outline-none text-center"
                    >
                      <option value="loop">Loop</option>
                      <option value="once">Play Once</option>
                      <option value="pingpong">Ping Pong</option>
                    </select>
                  </div>
                </div>
              )}
            </div>

            {/* Render section */}
            <div className="border-b border-border">
              <button
                onClick={() => toggleSection('render')}
                className="h-6 w-full flex items-center px-2.5 bg-background border-b border-border shrink-0 cursor-pointer border-x-0 border-t-0"
              >
                {sectionsOpen.render ? <ChevronDown size={10} className="text-muted-foreground mr-1" /> : <ChevronRight size={10} className="text-muted-foreground mr-1" />}
                <span className="text-[11px] font-bold uppercase tracking-wider text-muted-foreground">Render</span>
              </button>
              {sectionsOpen.render && (
                <div className="p-2.5 text-xs flex flex-col gap-1.5">
                  <RenderToggle label="Wireframe" enabled={renderSettings.showWireframe} onChange={v => setRenderSetting('showWireframe', v)} />
                  <RenderToggle label="Skeleton" enabled={renderSettings.showSkeleton} onChange={v => setRenderSetting('showSkeleton', v)} />
                  <RenderToggle label="Grid" enabled={renderSettings.showGrid} onChange={v => setRenderSetting('showGrid', v)} />
                  <RenderToggle label="Textures" enabled={renderSettings.showTextures} onChange={v => setRenderSetting('showTextures', v)} />
                  <div className="mt-1 pt-1.5 border-t border-border">
                    <div className="flex items-center justify-between mb-1">
                      <label className="text-[11px] text-muted-foreground">Lighting</label>
                      <span className="text-[11px] text-foreground">{renderSettings.lightIntensity.toFixed(1)}</span>
                    </div>
                    <input type="range" min={0.2} max={3.0} step={0.1} value={renderSettings.lightIntensity} onChange={e => setRenderSetting('lightIntensity', parseFloat(e.target.value))} className="w-full accent-primary" />
                  </div>
                </div>
              )}
            </div>

            {/* Controls help */}
            <div>
              <button
                onClick={() => toggleSection('controls')}
                className="h-6 w-full flex items-center px-2.5 bg-background border-b border-border shrink-0 cursor-pointer border-x-0 border-t-0"
              >
                {sectionsOpen.controls ? <ChevronDown size={10} className="text-muted-foreground mr-1" /> : <ChevronRight size={10} className="text-muted-foreground mr-1" />}
                <span className="text-[11px] font-bold uppercase tracking-wider text-muted-foreground">Controls</span>
              </button>
              {sectionsOpen.controls && (
                <div className="p-2.5 text-xs flex flex-col gap-1.5">
                  <div className="flex items-center gap-1.5">
                    <RefreshCw size={12} className="text-muted-foreground shrink-0" />
                    <span className="text-[11px] text-muted-foreground">Left Click + Drag</span>
                    <span className="text-[11px] text-foreground ml-auto">Orbit</span>
                  </div>
                  <div className="flex items-center gap-1.5">
                    <Move size={12} className="text-muted-foreground shrink-0" />
                    <span className="text-[11px] text-muted-foreground">Right Click + Drag</span>
                    <span className="text-[11px] text-foreground ml-auto">Pan</span>
                  </div>
                  <div className="flex items-center gap-1.5">
                    <ZoomIn size={12} className="text-muted-foreground shrink-0" />
                    <span className="text-[11px] text-muted-foreground">Scroll Wheel</span>
                    <span className="text-[11px] text-foreground ml-auto">Zoom</span>
                  </div>
                </div>
              )}
            </div>
          </>
        )}
      </div>
    </div>
  )
}
