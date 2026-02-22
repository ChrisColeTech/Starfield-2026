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
            className="shrink-0 w-[48px] h-[48px] flex items-center justify-center rounded-[3px] bg-bg cursor-pointer"
            style={{
              border: i === currentFrame ? '2px solid var(--color-active)' : '1px solid var(--color-border)',
            }}
          >
            <div
              className="sprite-render w-[32px] h-[32px]"
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
