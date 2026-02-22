import {
    RefreshCw, PanelLeftClose, PanelLeftOpen,
} from 'lucide-react';

interface ClipBrowserProps {
    isOpen: boolean;
    onToggle: () => void;
    clips: string[];
    selectedClip: string;
    onSelectClip: (clip: string) => void;
    folderPath: string;
    onRefresh: () => void;
}

export function ClipBrowser({
    isOpen, onToggle, clips, selectedClip, onSelectClip, folderPath, onRefresh,
}: ClipBrowserProps) {
    return (
        <div
            className="bg-surface border-r border-border flex flex-col shrink-0 overflow-hidden"
            style={{ width: isOpen ? 220 : 28 }}
        >
            <div className="h-[28px] flex items-center justify-between px-[6px] bg-bg border-b border-border">
                {isOpen && (
                    <span className="text-[11px] font-bold uppercase tracking-[0.5px] text-text-secondary ml-[4px]">
                        Clips {clips.length > 0 && `(${clips.length})`}
                    </span>
                )}
                <div className="flex items-center gap-[2px]">
                    {isOpen && folderPath && (
                        <button
                            onClick={onRefresh}
                            className="text-text-secondary hover:text-text bg-transparent border-none cursor-pointer"
                        >
                            <RefreshCw size={11} />
                        </button>
                    )}
                    <button
                        onClick={onToggle}
                        className="text-text-secondary hover:text-text bg-transparent border-none cursor-pointer"
                    >
                        {isOpen ? <PanelLeftClose size={14} /> : <PanelLeftOpen size={14} />}
                    </button>
                </div>
            </div>

            {isOpen && (
                <>
                    <div className="flex-1 overflow-y-auto">
                        {clips.map((clip) => (
                            <button
                                key={clip}
                                onClick={() => onSelectClip(clip)}
                                className="w-full h-[24px] px-[10px] bg-transparent border-none text-left text-[12px] cursor-pointer hover:bg-hover flex items-center truncate"
                                style={{
                                    color: selectedClip === clip ? '#e0e0e0' : '#808080',
                                    background: selectedClip === clip ? '#094771' : undefined,
                                }}
                            >
                                {clip}
                            </button>
                        ))}
                    </div>
                    <div className="h-[24px] flex items-center px-[10px] border-t border-border shrink-0">
                        <span className="text-[10px] text-text-disabled truncate">
                            {folderPath || 'No folder'}
                        </span>
                    </div>
                </>
            )}
        </div>
    );
}
