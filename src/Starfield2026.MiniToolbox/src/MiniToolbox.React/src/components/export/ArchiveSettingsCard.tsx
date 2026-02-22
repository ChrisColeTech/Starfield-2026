import { FolderOpen, HardDrive, Settings, Play, Square, RotateCcw } from 'lucide-react';
import type { ExportPhase } from '../../types';

interface ArchiveSettingsCardProps {
    arcPath: string;
    onSetArcPath: (v: string) => void;
    outputDir: string;
    onSetOutputDir: (v: string) => void;
    exportMode: 'baked' | 'split';
    onSetExportMode: (v: 'baked' | 'split') => void;
    parallelJobs: number;
    onSetParallelJobs: (v: number) => void;
    maxModels: number;
    onSetMaxModels: (v: number) => void;
    exportPhase: ExportPhase;
    onBrowseArc: () => void;
    onBrowseOutput: () => void;
    onStart: () => void;
    onStop: () => void;
    onReset: () => void;
}

export function ArchiveSettingsCard({
    arcPath, onSetArcPath, outputDir, onSetOutputDir,
    exportMode, onSetExportMode, parallelJobs, onSetParallelJobs,
    maxModels, onSetMaxModels, exportPhase,
    onBrowseArc, onBrowseOutput, onStart, onStop, onReset,
}: ArchiveSettingsCardProps) {
    const canStart = arcPath.length > 0 && exportPhase === 'idle';

    return (
        <div className="bg-surface border border-border rounded-[6px] p-[16px] flex flex-col gap-[16px]">
            {/* ── Paths Section ── */}
            <div className="flex flex-col gap-[12px]">
                <div className="flex items-center gap-[8px]">
                    <HardDrive size={14} className="text-accent" />
                    <h2 className="text-[12px] font-semibold uppercase tracking-[0.5px] text-text-secondary m-0">
                        Source & Output
                    </h2>
                </div>

                {/* Archive Path */}
                <div className="flex flex-col gap-[4px]">
                    <label className="text-[11px] text-text-secondary font-medium">Archive Path</label>
                    <div className="flex items-center gap-[6px]">
                        <input
                            type="text"
                            value={arcPath}
                            onChange={(e) => onSetArcPath(e.target.value)}
                            placeholder="Select an archive file or folder..."
                            className="flex-1 h-[30px] px-[10px] bg-input border border-border rounded-[4px] text-[12px] text-text outline-none focus:border-accent transition-colors"
                        />
                        <button
                            onClick={onBrowseArc}
                            className="h-[30px] w-[30px] bg-transparent text-text-secondary border border-border rounded-[4px] cursor-pointer hover:bg-hover hover:text-text flex items-center justify-center transition-colors shrink-0"
                            title="Browse..."
                        >
                            <FolderOpen size={13} />
                        </button>
                    </div>
                </div>

                {/* Output Dir */}
                <div className="flex flex-col gap-[4px]">
                    <label className="text-[11px] text-text-secondary font-medium">Output Directory</label>
                    <div className="flex items-center gap-[6px]">
                        <input
                            type="text"
                            value={outputDir}
                            onChange={(e) => onSetOutputDir(e.target.value)}
                            placeholder="D:\path\to\output"
                            className="flex-1 h-[30px] px-[10px] bg-input border border-border rounded-[4px] text-[12px] text-text outline-none focus:border-accent transition-colors"
                        />
                        <button
                            onClick={onBrowseOutput}
                            className="h-[30px] w-[30px] bg-transparent text-text-secondary border border-border rounded-[4px] cursor-pointer hover:bg-hover hover:text-text flex items-center justify-center transition-colors shrink-0"
                            title="Browse..."
                        >
                            <FolderOpen size={13} />
                        </button>
                    </div>
                </div>
            </div>

            {/* ── Export Settings Section ── */}
            <div className="flex flex-col gap-[12px] border-t border-border pt-[14px]">
                <div className="flex items-center gap-[8px]">
                    <Settings size={14} className="text-accent" />
                    <h2 className="text-[12px] font-semibold uppercase tracking-[0.5px] text-text-secondary m-0">
                        Export Settings
                    </h2>
                </div>

                {/* Export Mode — segmented toggle */}
                <div className="flex flex-col gap-[4px]">
                    <label className="text-[11px] text-text-secondary font-medium">Export Mode</label>
                    <div className="flex h-[30px] border border-border rounded-[4px] overflow-hidden w-fit">
                        <button
                            onClick={() => onSetExportMode('baked')}
                            className={`px-[14px] text-[11px] font-medium border-none cursor-pointer transition-colors ${exportMode === 'baked'
                                ? 'bg-accent text-white'
                                : 'bg-input text-text-secondary hover:bg-hover hover:text-text'
                                }`}
                        >
                            Baked (Model + Anims)
                        </button>
                        <button
                            onClick={() => onSetExportMode('split')}
                            className={`px-[14px] text-[11px] font-medium border-none border-l border-border cursor-pointer transition-colors ${exportMode === 'split'
                                ? 'bg-accent text-white'
                                : 'bg-input text-text-secondary hover:bg-hover hover:text-text'
                                }`}
                        >
                            Split (Separate DAEs)
                        </button>
                    </div>
                </div>

                {/* Parallel + Limit — side by side */}
                <div className="flex gap-[20px]">
                    <div className="flex flex-col gap-[4px]">
                        <label className="text-[11px] text-text-secondary font-medium">Parallel Jobs</label>
                        <div className="flex items-center gap-[6px]">
                            <input
                                type="range"
                                min={1} max={16}
                                value={parallelJobs}
                                onChange={(e) => onSetParallelJobs(Number(e.target.value))}
                                className="w-[100px] accent-accent"
                            />
                            <span className="text-[12px] text-text w-[24px] text-center font-mono bg-input border border-border rounded-[3px] h-[24px] leading-[24px]">
                                {parallelJobs}
                            </span>
                        </div>
                    </div>

                    <div className="flex flex-col gap-[4px]">
                        <label className="text-[11px] text-text-secondary font-medium">Model Limit</label>
                        <input
                            type="number"
                            min={0}
                            value={maxModels}
                            onChange={(e) => onSetMaxModels(Number(e.target.value))}
                            placeholder="0 = all"
                            className="w-[64px] h-[30px] px-[6px] bg-input border border-border rounded-[4px] text-[12px] text-text outline-none text-center focus:border-accent transition-colors"
                        />
                        <span className="text-[10px] text-text-disabled">0 = no limit</span>
                    </div>
                </div>
            </div>

            {/* ── Primary Action ── */}
            <div className="flex gap-[8px] border-t border-border pt-[14px]">
                {exportPhase === 'exporting' ? (
                    <button
                        onClick={onStop}
                        className="flex-1 h-[34px] bg-danger/15 text-danger text-[12px] font-semibold border border-danger/30 rounded-[4px] cursor-pointer hover:bg-danger/25 flex items-center justify-center gap-[6px] transition-colors"
                    >
                        <Square size={13} /> Cancel Export
                    </button>
                ) : exportPhase !== 'idle' ? (
                    <button
                        onClick={onReset}
                        className="flex-1 h-[34px] bg-transparent text-text-secondary text-[12px] font-medium border border-border rounded-[4px] cursor-pointer hover:bg-hover hover:text-text flex items-center justify-center gap-[6px] transition-colors"
                    >
                        <RotateCcw size={13} /> Reset
                    </button>
                ) : (
                    <button
                        onClick={onStart}
                        disabled={!canStart}
                        className="flex-1 h-[34px] bg-accent text-white text-[12px] font-semibold border-none rounded-[4px] cursor-pointer hover:brightness-110 disabled:opacity-30 disabled:cursor-default flex items-center justify-center gap-[6px] transition-all shadow-[0_2px_8px_rgba(86,156,214,0.15)]"
                    >
                        <Play size={13} /> Start Export
                    </button>
                )}
            </div>
        </div>
    );
}
