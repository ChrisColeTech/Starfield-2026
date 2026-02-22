import { Switch, Listbox, ListboxButton, ListboxOption, ListboxOptions } from '@headlessui/react';
import { PanelRightClose, PanelRightOpen, RefreshCw, Move, ZoomIn, Mouse, ChevronDown } from 'lucide-react';
import type { ModelInfo, RenderSettings } from '../../types';

interface PropertiesPanelProps {
    isOpen: boolean;
    onToggle: () => void;
    selectedClip: string;
    modelInfo: ModelInfo | null;
    renderSettings: RenderSettings;
    lightIntensity: number;
    onSetShowWireframe: (v: boolean) => void;
    onSetShowSkeleton: (v: boolean) => void;
    onSetShowGrid: (v: boolean) => void;
    onSetShowTextures: (v: boolean) => void;
    onSetLightIntensity: (v: number) => void;
}

const loopOptions = [
    { value: 'loop', label: 'Loop' },
    { value: 'once', label: 'Play Once' },
    { value: 'pingpong', label: 'Ping Pong' },
];

function RenderToggle({ label, enabled, onChange }: { label: string; enabled: boolean; onChange: (v: boolean) => void }) {
    return (
        <div className="flex items-center justify-between">
            <span className="text-[12px] text-text">{label}</span>
            <Switch
                checked={enabled}
                onChange={onChange}
                className={`relative inline-flex h-[18px] w-[32px] shrink-0 cursor-pointer rounded-full border-2 border-transparent transition-colors duration-200 ease-in-out focus:outline-none ${enabled ? 'bg-accent' : 'bg-border'}`}
            >
                <span
                    className={`pointer-events-none inline-block h-[14px] w-[14px] transform rounded-full bg-white shadow ring-0 transition duration-200 ease-in-out ${enabled ? 'translate-x-[14px]' : 'translate-x-0'}`}
                />
            </Switch>
        </div>
    );
}

