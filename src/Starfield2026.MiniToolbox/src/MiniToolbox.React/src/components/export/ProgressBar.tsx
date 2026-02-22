import { CheckCircle, XCircle, SkipForward, Clock } from 'lucide-react';
import type { ExportPhase } from '../../types';

interface ProgressBarProps {
    exportPhase: ExportPhase;
    pct: number;
    success: number;
    failed: number;
    skipped: number;
    processed: number;
    total: number;
}

export function ProgressBar({ exportPhase, pct, success, failed, skipped, processed, total }: ProgressBarProps) {
    if (exportPhase === 'idle') return null;

    const barColor =
        exportPhase === 'error' ? '#c74e4e'
            : exportPhase === 'complete' ? '#4ade80'
                : exportPhase === 'cancelled' ? '#fbbf24'
                    : '#569cd6';

    const statusLabel =
        exportPhase === 'complete' ? 'Complete'
            : exportPhase === 'error' ? 'Error'
                : exportPhase === 'cancelled' ? 'Cancelled'
                    : 'Exporting';

    const statusColor =
        exportPhase === 'complete' ? 'text-success'
            : exportPhase === 'error' ? 'text-danger'
                : exportPhase === 'cancelled' ? 'text-warning'
                    : 'text-accent';

    return (
        <div className="bg-surface border border-border rounded-[6px] overflow-hidden">
            {/* Progress track */}
            <div className="h-[3px] bg-bg">
                <div
                    className="h-full transition-all duration-300 ease-out"
                    style={{
                        width: `${pct}%`,
                        background: barColor,
                        boxShadow: exportPhase === 'exporting' ? `0 0 8px ${barColor}40` : 'none',
                    }}
                />
            </div>

            {/* Stats row */}
            <div className="flex items-center gap-[16px] px-[14px] py-[8px]">
                <span className={`text-[12px] font-semibold ${statusColor} flex items-center gap-[4px]`}>
                    <Clock size={12} />
                    {statusLabel}
                </span>

                <span className="text-[12px] text-text font-mono">
                    {pct.toFixed(0)}%
                </span>

                <div className="h-[12px] w-[1px] bg-border" />

                <div className="flex gap-[12px] text-[11px]">
                    <span className="text-success flex items-center gap-[3px]">
                        <CheckCircle size={11} /> {success}
                    </span>
                    <span className="text-danger flex items-center gap-[3px]">
                        <XCircle size={11} /> {failed}
                    </span>
                    <span className="text-text-secondary flex items-center gap-[3px]">
                        <SkipForward size={11} /> {skipped}
                    </span>
                </div>

                <div className="flex-1" />

                <span className="text-[11px] text-text-secondary font-mono">
                    {processed} / {total}
                </span>
            </div>
        </div>
    );
}
