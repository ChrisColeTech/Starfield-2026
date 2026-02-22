import { Play, Square, RotateCcw, Zap } from 'lucide-react';
import type { ExportPhase } from '../../types';

interface ExportControlsProps {
    selectedModelCount: number;
    exportPhase: ExportPhase;
    onStart: () => void;
    onStop: () => void;
    onReset: () => void;
}

export function ExportControls({ selectedModelCount, exportPhase, onStart, onStop, onReset }: ExportControlsProps) {
    const isIdle = exportPhase === 'idle';
    const isExporting = exportPhase === 'exporting';

    return (
        <div className="flex items-center gap-[8px]">
            {/* Primary action */}
            <button
                onClick={onStart}
                disabled={selectedModelCount === 0 || isExporting}
                className="h-[32px] px-[20px] bg-accent text-white text-[12px] font-semibold border-none rounded-[4px] cursor-pointer hover:brightness-110 disabled:opacity-30 disabled:cursor-default flex items-center gap-[6px] transition-all shadow-[0_2px_8px_rgba(86,156,214,0.2)]"
            >
                {isExporting
                    ? <><Zap size={13} className="animate-pulse" /> Exporting...</>
                    : <><Play size={13} /> Start Export</>}
            </button>

            {isExporting && (
                <button
                    onClick={onStop}
                    className="h-[32px] px-[14px] bg-danger/15 text-danger text-[12px] font-medium border border-danger/30 rounded-[4px] cursor-pointer hover:bg-danger/25 flex items-center gap-[5px] transition-colors"
                >
                    <Square size={12} /> Cancel
                </button>
            )}

            {!isIdle && !isExporting && (
                <button
                    onClick={onReset}
                    className="h-[32px] px-[14px] bg-transparent text-text-secondary text-[12px] border border-border rounded-[4px] cursor-pointer hover:bg-hover hover:text-text flex items-center gap-[5px] transition-colors"
                >
                    <RotateCcw size={12} /> Reset
                </button>
            )}

            {/* Model count badge */}
            {selectedModelCount > 0 && isIdle && (
                <span className="text-[11px] text-text-secondary ml-[4px]">
                    {selectedModelCount} model{selectedModelCount !== 1 ? 's' : ''} ready
                </span>
            )}
        </div>
    );
}
