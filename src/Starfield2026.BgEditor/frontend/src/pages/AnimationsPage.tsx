import { useState, useCallback } from 'react'
import { useAnimationEditorStore } from '../store/animationEditorStore'
import { useEditorStore } from '../store/editorStore'
import Viewport from '../components/Viewport'
import { SEMANTIC_TAGS } from '../types/animation'
import type { SplitManifestClip } from '../types/animation'
import { Film, Save, Tag, Loader, FolderOpen, Play, Pause, Wand2 } from 'lucide-react'

export default function AnimationsPage() {
  const folderPath = useAnimationEditorStore(s => s.folderPath)
  const manifest = useAnimationEditorStore(s => s.manifest)
  const dirty = useAnimationEditorStore(s => s.dirty)
  const saving = useAnimationEditorStore(s => s.saving)
  const loading = useAnimationEditorStore(s => s.loading)
  const error = useAnimationEditorStore(s => s.error)
  const activeModelIndex = useAnimationEditorStore(s => s.activeModelIndex)
  const activeClipIndex = useAnimationEditorStore(s => s.activeClipIndex)
  const clipLoading = useAnimationEditorStore(s => s.clipLoading)
  const loadFolder = useAnimationEditorStore(s => s.loadFolder)
  const selectClip = useAnimationEditorStore(s => s.selectClip)
  const tagClip = useAnimationEditorStore(s => s.tagClip)
  const autoTag = useAnimationEditorStore(s => s.autoTag)
  const save = useAnimationEditorStore(s => s.save)

  const animationPlaying = useEditorStore(s => s.animationPlaying)
  const setAnimationPlaying = useEditorStore(s => s.setAnimationPlaying)
  const scene = useEditorStore(s => s.scene)

  const [pathInput, setPathInput] = useState('')

  const handleLoad = useCallback(() => {
    const p = pathInput.trim()
    if (p) loadFolder(p)
  }, [pathInput, loadFolder])

  const model = manifest?.models[activeModelIndex]
  const clips = model?.clips ?? []
  const selectedClip = activeClipIndex >= 0 ? clips[activeClipIndex] : null

  return (
    <div className="flex flex-col h-full overflow-hidden">
      {/* Header */}
      <div className="px-[20px] py-[12px] bg-surface border-b border-border flex justify-between items-center shrink-0">
        <h1 className="m-0 text-[16px] text-text">Animation Editor</h1>
        <div className="flex items-center gap-[8px]">
          {dirty && (
            <span className="w-[8px] h-[8px] rounded-full bg-warning inline-block" title="Unsaved changes" />
          )}
          {model && (
            <span className="text-[12px] text-text-secondary">
              {model.name} — {clips.length} clips
            </span>
          )}
        </div>
      </div>

      {/* Main area */}
      <div className="flex-1 flex overflow-hidden">
        {/* Viewport */}
        <div className="flex-1 relative">
          {scene ? (
            <Viewport />
          ) : (
            <div className="flex flex-col h-full items-center justify-center text-text-disabled text-[14px] gap-[8px]">
              <Film size={32} strokeWidth={1.5} />
              <span>Load a model folder to begin</span>
            </div>
          )}
          {clipLoading && (
            <div className="absolute top-[8px] left-[8px] bg-black/70 text-text-secondary px-[10px] py-[4px] rounded text-[11px] flex items-center gap-[6px]">
              <Loader size={12} className="spin" /> Loading clip...
            </div>
          )}
        </div>

        {/* Right panel */}
        <div className="w-[360px] flex flex-col bg-surface border-l border-border overflow-hidden">
          {/* Folder loader */}
          <div className="p-[12px_16px] border-b border-border shrink-0">
            <div className="flex gap-[6px]">
              <input
                type="text"
                value={pathInput}
                onChange={e => setPathInput(e.target.value)}
                onKeyDown={e => e.key === 'Enter' && handleLoad()}
                placeholder="Path to model folder..."
                className="flex-1 px-[10px] py-[6px] text-[12px] bg-bg border border-border rounded text-text outline-none font-mono"
              />
              <button
                onClick={handleLoad}
                disabled={loading || !pathInput.trim()}
                className="px-[12px] py-[6px] text-[12px] bg-input border border-border rounded text-text cursor-pointer flex items-center gap-[4px] hover:bg-hover disabled:opacity-50 disabled:cursor-default"
              >
                <FolderOpen size={12} />
                {loading ? 'Loading...' : 'Load'}
              </button>
            </div>
            {error && (
              <div className="mt-[6px] text-[11px] text-danger">{error}</div>
            )}
          </div>

          {/* Playback controls */}
          {model && (
            <div className="px-[16px] py-[8px] border-b border-border flex items-center gap-[10px] shrink-0">
              <button
                onClick={() => setAnimationPlaying(!animationPlaying)}
                disabled={activeClipIndex < 0}
                className="px-[14px] py-[5px] text-[12px] bg-input border border-border rounded text-text cursor-pointer flex items-center gap-[5px] hover:bg-hover disabled:opacity-50 disabled:cursor-default"
              >
                {animationPlaying
                  ? <><Pause size={11} /> Pause</>
                  : <><Play size={11} /> Play</>}
              </button>
              <span className="text-[11px] text-text-secondary">
                {activeClipIndex >= 0 ? `${activeClipIndex + 1} / ${clips.length}` : 'Select a clip'}
              </span>
            </div>
          )}

          {/* Clip list */}
          <div className="flex-1 overflow-y-auto py-[4px]">
            {clips.length === 0 && model && (
              <div className="p-[20px_16px] text-text-disabled text-[12px] text-center">
                No clips in manifest
              </div>
            )}
            {clips.map((clip, i) => (
              <ClipRow
                key={clip.id}
                clip={clip}
                index={i}
                active={i === activeClipIndex}
                onClick={() => selectClip(i)}
              />
            ))}
          </div>

          {/* Tag editor for selected clip */}
          {selectedClip && (
            <TagEditor
              clip={selectedClip}
              clipIndex={activeClipIndex}
              onTag={tagClip}
            />
          )}

          {/* Action bar */}
          {model && (
            <div className="px-[16px] py-[10px] border-t border-border flex gap-[8px] shrink-0">
              <button
                onClick={autoTag}
                className="flex-1 py-[7px] text-[12px] bg-input border border-border rounded text-text cursor-pointer flex items-center justify-center gap-[5px] hover:bg-hover"
              >
                <Wand2 size={12} /> Auto-tag
              </button>
              <button
                onClick={save}
                disabled={!dirty || saving}
                className="flex-1 py-[7px] text-[12px] border rounded flex items-center justify-center gap-[5px] disabled:opacity-60 disabled:cursor-default"
                style={{
                  background: dirty ? 'var(--color-success)' : 'var(--color-input)',
                  borderColor: dirty ? 'var(--color-success)' : 'var(--color-border)',
                  color: dirty ? '#1e1e1e' : 'var(--color-text-secondary)',
                  cursor: dirty ? 'pointer' : 'default',
                  fontWeight: dirty ? 600 : 400,
                }}
              >
                <Save size={12} /> {saving ? 'Saving...' : 'Save'}
              </button>
            </div>
          )}
        </div>
      </div>
    </div>
  )
}

