import { useStore } from '../../store';

export function FrameStrip() {
  const frames = useStore((s) => s.frames);
  const currentFrame = useStore((s) => s.currentFrame);
  const setCurrentFrame = useStore((s) => s.setCurrentFrame);

  if (frames.length === 0) return null;

  return (
    <div className="border-t border-border bg-surface px-[8px] py-[6px]">
      <div className="flex items-center gap-[4px] overflow-x-auto">
        {frames.map((svg, i) => (
          <button
            key={i}
            onClick={() => setCurrentFrame(i)}
            className="cursor-pointer"
            style={{
              flexShrink: 0,
              width: 48,
              height: 48,
              display: 'flex',
              alignItems: 'center',
              justifyContent: 'center',
              borderRadius: 3,
              background: '#0f0f23',
              border: i === currentFrame ? '2px solid #094771' : '1px solid #2d2d2d',
            }}
          >
            <div
              className="sprite-render"
              style={{ width: 32, height: 32 }}
              dangerouslySetInnerHTML={{ __html: svg }}
            />
          </button>
        ))}
      </div>
      <div className="text-[10px] text-text-secondary text-center mt-[4px]">
        Frame {currentFrame + 1} / {frames.length}
      </div>
    </div>
  );
}
