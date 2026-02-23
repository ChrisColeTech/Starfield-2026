import { useState, useEffect } from 'react'
import { useAnimationEditorStore } from '../store/animationEditorStore'
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
      <span className="text-[12px] text-text">{label}</span>
      <Switch
        checked={enabled}
        onChange={onChange}
        className={`relative inline-flex h-[18px] w-[32px] shrink-0 cursor-pointer rounded-full border-2 border-transparent transition-colors duration-200 ease-in-out focus:outline-none ${enabled ? 'bg-accent' : 'bg-border'}`}
      >
        <span
          className={`pointer-events-none inline-block h-[14px] w-[14px] transform rounded-full bg-white shadow ring-0 transition duration-200 ease-in-out ${enabled ? 'translate-x-[14px]' : 'translate-x-0'}`}
        />
      </Switch>
    </div>
  )
}

export default function AnimationsPage() {
  const folderPath = useAnimationEditorStore(s => s.folderPath)
  const manifest = useAnimationEditorStore(s => s.manifest)
  const loading = useAnimationEditorStore(s => s.loading)
  const error = useAnimationEditorStore(s => s.error)
  const activeModelIndex = useAnimationEditorStore(s => s.activeModelIndex)
  const activeClipIndex = useAnimationEditorStore(s => s.activeClipIndex)
  const clipLoading = useAnimationEditorStore(s => s.clipLoading)
  const loadFolder = useAnimationEditorStore(s => s.loadFolder)
  const selectClip = useAnimationEditorStore(s => s.selectClip)
  const tagClip = useAnimationEditorStore(s => s.tagClip)

  const animationPlaying = useEditorStore(s => s.animationPlaying)
  const setAnimationPlaying = useEditorStore(s => s.setAnimationPlaying)

  // Scan browser state from store (persists across page nav)
  const scanDir = useAnimationEditorStore(s => s.scanDir)
  const manifests = useAnimationEditorStore(s => s.manifests)
  const scanning = useAnimationEditorStore(s => s.scanning)
  const selectedManifestIndex = useAnimationEditorStore(s => s.selectedManifestIndex)
  const scanFolder = useAnimationEditorStore(s => s.scanFolder)
  const selectManifest = useAnimationEditorStore(s => s.selectManifest)

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
        className="bg-surface border-r border-border flex flex-col shrink-0 overflow-hidden"
        style={{ width: leftOpen ? 220 : 28 }}
      >
        <div className="h-[28px] flex items-center justify-between px-[6px] bg-bg border-b border-border">
          {leftOpen && (
            <span className="text-[11px] font-bold uppercase tracking-[0.5px] text-text-secondary ml-[4px]">
              Models {manifests.length > 0 && `(${manifests.length})`}
            </span>
          )}
          <div className="flex items-center gap-[2px]">
            {leftOpen && scanDir && (
              <button
                onClick={() => scanFolder(scanDir)}
                className="text-text-secondary hover:text-text bg-transparent border-none cursor-pointer"
                title="Refresh"
              >
                <RefreshCw size={11} />
              </button>
            )}
            <button
              onClick={() => setLeftOpen(!leftOpen)}
              className="text-text-secondary hover:text-text bg-transparent border-none cursor-pointer"
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
                  <tr className="bg-bg text-text-secondary text-left border-b border-border">
                    <th className="px-[8px] py-[4px] font-medium">Name</th>
                    <th className="px-[4px] py-[4px] font-medium w-[36px] text-right">Clips</th>
                  </tr>
                </thead>
                <tbody>
                  {scanning && (
                    <tr><td colSpan={2} className="px-[8px] py-[12px] text-text-disabled text-center">
                      <Loader size={12} className="spin inline mr-[4px]" />Scanning...
                    </td></tr>
                  )}
                  {!scanning && manifests.length === 0 && (
                    <tr><td colSpan={2} className="px-[8px] py-[12px] text-text-disabled text-center">
                      {scanDir ? 'No manifests found' : 'No folder selected'}
                    </td></tr>
                  )}
                  {!scanning && manifests.map((m, i) => {
                    const active = i === selectedManifestIndex
                    return (
                      <tr
                        key={m.dir + m.name}
                        onClick={() => selectManifest(i)}
                        className="cursor-pointer border-b border-border"
                        style={{ background: active ? '#094771' : undefined }}
                        onMouseEnter={e => { if (!active) e.currentTarget.style.background = 'var(--color-hover)' }}
                        onMouseLeave={e => { if (!active) e.currentTarget.style.background = '' }}
                      >
                        <td className="px-[8px] py-[3px] truncate" style={{ color: active ? '#e0e0e0' : '#aaa' }}>
                          {m.name}
                        </td>
                        <td className="px-[4px] py-[3px] text-text-disabled text-right">{m.clipCount}</td>
                      </tr>
                    )
                  })}
                </tbody>
              </table>
            </div>

            {/* Footer: folder path */}
            <div className="h-[24px] flex items-center px-[10px] border-t border-border shrink-0">
              <span className="text-[10px] text-text-disabled truncate">
                {scanDir || 'No folder'}
              </span>
            </div>
          </>
        )}
      </div>

      {/* ───── Center: Viewport + TransportBar ───── */}
      <div className="flex-1 flex flex-col min-w-0">
        <div className="flex-1 bg-bg relative">
          <Viewport />
          {clipLoading && (
            <div className="absolute top-[8px] left-[8px] bg-black/70 text-text-secondary px-[10px] py-[4px] rounded text-[11px] flex items-center gap-[6px]">
              <Loader size={12} className="spin" /> Loading clip...
            </div>
          )}
          {loading && (
            <div className="absolute top-[8px] left-[8px] bg-black/70 text-text-secondary px-[10px] py-[4px] rounded text-[11px] flex items-center gap-[6px]">
              <Loader size={12} className="spin" /> Loading model...
            </div>
          )}
          {error && (
            <div className="absolute top-[8px] left-[8px] bg-black/70 text-danger px-[10px] py-[4px] rounded text-[11px]">
              {error}
            </div>
          )}
        </div>

        {/* Transport Bar (matches MiniToolbox TransportBar) */}
        <div className="h-[44px] bg-surface border-t border-border flex items-center px-[16px] gap-[8px] shrink-0">
          <button
            onClick={() => { if (activeClipIndex > 0) selectClip(0) }}
            disabled={activeClipIndex <= 0}
            className="w-[28px] h-[28px] flex items-center justify-center bg-transparent border border-border rounded-[2px] text-text-secondary hover:bg-hover hover:text-text cursor-pointer disabled:opacity-30"
          >
            <SkipBack size={14} />
          </button>
          <button
            onClick={() => { if (activeClipIndex > 0) selectClip(activeClipIndex - 1) }}
            disabled={activeClipIndex <= 0}
            className="w-[28px] h-[28px] flex items-center justify-center bg-transparent border border-border rounded-[2px] text-text-secondary hover:bg-hover hover:text-text cursor-pointer disabled:opacity-30"
          >
            <ChevronLeft size={14} />
          </button>
          <button
            onClick={() => setAnimationPlaying(!animationPlaying)}
            disabled={activeClipIndex < 0}
            className="w-[32px] h-[32px] flex items-center justify-center bg-active border-none rounded-[4px] text-text cursor-pointer hover:opacity-90 disabled:opacity-30"
          >
            {animationPlaying ? <Pause size={16} /> : <Play size={16} />}
          </button>
          <button
            onClick={() => { if (activeClipIndex < clips.length - 1) selectClip(activeClipIndex + 1) }}
            disabled={activeClipIndex >= clips.length - 1}
            className="w-[28px] h-[28px] flex items-center justify-center bg-transparent border border-border rounded-[2px] text-text-secondary hover:bg-hover hover:text-text cursor-pointer disabled:opacity-30"
          >
            <ChevronRight size={14} />
          </button>
          <button
            onClick={() => { if (clips.length > 0) selectClip(clips.length - 1) }}
            disabled={activeClipIndex >= clips.length - 1}
            className="w-[28px] h-[28px] flex items-center justify-center bg-transparent border border-border rounded-[2px] text-text-secondary hover:bg-hover hover:text-text cursor-pointer disabled:opacity-30"
          >
            <SkipForward size={14} />
          </button>

          <div className="flex-1 mx-[12px]">
            <div className="h-[4px] bg-bg rounded-[2px] overflow-hidden">
              <div
                className="h-full bg-accent rounded-[2px]"
                style={{ width: clips.length > 0 && activeClipIndex >= 0 ? `${((activeClipIndex + 1) / clips.length) * 100}%` : '0%' }}
              />
            </div>
          </div>

          <span className="text-[11px] text-text-secondary font-mono w-[80px] text-right">
            {activeClipIndex >= 0 ? `${activeClipIndex + 1} / ${clips.length}` : '— / —'}
          </span>
        </div>
      </div>

      {/* ───── Right: Clips Panel (matches PropertiesPanel) ───── */}
      <div
        className="bg-surface border-l border-border flex flex-col shrink-0 overflow-hidden"
        style={{ width: rightOpen ? 280 : 28 }}
      >
        <div className="h-[28px] flex items-center justify-between px-[6px] bg-bg border-b border-border">
          <button
            onClick={() => setRightOpen(!rightOpen)}
            className="text-text-secondary hover:text-text bg-transparent border-none cursor-pointer"
          >
            {rightOpen ? <PanelRightClose size={14} /> : <PanelRightOpen size={14} />}
          </button>
          {rightOpen && (
            <span className="text-[11px] font-bold uppercase tracking-[0.5px] text-text-secondary mr-[4px]">
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
                className="h-[24px] w-full flex items-center px-[10px] bg-bg border-b border-border shrink-0 cursor-pointer border-x-0 border-t-0"
              >
                {sectionsOpen.clips ? <ChevronDown size={10} className="text-text-secondary mr-[4px]" /> : <ChevronRight size={10} className="text-text-secondary mr-[4px]" />}
                <span className="text-[11px] font-bold uppercase tracking-[0.5px] text-text-secondary">
                  {model ? `${model.name} (${clips.length})` : 'Animation Clips'}
                </span>
              </button>
              {sectionsOpen.clips && (
                <div className="flex-1 overflow-y-auto">
                  {clips.length === 0 && (
                    <div className="p-[10px] text-[11px] text-text-disabled">
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
                        className="w-full px-[10px] py-[4px] bg-transparent border-none text-left text-[12px] cursor-pointer hover:bg-hover flex flex-col"
                        style={{
                          color: active ? '#e0e0e0' : '#808080',
                          background: active ? '#094771' : undefined,
                        }}
                      >
                        <div className="flex items-center gap-[6px]">
                          <span className="font-mono text-[11px]">{clip.id}</span>
                          {tagged && (
                            <span className="text-[9px] px-[4px] py-[0px] rounded-[3px]"
                              style={{ background: 'rgba(74,222,128,0.15)', color: 'var(--color-success)' }}
                            >
                              {clip.semanticName}
                            </span>
                          )}
                        </div>
                        <div className="text-[10px] text-text-disabled">
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
                className="h-[24px] w-full flex items-center px-[10px] bg-bg border-b border-border shrink-0 cursor-pointer border-x-0 border-t-0"
              >
                {sectionsOpen.tag ? <ChevronDown size={10} className="text-text-secondary mr-[4px]" /> : <ChevronRight size={10} className="text-text-secondary mr-[4px]" />}
                <span className="text-[11px] font-bold uppercase tracking-[0.5px] text-text-secondary">Tag</span>
              </button>
              {sectionsOpen.tag && (
                <div className="p-[10px] text-[12px]">
                  <div className="text-[10px] text-text-secondary mb-[4px] flex items-center gap-[4px]">
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
                    className="w-full px-[6px] py-[4px] text-[11px] bg-input border border-border rounded-[2px] text-text outline-none disabled:opacity-40"
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
                className="h-[24px] w-full flex items-center px-[10px] bg-bg border-b border-border shrink-0 cursor-pointer border-x-0 border-t-0"
              >
                {sectionsOpen.playback ? <ChevronDown size={10} className="text-text-secondary mr-[4px]" /> : <ChevronRight size={10} className="text-text-secondary mr-[4px]" />}
                <span className="text-[11px] font-bold uppercase tracking-[0.5px] text-text-secondary">Playback</span>
              </button>
              {sectionsOpen.playback && (
                <div className="p-[10px] text-[12px]">
                  <div className="mb-[8px]">
                    <div className="flex items-center justify-between mb-[4px]">
                      <label className="text-[11px] text-text-secondary">Speed</label>
                      <span className="text-[11px] text-text">{playbackSpeed.toFixed(1)}x</span>
                    </div>
                    <input type="range" min={0.1} max={3.0} step={0.1} value={playbackSpeed} onChange={e => setPlaybackSpeed(parseFloat(e.target.value))} className="w-full" />
                  </div>
                  <div>
                    <label className="text-[11px] text-text-secondary block mb-[4px]">Loop</label>
                    <select
                      value={loopMode}
                      onChange={e => setLoopMode(e.target.value as 'loop' | 'once' | 'pingpong')}
                      className="w-full px-[6px] py-[4px] text-[11px] bg-input border border-border rounded-[2px] text-text outline-none"
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
                className="h-[24px] w-full flex items-center px-[10px] bg-bg border-b border-border shrink-0 cursor-pointer border-x-0 border-t-0"
              >
                {sectionsOpen.render ? <ChevronDown size={10} className="text-text-secondary mr-[4px]" /> : <ChevronRight size={10} className="text-text-secondary mr-[4px]" />}
                <span className="text-[11px] font-bold uppercase tracking-[0.5px] text-text-secondary">Render</span>
              </button>
              {sectionsOpen.render && (
                <div className="p-[10px] text-[12px] flex flex-col gap-[6px]">
                  <RenderToggle label="Wireframe" enabled={renderSettings.showWireframe} onChange={v => setRenderSetting('showWireframe', v)} />
                  <RenderToggle label="Skeleton" enabled={renderSettings.showSkeleton} onChange={v => setRenderSetting('showSkeleton', v)} />
                  <RenderToggle label="Grid" enabled={renderSettings.showGrid} onChange={v => setRenderSetting('showGrid', v)} />
                  <RenderToggle label="Textures" enabled={renderSettings.showTextures} onChange={v => setRenderSetting('showTextures', v)} />
                  <div className="mt-[4px] pt-[6px] border-t border-border">
                    <div className="flex items-center justify-between mb-[4px]">
                      <label className="text-[11px] text-text-secondary">Lighting</label>
                      <span className="text-[11px] text-text">{renderSettings.lightIntensity.toFixed(1)}</span>
                    </div>
                    <input type="range" min={0.2} max={3.0} step={0.1} value={renderSettings.lightIntensity} onChange={e => setRenderSetting('lightIntensity', parseFloat(e.target.value))} className="w-full" />
                  </div>
                </div>
              )}
            </div>

            {/* Controls help */}
            <div>
              <button
                onClick={() => toggleSection('controls')}
                className="h-[24px] w-full flex items-center px-[10px] bg-bg border-b border-border shrink-0 cursor-pointer border-x-0 border-t-0"
              >
                {sectionsOpen.controls ? <ChevronDown size={10} className="text-text-secondary mr-[4px]" /> : <ChevronRight size={10} className="text-text-secondary mr-[4px]" />}
                <span className="text-[11px] font-bold uppercase tracking-[0.5px] text-text-secondary">Controls</span>
              </button>
              {sectionsOpen.controls && (
                <div className="p-[10px] text-[12px] flex flex-col gap-[5px]">
                  <div className="flex items-center gap-[6px]">
                    <RefreshCw size={12} className="text-text-secondary shrink-0" />
                    <span className="text-[11px] text-text-secondary">Left Click + Drag</span>
                    <span className="text-[11px] text-text ml-auto">Orbit</span>
                  </div>
                  <div className="flex items-center gap-[6px]">
                    <Move size={12} className="text-text-secondary shrink-0" />
                    <span className="text-[11px] text-text-secondary">Right Click + Drag</span>
                    <span className="text-[11px] text-text ml-auto">Pan</span>
                  </div>
                  <div className="flex items-center gap-[6px]">
                    <ZoomIn size={12} className="text-text-secondary shrink-0" />
                    <span className="text-[11px] text-text-secondary">Scroll Wheel</span>
                    <span className="text-[11px] text-text ml-auto">Zoom</span>
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

