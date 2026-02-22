import { useState } from 'react';
import { useExportPage } from '../hooks/useExportPage';
import { ArchiveSettingsCard } from '../components/export/ArchiveSettingsCard';
import { ProgressBar } from '../components/export/ProgressBar';
import { ConsoleOutput } from '../components/export/ConsoleOutput';
import { Package } from 'lucide-react';

export function ExportPage() {
    const vm = useExportPage();
    const [consoleCollapsed, setConsoleCollapsed] = useState(false);

    return (
        <div className="flex flex-col h-full">
            {/* Content area */}
            <div className={`p-[20px] flex flex-col gap-[16px] ${consoleCollapsed ? 'flex-1 overflow-y-auto' : 'flex-shrink-0'}`}>
                {/* Page header */}
                <div className="flex items-center gap-[10px]">
                    <Package size={18} className="text-accent" />
                    <h1 className="text-[16px] font-semibold text-text m-0">Export</h1>
                    <span className="text-[11px] text-text-disabled bg-border/40 px-[8px] py-[2px] rounded-[4px]">
                        TRPAK → DAE
                    </span>
                </div>

                {/* Settings + Start */}
                <ArchiveSettingsCard
                    arcPath={vm.arcPath}
                    onSetArcPath={vm.setArcPath}
                    outputDir={vm.outputDir}
                    onSetOutputDir={vm.setOutputDir}
                    exportMode={vm.exportMode}
                    onSetExportMode={vm.setExportMode}
                    parallelJobs={vm.parallelJobs}
                    onSetParallelJobs={vm.setParallelJobs}
                    maxModels={vm.maxModels}
                    onSetMaxModels={vm.setMaxModels}
                    exportPhase={vm.exportPhase}
                    onBrowseArc={vm.handleBrowseArc}
                    onBrowseOutput={vm.handleBrowseOutput}
                    onStart={vm.handleStart}
                    onStop={vm.handleStop}
                    onReset={vm.handleReset}
                />

                {/* Progress */}
                <ProgressBar
                    exportPhase={vm.exportPhase}
                    pct={vm.pct}
                    success={vm.success}
                    failed={vm.failed}
                    skipped={vm.skipped}
                    processed={vm.processed}
                    total={vm.total}
                />
            </div>

            {/* Console */}
            <ConsoleOutput
                logs={vm.logs}
                logEndRef={vm.logEndRef}
                onClear={vm.clearLogs}
                collapsed={consoleCollapsed}
                onToggleCollapse={() => setConsoleCollapsed(!consoleCollapsed)}
            />
        </div>
    );
}
