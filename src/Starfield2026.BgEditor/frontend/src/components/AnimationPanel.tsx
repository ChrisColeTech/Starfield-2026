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
      <div className="px-3.5 py-2.5 border-b border-border text-xs font-semibold text-foreground shrink-0 flex justify-between items-center">
        <span>Animations</span>
        <span className="text-[10px] text-muted-foreground/50 font-normal">
          {animations.length} clip{animations.length !== 1 ? 's' : ''}
        </span>
      </div>

      {/* Play/Pause + active clip name */}
      <div className="px-3.5 py-2 shrink-0 flex items-center gap-2">
        <button
          onClick={() => setAnimationPlaying(!animationPlaying)}
          className="px-3.5 py-1.5 bg-input border border-border rounded text-foreground text-xs cursor-pointer flex items-center gap-1.5 shrink-0 hover:bg-muted"
        >
          {animationPlaying ? (
            <><Pause size={12} strokeWidth={2} /> Pause</>
          ) : (
            <><Play size={12} strokeWidth={2} /> Play</>
          )}
        </button>
        <div className="text-[11px] text-muted-foreground overflow-hidden text-ellipsis whitespace-nowrap min-w-0">
          {activeClipIndex + 1}/{animations.length}: {animations[activeClipIndex]?.name || `Clip ${activeClipIndex}`}
        </div>
      </div>

      {/* Clip list */}
      <div className="flex-1 min-h-0 overflow-y-auto px-3.5 pb-2">
        {animations.map((clip, i) => (
          <div
            key={clip.name + i}
            onClick={() => setActiveClipIndex(i)}
            className={`flex items-center justify-between px-2.5 py-1.5 mb-0.5 rounded cursor-pointer border transition-colors ${i === activeClipIndex
                ? 'bg-primary/10 border-primary'
                : 'border-transparent hover:bg-muted'
              }`}
          >
            <div className="flex-1 min-w-0">
              <div className={`text-[11px] whitespace-nowrap overflow-hidden text-ellipsis ${i === activeClipIndex ? 'text-foreground' : 'text-muted-foreground'
                }`}>
                {clip.name || `Clip ${i}`}
              </div>
              <div className="text-[9px] text-muted-foreground/50">
                {clip.duration.toFixed(2)}s | {clip.tracks.length} tracks
              </div>
            </div>
            {i === activeClipIndex && (
              <div className={`w-1.5 h-1.5 rounded-full shrink-0 ml-2 ${animationPlaying ? 'bg-primary' : 'bg-muted-foreground/50'
                }`} />
            )}
          </div>
        ))}
      </div>
    </div>
  )
}
