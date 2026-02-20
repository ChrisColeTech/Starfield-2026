import { PreviewCanvas } from '../preview/PreviewCanvas';
import { FrameStrip } from '../preview/FrameStrip';
import { AnimationPlayer } from '../preview/AnimationPlayer';

export function MainContent() {
  return (
    <main className="flex-1 flex flex-col min-w-0 min-h-0 bg-bg">
      <div className="flex-1 flex items-center justify-center p-[16px]">
        <PreviewCanvas />
      </div>
      <AnimationPlayer />
      <FrameStrip />
    </main>
  );
}
