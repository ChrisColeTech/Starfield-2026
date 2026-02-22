import {
    Play, SkipBack, SkipForward, ChevronLeft, ChevronRight,
} from 'lucide-react';

interface TransportBarProps {
    disabled: boolean;
}

export function TransportBar({ disabled }: TransportBarProps) {
    return (
        <div className="h-[44px] bg-surface border-t border-border flex items-center px-[16px] gap-[8px] shrink-0">
            <button disabled={disabled} className="w-[28px] h-[28px] flex items-center justify-center bg-transparent border border-border rounded-[2px] text-text-secondary hover:bg-hover hover:text-text cursor-pointer disabled:opacity-30">
                <SkipBack size={14} />
            </button>
            <button disabled={disabled} className="w-[28px] h-[28px] flex items-center justify-center bg-transparent border border-border rounded-[2px] text-text-secondary hover:bg-hover hover:text-text cursor-pointer disabled:opacity-30">
                <ChevronLeft size={14} />
            </button>
            <button disabled={disabled} className="w-[32px] h-[32px] flex items-center justify-center bg-active border-none rounded-[4px] text-text cursor-pointer hover:opacity-90 disabled:opacity-30">
                <Play size={16} />
            </button>
            <button disabled={disabled} className="w-[28px] h-[28px] flex items-center justify-center bg-transparent border border-border rounded-[2px] text-text-secondary hover:bg-hover hover:text-text cursor-pointer disabled:opacity-30">
                <ChevronRight size={14} />
            </button>
            <button disabled={disabled} className="w-[28px] h-[28px] flex items-center justify-center bg-transparent border border-border rounded-[2px] text-text-secondary hover:bg-hover hover:text-text cursor-pointer disabled:opacity-30">
                <SkipForward size={14} />
            </button>

            <div className="flex-1 mx-[12px]">
                <div className="h-[4px] bg-bg rounded-[2px] overflow-hidden">
                    <div className="h-full w-0 bg-accent rounded-[2px]" />
                </div>
            </div>

            <span className="text-[11px] text-text-secondary font-mono w-[80px] text-right">
                0:00 / 0:00
            </span>
        </div>
    );
}
