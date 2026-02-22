import { useEditorStore } from '../store/editorStore'
import { Pause, Play } from 'lucide-react'

export default function AnimationPanel() {
  const animations = useEditorStore(s => s.animations)
  const animationPlaying = useEditorStore(s => s.animationPlaying)
  const activeClipIndex = useEditorStore(s => s.activeClipIndex)
  const setAnimationPlaying = useEditorStore(s => s.setAnimationPlaying)
  const setActiveClipIndex = useEditorStore(s => s.setActiveClipIndex)

  if (animations.length === 0) return null

  return (
    <div className="flex flex-col overflow-hidden border-t border-border flex-1 min-h-0">
      {/* Header */}
      <div className="p-[10px_14px] border-b border-border text-[12px] font-semibold text-text shrink-0 flex justify-between items-center">
        <span>Animations</span>
        <span className="text-[10px] text-text-disabled font-normal">
          {animations.length} clip{animations.length !== 1 ? 's' : ''}
        </span>
      </div>

      {/* Play/Pause + active clip name */}
      <div className="p-[8px_14px] shrink-0 flex items-center gap-[8px]">
        <button
          onClick={() => setAnimationPlaying(!animationPlaying)}
          className="px-[14px] py-[6px] bg-input border border-border rounded text-text text-[12px] cursor-pointer flex items-center gap-[6px] shrink-0 hover:bg-hover"
        >
          {animationPlaying ? (
            <><Pause size={12} strokeWidth={2} /> Pause</>
          ) : (
            <><Play size={12} strokeWidth={2} /> Play</>
          )}
        </button>
        <div className="text-[11px] text-text-secondary overflow-hidden text-ellipsis whitespace-nowrap min-w-0">
          {activeClipIndex + 1}/{animations.length}: {animations[activeClipIndex]?.name || `Clip ${activeClipIndex}`}
        </div>
      </div>

      {/* Clip list */}
      <div className="flex-1 min-h-0 overflow-y-auto p-[0_14px_8px]">
        {animations.map((clip, i) => (
          <div
            key={clip.name + i}
            onClick={() => setActiveClipIndex(i)}
            className="flex items-center justify-between p-[5px_10px] mb-[2px] rounded cursor-pointer"
            style={{
              background: i === activeClipIndex ? 'var(--color-active)' : 'transparent',
              border: i === activeClipIndex ? '1px solid var(--color-accent)' : '1px solid transparent',
            }}
          >
            <div className="flex-1 min-w-0">
              <div className="text-[11px] whitespace-nowrap overflow-hidden text-ellipsis"
                style={{ color: i === activeClipIndex ? '#e0e0e0' : '#aaa' }}
              >
                {clip.name || `Clip ${i}`}
              </div>
              <div className="text-[9px] text-text-disabled">
                {clip.duration.toFixed(2)}s | {clip.tracks.length} tracks
              </div>
            </div>
            {i === activeClipIndex && (
              <div className="w-[6px] h-[6px] rounded-full shrink-0 ml-[8px]"
                style={{ background: animationPlaying ? 'var(--color-accent)' : 'var(--color-text-disabled)' }}
              />
            )}
          </div>
        ))}
      </div>
    </div>
  )
}
