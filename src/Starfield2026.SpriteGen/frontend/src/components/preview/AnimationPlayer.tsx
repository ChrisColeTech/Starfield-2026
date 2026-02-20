import { useEffect } from 'react';
import { ChevronLeft, ChevronRight, Play, Pause } from 'lucide-react';
import { useStore } from '../../store';

const btnStyle: React.CSSProperties = {
  display: 'flex',
  alignItems: 'center',
  justifyContent: 'center',
  padding: 4,
  background: 'none',
  border: 'none',
  color: '#808080',
  cursor: 'pointer',
};

export function AnimationPlayer() {
  const frames = useStore((s) => s.frames);
  const isPlaying = useStore((s) => s.isPlaying);
  const togglePlayback = useStore((s) => s.togglePlayback);
  const currentFrame = useStore((s) => s.currentFrame);
  const setCurrentFrame = useStore((s) => s.setCurrentFrame);
  const playbackSpeed = useStore((s) => s.playbackSpeed);

  useEffect(() => {
    if (!isPlaying || frames.length <= 1) return;
    const id = setInterval(() => {
      const cur = useStore.getState().currentFrame;
      useStore.getState().setCurrentFrame((cur + 1) % frames.length);
    }, playbackSpeed);
    return () => clearInterval(id);
  }, [isPlaying, frames.length, playbackSpeed]);

  if (frames.length === 0) return null;

  return (
    <div className="flex items-center justify-center gap-[8px] py-[4px] border-t border-border bg-surface">
      <button
        onClick={() => setCurrentFrame(Math.max(0, currentFrame - 1))}
        disabled={currentFrame === 0}
        style={{ ...btnStyle, opacity: currentFrame === 0 ? 0.3 : 1 }}
      >
        <ChevronLeft size={16} />
      </button>

      <button
        onClick={togglePlayback}
        style={{
          ...btnStyle,
          width: 28,
          height: 28,
          borderRadius: '50%',
          background: '#094771',
          color: '#e0e0e0',
        }}
      >
        {isPlaying ? <Pause size={14} /> : <Play size={14} />}
      </button>

      <button
        onClick={() => setCurrentFrame(Math.min(frames.length - 1, currentFrame + 1))}
        disabled={currentFrame === frames.length - 1}
        style={{ ...btnStyle, opacity: currentFrame === frames.length - 1 ? 0.3 : 1 }}
      >
        <ChevronRight size={16} />
      </button>
    </div>
  );
}