function ClipRow({ clip, index, active, onClick }: {
  clip: SplitManifestClip
  index: number
  active: boolean
  onClick: () => void
}) {
  const tagged = !!clip.semanticName
  return (
    <div
      onClick={onClick}
      className="flex items-center px-[16px] py-[7px] cursor-pointer transition-colors"
      style={{
        background: active ? 'var(--color-active)' : 'transparent',
        borderLeft: active ? '3px solid var(--color-accent)' : '3px solid transparent',
      }}
      onMouseEnter={e => { if (!active) e.currentTarget.style.background = 'var(--color-hover)' }}
      onMouseLeave={e => { if (!active) e.currentTarget.style.background = 'transparent' }}
    >
      <div className="flex-1 min-w-0">
        <div className="flex items-center gap-[6px]">
          <span className="text-[12px] font-mono"
            style={{ color: active ? '#e0e0e0' : '#aaa' }}
          >
            {clip.id}
          </span>
          {tagged && (
            <span className="text-[10px] px-[6px] py-[1px] rounded-[3px] font-medium"
              style={{ background: 'rgba(74, 222, 128, 0.15)', color: 'var(--color-success)' }}
            >
              {clip.semanticName}
            </span>
          )}
        </div>
        <div className="text-[10px] text-text-disabled mt-[2px]">
          {clip.frameCount} frames &middot; {clip.sourceName}
        </div>
      </div>
    </div>
  )
}

function TagEditor({ clip, clipIndex, onTag }: {
  clip: SplitManifestClip
  clipIndex: number
  onTag: (index: number, name: string | null) => void
}) {
  const [customMode, setCustomMode] = useState(false)
  const [customValue, setCustomValue] = useState('')

  const handleSelect = (value: string) => {
    if (value === '__custom__') {
      setCustomMode(true)
      setCustomValue(clip.semanticName ?? '')
    } else if (value === '__clear__') {
      onTag(clipIndex, null)
      setCustomMode(false)
    } else {
      onTag(clipIndex, value)
      setCustomMode(false)
    }
  }

  const handleCustomSubmit = () => {
    const v = customValue.trim()
    if (v) onTag(clipIndex, v)
    setCustomMode(false)
  }

  return (
    <div className="p-[10px_16px] border-t border-border shrink-0">
      <div className="text-[11px] text-text-secondary mb-[6px] flex items-center gap-[4px]">
        <Tag size={11} /> Tag: {clip.id}
      </div>
      {customMode ? (
        <div className="flex gap-[4px]">
          <input
            autoFocus
            value={customValue}
            onChange={e => setCustomValue(e.target.value)}
            onKeyDown={e => e.key === 'Enter' && handleCustomSubmit()}
            placeholder="Custom tag name..."
            className="flex-1 px-[8px] py-[5px] text-[12px] bg-bg border border-border rounded text-text outline-none"
          />
          <button
            onClick={handleCustomSubmit}
            className="px-[10px] py-[5px] text-[11px] rounded cursor-pointer"
            style={{ background: 'rgba(74,222,128,0.2)', border: '1px solid var(--color-success)', color: 'var(--color-success)' }}
          >
            Set
          </button>
        </div>
      ) : (
        <select
          value={clip.semanticName ?? ''}
          onChange={e => handleSelect(e.target.value)}
          className="w-full px-[8px] py-[5px] text-[12px] bg-bg border border-border rounded text-text outline-none"
        >
          <option value="">— No tag —</option>
          {SEMANTIC_TAGS.map(tag => (
            <option key={tag} value={tag}>{tag}</option>
          ))}
          <option value="__custom__">Custom...</option>
          {clip.semanticName && <option value="__clear__">Clear tag</option>}
        </select>
      )}
      {clip.semanticSource && (
        <div className="text-[10px] text-text-disabled mt-[4px]">
          Source: {clip.semanticSource}
        </div>
      )}
    </div>
  )
}
