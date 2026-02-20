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
    <div style={{
      display: 'flex',
      flexDirection: 'column',
      height: '100%',
      overflow: 'hidden',
    }}>
      {/* Header */}
      <div style={{
        padding: '12px 20px',
        background: '#12122a',
        borderBottom: '1px solid #2a2a4a',
        display: 'flex',
        justifyContent: 'space-between',
        alignItems: 'center',
        flexShrink: 0,
      }}>
        <h1 style={{ margin: 0, fontSize: 16, color: '#e0e0e0' }}>Animation Editor</h1>
        <div style={{ display: 'flex', alignItems: 'center', gap: 8 }}>
          {dirty && (
            <span style={{
              width: 8, height: 8, borderRadius: '50%',
              background: '#ff8844', display: 'inline-block',
            }} title="Unsaved changes" />
          )}
          {model && (
            <span style={{ fontSize: 12, color: '#888' }}>
              {model.name} — {clips.length} clips
            </span>
          )}
        </div>
      </div>

      {/* Main area */}
      <div style={{ flex: 1, display: 'flex', overflow: 'hidden' }}>
        {/* Viewport */}
        <div style={{ flex: 1, position: 'relative' }}>
          {scene ? (
            <Viewport />
          ) : (
            <div style={{
              display: 'flex', flexDirection: 'column', height: '100%',
              alignItems: 'center', justifyContent: 'center',
              color: '#666', fontSize: 14, gap: 8,
            }}>
              <Film size={32} strokeWidth={1.5} />
              <span>Load a model folder to begin</span>
            </div>
          )}
          {clipLoading && (
            <div style={{
              position: 'absolute', top: 8, left: 8,
              background: 'rgba(0,0,0,0.7)', color: '#aaa',
              padding: '4px 10px', borderRadius: 4, fontSize: 11,
              display: 'flex', alignItems: 'center', gap: 6,
            }}>
              <Loader size={12} className="spin" /> Loading clip...
            </div>
          )}
        </div>

        {/* Right panel */}
        <div style={{
          width: 360,
          display: 'flex',
          flexDirection: 'column',
          background: '#16162a',
          borderLeft: '1px solid #2a2a4a',
          overflow: 'hidden',
        }}>
          {/* Folder loader */}
          <div style={{
            padding: '12px 16px',
            borderBottom: '1px solid #2a2a4a',
            flexShrink: 0,
          }}>
            <div style={{ display: 'flex', gap: 6 }}>
              <input
                type="text"
                value={pathInput}
                onChange={e => setPathInput(e.target.value)}
                onKeyDown={e => e.key === 'Enter' && handleLoad()}
                placeholder="Path to model folder..."
                style={{
                  flex: 1, padding: '6px 10px', fontSize: 12,
                  background: '#0c0c1a', border: '1px solid #2a2a4a',
                  borderRadius: 4, color: '#ccc', outline: 'none',
                  fontFamily: 'monospace',
                }}
              />
              <button
                onClick={handleLoad}
                disabled={loading || !pathInput.trim()}
                style={{
                  padding: '6px 12px', fontSize: 12,
                  background: '#2a3a4a', border: '1px solid #3a3a6a',
                  borderRadius: 4, color: '#ccc', cursor: 'pointer',
                  display: 'flex', alignItems: 'center', gap: 4,
                  opacity: loading || !pathInput.trim() ? 0.5 : 1,
                }}
              >
                <FolderOpen size={12} />
                {loading ? 'Loading...' : 'Load'}
              </button>
            </div>
            {error && (
              <div style={{ marginTop: 6, fontSize: 11, color: '#ff6666' }}>{error}</div>
            )}
          </div>

          {/* Playback controls */}
          {model && (
            <div style={{
              padding: '8px 16px',
              borderBottom: '1px solid #2a2a4a',
              display: 'flex', alignItems: 'center', gap: 10,
              flexShrink: 0,
            }}>
              <button
                onClick={() => setAnimationPlaying(!animationPlaying)}
                disabled={activeClipIndex < 0}
                style={{
                  padding: '5px 14px', fontSize: 12,
                  background: animationPlaying ? '#3a2a4a' : '#2a3a4a',
                  border: '1px solid #3a3a6a', borderRadius: 4,
                  color: '#ccc', cursor: 'pointer',
                  display: 'flex', alignItems: 'center', gap: 5,
                }}
              >
                {animationPlaying
                  ? <><Pause size={11} /> Pause</>
                  : <><Play size={11} /> Play</>}
              </button>
              <span style={{ fontSize: 11, color: '#888' }}>
                {activeClipIndex >= 0 ? `${activeClipIndex + 1} / ${clips.length}` : 'Select a clip'}
              </span>
            </div>
          )}

          {/* Clip list */}
          <div style={{ flex: 1, overflowY: 'auto', padding: '4px 0' }}>
            {clips.length === 0 && model && (
              <div style={{ padding: '20px 16px', color: '#666', fontSize: 12, textAlign: 'center' }}>
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
            <div style={{
              padding: '10px 16px',
              borderTop: '1px solid #2a2a4a',
              display: 'flex', gap: 8,
              flexShrink: 0,
            }}>
              <button
                onClick={autoTag}
                style={{
                  flex: 1, padding: '7px 0', fontSize: 12,
                  background: '#2a2a3a', border: '1px solid #3a3a6a',
                  borderRadius: 4, color: '#ccc', cursor: 'pointer',
                  display: 'flex', alignItems: 'center', justifyContent: 'center', gap: 5,
                }}
              >
                <Wand2 size={12} /> Auto-tag
              </button>
              <button
                onClick={save}
                disabled={!dirty || saving}
                style={{
                  flex: 1, padding: '7px 0', fontSize: 12,
                  background: dirty ? '#2a4a3a' : '#2a2a3a',
                  border: `1px solid ${dirty ? '#3a6a4a' : '#3a3a6a'}`,
                  borderRadius: 4,
                  color: dirty ? '#8fdf8f' : '#888',
                  cursor: dirty ? 'pointer' : 'default',
                  display: 'flex', alignItems: 'center', justifyContent: 'center', gap: 5,
                  opacity: !dirty || saving ? 0.6 : 1,
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
      style={{
        display: 'flex', alignItems: 'center',
        padding: '7px 16px', cursor: 'pointer',
        background: active ? '#2a2a5a' : 'transparent',
        borderLeft: active ? '3px solid #8c8cff' : '3px solid transparent',
        transition: 'background 0.1s',
      }}
      onMouseEnter={e => { if (!active) e.currentTarget.style.background = '#1e1e3a' }}
      onMouseLeave={e => { if (!active) e.currentTarget.style.background = 'transparent' }}
    >
      <div style={{ flex: 1, minWidth: 0 }}>
        <div style={{ display: 'flex', alignItems: 'center', gap: 6 }}>
          <span style={{
            fontSize: 12,
            color: active ? '#e0e0e0' : '#aaa',
            fontFamily: 'monospace',
          }}>
            {clip.id}
          </span>
          {tagged && (
            <span style={{
              fontSize: 10, padding: '1px 6px',
              background: '#1a3a2a', color: '#6fbf6f',
              borderRadius: 3, fontWeight: 500,
            }}>
              {clip.semanticName}
            </span>
          )}
        </div>
        <div style={{ fontSize: 10, color: '#555', marginTop: 2 }}>
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
    <div style={{
      padding: '10px 16px',
      borderTop: '1px solid #2a2a4a',
      flexShrink: 0,
    }}>
      <div style={{
        fontSize: 11, color: '#888', marginBottom: 6,
        display: 'flex', alignItems: 'center', gap: 4,
      }}>
        <Tag size={11} /> Tag: {clip.id}
      </div>
      {customMode ? (
        <div style={{ display: 'flex', gap: 4 }}>
          <input
            autoFocus
            value={customValue}
            onChange={e => setCustomValue(e.target.value)}
            onKeyDown={e => e.key === 'Enter' && handleCustomSubmit()}
            placeholder="Custom tag name..."
            style={{
              flex: 1, padding: '5px 8px', fontSize: 12,
              background: '#0c0c1a', border: '1px solid #3a3a6a',
              borderRadius: 4, color: '#ccc', outline: 'none',
            }}
          />
          <button
            onClick={handleCustomSubmit}
            style={{
              padding: '5px 10px', fontSize: 11,
              background: '#2a4a3a', border: '1px solid #3a6a4a',
              borderRadius: 4, color: '#8fdf8f', cursor: 'pointer',
            }}
          >
            Set
          </button>
        </div>
      ) : (
        <select
          value={clip.semanticName ?? ''}
          onChange={e => handleSelect(e.target.value)}
          style={{
            width: '100%', padding: '5px 8px', fontSize: 12,
            background: '#0c0c1a', border: '1px solid #3a3a6a',
            borderRadius: 4, color: '#ccc', outline: 'none',
          }}
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
        <div style={{ fontSize: 10, color: '#555', marginTop: 4 }}>
          Source: {clip.semanticSource}
        </div>
      )}
    </div>
  )
}