export function PropertiesPanel({
    isOpen, onToggle, selectedClip, modelInfo, renderSettings, lightIntensity,
    onSetShowWireframe, onSetShowSkeleton, onSetShowGrid, onSetShowTextures, onSetLightIntensity,
}: PropertiesPanelProps) {
    return (
        <div
            className="bg-surface border-l border-border flex flex-col shrink-0 overflow-hidden"
            style={{ width: isOpen ? 240 : 28 }}
        >
            <div className="h-[28px] flex items-center justify-between px-[6px] bg-bg border-b border-border">
                <button
                    onClick={onToggle}
                    className="text-text-secondary hover:text-text bg-transparent border-none cursor-pointer"
                >
                    {isOpen ? <PanelRightClose size={14} /> : <PanelRightOpen size={14} />}
                </button>
                {isOpen && (
                    <span className="text-[11px] font-bold uppercase tracking-[0.5px] text-text-secondary mr-[4px]">
                        Properties
                    </span>
                )}
            </div>

            {isOpen && (
                <>
                    {/* Model Info */}
                    <div className="border-b border-border">
                        <div className="h-[24px] flex items-center px-[10px] bg-bg border-b border-border">
                            <span className="text-[11px] font-bold uppercase tracking-[0.5px] text-text-secondary">Model</span>
                        </div>
                        <div className="p-[10px] text-[12px]">
                            {selectedClip ? (
                                <div className="flex flex-col gap-[4px]">
                                    <div className="flex justify-between"><span className="text-text-secondary">Name</span><span className="text-text truncate ml-[8px]">{selectedClip.replace('.dae', '')}</span></div>
                                    <div className="flex justify-between"><span className="text-text-secondary">Vertices</span><span className="text-text">{modelInfo?.vertices.toLocaleString() ?? '—'}</span></div>
                                    <div className="flex justify-between"><span className="text-text-secondary">Faces</span><span className="text-text">{modelInfo?.faces.toLocaleString() ?? '—'}</span></div>
                                    <div className="flex justify-between"><span className="text-text-secondary">Bones</span><span className="text-text">{modelInfo?.bones ?? '—'}</span></div>
                                    <div className="flex justify-between"><span className="text-text-secondary">Materials</span><span className="text-text">{modelInfo?.materials ?? '—'}</span></div>
                                </div>
                            ) : (
                                <span className="text-text-disabled text-[11px]">No model selected</span>
                            )}
                        </div>
                    </div>

                    {/* Animation Controls */}
                    <div className="border-b border-border">
                        <div className="h-[24px] flex items-center px-[10px] bg-bg border-b border-border">
                            <span className="text-[11px] font-bold uppercase tracking-[0.5px] text-text-secondary">Animation</span>
                        </div>
                        <div className="p-[10px] text-[12px]">
                            {/* Active Clip — Listbox */}
                            <div className="mb-[8px]">
                                <label className="text-[11px] text-text-secondary block mb-[4px]">Active Clip</label>
                                <Listbox value={modelInfo?.clips?.[0] ?? ''} onChange={() => { }}>
                                    <div className="relative">
                                        <ListboxButton className="w-full h-[24px] px-[6px] bg-input border border-border rounded-[2px] text-[12px] text-text text-left flex items-center justify-between disabled:opacity-40" disabled={!modelInfo?.clips?.length}>
                                            <span className="truncate">{modelInfo?.clips?.[0] ?? '— none —'}</span>
                                            <ChevronDown size={10} className="text-text-secondary shrink-0 ml-[4px]" />
                                        </ListboxButton>
                                        <ListboxOptions className="absolute z-10 mt-[2px] w-full max-h-[120px] overflow-auto bg-bg border border-border rounded-[2px] py-[2px] text-[12px] shadow-lg">
                                            {(modelInfo?.clips ?? []).map((c) => (
                                                <ListboxOption key={c} value={c} className="cursor-pointer px-[6px] h-[24px] flex items-center text-text hover:bg-hover data-[selected]:bg-active">
                                                    {c}
                                                </ListboxOption>
                                            ))}
                                        </ListboxOptions>
                                    </div>
                                </Listbox>
                            </div>

                            {/* Speed */}
                            <div className="mb-[8px]">
                                <div className="flex items-center justify-between mb-[4px]">
                                    <label className="text-[11px] text-text-secondary">Speed</label>
                                    <span className="text-[11px] text-text">1.0x</span>
                                </div>
                                <input type="range" min={0.1} max={3.0} step={0.1} defaultValue={1.0} disabled={!selectedClip} className="w-full disabled:opacity-40" />
                            </div>

                            {/* Loop — Listbox */}
                            <div className="mb-[8px]">
                                <label className="text-[11px] text-text-secondary block mb-[4px]">Loop</label>
                                <Listbox value="loop" onChange={() => { }}>
                                    <div className="relative">
                                        <ListboxButton className="w-full h-[24px] px-[6px] bg-input border border-border rounded-[2px] text-[12px] text-text text-left flex items-center justify-between disabled:opacity-40" disabled={!selectedClip}>
                                            <span>Loop</span>
                                            <ChevronDown size={10} className="text-text-secondary shrink-0 ml-[4px]" />
                                        </ListboxButton>
                                        <ListboxOptions className="absolute z-10 mt-[2px] w-full bg-bg border border-border rounded-[2px] py-[2px] text-[12px] shadow-lg">
                                            {loopOptions.map((opt) => (
                                                <ListboxOption key={opt.value} value={opt.value} className="cursor-pointer px-[6px] h-[24px] flex items-center text-text hover:bg-hover data-[selected]:bg-active">
                                                    {opt.label}
                                                </ListboxOption>
                                            ))}
                                        </ListboxOptions>
                                    </div>
                                </Listbox>
                            </div>

                            <div className="flex justify-between text-[11px]">
                                <span className="text-text-secondary">Clips</span>
                                <span className="text-text">{modelInfo?.clips?.length ?? 0}</span>
                            </div>
                        </div>
                    </div>

                    {/* Render Settings */}
                    <div>
                        <div className="h-[24px] flex items-center px-[10px] bg-bg border-b border-border">
                            <span className="text-[11px] font-bold uppercase tracking-[0.5px] text-text-secondary">Render</span>
                        </div>
                        <div className="p-[10px] text-[12px] flex flex-col gap-[6px]">
                            <RenderToggle label="Wireframe" enabled={renderSettings.showWireframe} onChange={onSetShowWireframe} />
                            <RenderToggle label="Skeleton" enabled={renderSettings.showSkeleton} onChange={onSetShowSkeleton} />
                            <RenderToggle label="Grid" enabled={renderSettings.showGrid} onChange={onSetShowGrid} />
                            <RenderToggle label="Textures" enabled={renderSettings.showTextures} onChange={onSetShowTextures} />

                            <div className="mt-[4px] pt-[6px] border-t border-border">
                                <div className="flex items-center justify-between mb-[4px]">
                                    <label className="text-[11px] text-text-secondary">Lighting</label>
                                    <span className="text-[11px] text-text">{lightIntensity.toFixed(1)}</span>
                                </div>
                                <input type="range" min={0.2} max={3.0} step={0.1} value={lightIntensity} onChange={(e) => onSetLightIntensity(parseFloat(e.target.value))} className="w-full" />
                            </div>

                            <div className="mt-[6px] pt-[6px] border-t border-border">
                                <div className="text-[11px] font-bold uppercase tracking-[0.5px] text-text-secondary mb-[6px]">Controls</div>
                                <div className="flex flex-col gap-[5px]">
                                    <div className="flex items-center gap-[6px]">
                                        <RefreshCw size={12} className="text-text-secondary shrink-0" />
                                        <span className="text-[11px] text-text-secondary">Left Click + Drag</span>
                                        <span className="text-[11px] text-text ml-auto">Orbit</span>
                                    </div>
                                    <div className="flex items-center gap-[6px]">
                                        <Move size={12} className="text-text-secondary shrink-0" />
                                        <span className="text-[11px] text-text-secondary">Right Click + Drag</span>
                                        <span className="text-[11px] text-text ml-auto">Pan</span>
                                    </div>
                                    <div className="flex items-center gap-[6px]">
                                        <ZoomIn size={12} className="text-text-secondary shrink-0" />
                                        <span className="text-[11px] text-text-secondary">Scroll Wheel</span>
                                        <span className="text-[11px] text-text ml-auto">Zoom</span>
                                    </div>
                                    <div className="flex items-center gap-[6px]">
                                        <Mouse size={12} className="text-text-secondary shrink-0" />
                                        <span className="text-[11px] text-text-secondary">Middle Click + Drag</span>
                                        <span className="text-[11px] text-text ml-auto">Pan</span>
                                    </div>
                                </div>
                            </div>
                        </div>
                    </div>
                </>
            )}
        </div>
    );
}
