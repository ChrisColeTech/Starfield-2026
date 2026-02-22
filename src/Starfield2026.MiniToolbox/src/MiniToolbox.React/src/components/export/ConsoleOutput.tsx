import React, { useState } from 'react';
import { ChevronUp, ChevronDown, Copy, Trash2, Check, Terminal } from 'lucide-react';

interface ConsoleOutputProps {
    logs: string[];
    logEndRef: React.RefObject<HTMLDivElement | null>;
    onClear: () => void;
    collapsed: boolean;
    onToggleCollapse: () => void;
}

export function ConsoleOutput({ logs, logEndRef, onClear, collapsed, onToggleCollapse }: ConsoleOutputProps) {
    const [copied, setCopied] = useState(false);

    const handleCopy = async () => {
        if (logs.length === 0) return;
        await navigator.clipboard.writeText(logs.join('\n'));
        setCopied(true);
        setTimeout(() => setCopied(false), 1500);
    };

    if (collapsed) {
        // Collapsed: just a thin bar pinned to the bottom
        return (
            <div className="h-[32px] flex items-center px-[12px] bg-surface border-t border-border shrink-0 select-none">
                <Terminal size={12} className="text-text-secondary mr-[6px]" />
                <span className="text-[11px] font-semibold uppercase tracking-[0.5px] text-text-secondary">Console</span>
                {logs.length > 0 && (
                    <span className="text-[10px] text-text-disabled ml-[6px] bg-border/50 px-[5px] py-[1px] rounded-[3px]">
                        {logs.length}
                    </span>
                )}
                <div className="flex-1" />
                <button
                    onClick={onToggleCollapse}
                    className="h-[22px] w-[22px] bg-transparent border-none cursor-pointer text-text-secondary hover:text-text hover:bg-hover rounded-[3px] flex items-center justify-center transition-colors"
                    title="Expand console"
                >
                    <ChevronUp size={13} />
                </button>
            </div>
        );
    }

    // Expanded: full console panel
    return (
        <div className="flex-1 min-h-[120px] flex flex-col border-t border-border">
            {/* Header */}
            <div className="h-[32px] flex items-center px-[12px] bg-surface border-b border-border shrink-0 select-none">
                <Terminal size={12} className="text-text-secondary mr-[6px]" />
                <span className="text-[11px] font-semibold uppercase tracking-[0.5px] text-text-secondary">Console</span>
                {logs.length > 0 && (
                    <span className="text-[10px] text-text-disabled ml-[6px] bg-border/50 px-[5px] py-[1px] rounded-[3px]">
                        {logs.length}
                    </span>
                )}
                <div className="flex-1" />
                <div className="flex items-center gap-[2px] mr-[4px]">
                    <button
                        onClick={handleCopy}
                        disabled={logs.length === 0}
                        className="h-[22px] px-[6px] bg-transparent border-none cursor-pointer text-text-secondary hover:text-text hover:bg-hover rounded-[3px] flex items-center gap-[4px] text-[10px] transition-colors disabled:opacity-30 disabled:cursor-default"
                        title="Copy to clipboard"
                    >
                        {copied ? <Check size={11} className="text-success" /> : <Copy size={11} />}
                        {copied ? 'Copied' : 'Copy'}
                    </button>
                    <button
                        onClick={onClear}
                        disabled={logs.length === 0}
                        className="h-[22px] px-[6px] bg-transparent border-none cursor-pointer text-text-secondary hover:text-text hover:bg-hover rounded-[3px] flex items-center gap-[4px] text-[10px] transition-colors disabled:opacity-30 disabled:cursor-default"
                        title="Clear console"
                    >
                        <Trash2 size={11} />
                        Clear
                    </button>
                </div>
                <button
                    onClick={onToggleCollapse}
                    className="h-[22px] w-[22px] bg-transparent border-none cursor-pointer text-text-secondary hover:text-text hover:bg-hover rounded-[3px] flex items-center justify-center transition-colors"
                    title="Collapse console"
                >
                    <ChevronDown size={13} />
                </button>
            </div>

            {/* Log content */}
            <div className="flex-1 overflow-y-auto bg-bg p-[10px] font-mono text-[11px] leading-[18px]">
                {logs.length === 0 ? (
                    <span className="text-text-disabled italic">Ready.</span>
                ) : (
                    logs.map((log, i) => {
                        let color = 'text-text-secondary';
                        if (log.startsWith('[ERR]')) color = 'text-danger';
                        else if (log.startsWith('[OK]') || log.startsWith('[DONE]')) color = 'text-success';
                        else if (log.startsWith('[SKIP]') || log.startsWith('[STOP]')) color = 'text-warning';
                        return (
                            <div key={i} className={`${color} py-[1px]`}>{log}</div>
                        );
                    })
                )}
                <div ref={logEndRef} />
            </div>
        </div>
    );
}
