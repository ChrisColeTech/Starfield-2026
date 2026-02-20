import { ImageOff } from 'lucide-react';
import { useStore } from '../../store';

export function PreviewCanvas() {
  const frames = useStore((s) => s.frames);
  const currentFrame = useStore((s) => s.currentFrame);
  const showGrid = useStore((s) => s.showGrid);
  const svgContent = frames[currentFrame];

  return (
    <div
      className="relative flex items-center justify-center rounded-[3px]"
      style={{
        width: 320,
        height: 320,
        background: '#0f0f23',
        border: '1px solid #2d2d2d',
      }}
    >
      {svgContent ? (
        <>
          <div
            className="sprite-render"
            style={{ transform: 'scale(12)' }}
            dangerouslySetInnerHTML={{ __html: svgContent }}
          />
          {/* Grid overlay */}
          {showGrid && (
            <div
              className="absolute inset-0 pointer-events-none opacity-5"
              style={{
                backgroundImage:
                  'repeating-linear-gradient(0deg,transparent,transparent 19px,#fff 19px,#fff 20px),repeating-linear-gradient(90deg,transparent,transparent 19px,#fff 19px,#fff 20px)',
              }}
            />
          )}
        </>
      ) : (
        <div className="flex flex-col items-center gap-[8px] text-text-disabled">
          <ImageOff size={32} strokeWidth={1} />
          <span className="text-[11px]">No preview</span>
          <span className="text-[10px]">Select a type and click Generate</span>
        </div>
      )}
    </div>
  );
}
