import { useState } from 'react'
import { ChevronDown, ChevronRight, Bone, RotateCcw, Trash2, Eye, EyeOff } from 'lucide-react'
import { useEditorStore } from '../store/editorStore'
import BoneInspector from './BoneInspector'
import {
    RIG_TEMPLATE_LABELS,
    GAME_LABELS,
    detectBoneCollections,
    type RigTemplate,
    type GameType,
} from '../data/skeletons'

export default function AutoRigPanel() {
    const skeleton = useEditorStore(s => s.skeleton)
    const rigTemplate = useEditorStore(s => s.rigTemplate)
    const gameType = useEditorStore(s => s.gameType)
    const generateRig = useEditorStore(s => s.generateRig)
    const setRigTemplate = useEditorStore(s => s.setRigTemplate)
    const setGameType = useEditorStore(s => s.setGameType)
    const clearRig = useEditorStore(s => s.clearRig)
    const showGrid = useEditorStore(s => s.showGrid)
    const showAxes = useEditorStore(s => s.showAxes)
    const setShowGrid = useEditorStore(s => s.setShowGrid)
    const setShowAxes = useEditorStore(s => s.setShowAxes)

    // Collapsible sections — same pattern as AnimationsPage
    const [sectionsOpen, setSectionsOpen] = useState({
        rig: true,
        game: true,
        bones: true,
    })
    const toggleSection = (key: keyof typeof sectionsOpen) =>
        setSectionsOpen(prev => ({ ...prev, [key]: !prev[key] }))

    const boneCount = skeleton?.length ?? 0
    const templateOptions = Object.entries(RIG_TEMPLATE_LABELS) as [RigTemplate, string][]
    const gameOptions = Object.entries(GAME_LABELS) as [GameType, string][]
    const collections = skeleton ? detectBoneCollections(skeleton) : []

    return (
        <div className="flex flex-col h-full">
            {/* ── Rig Template ── */}
            <div className="border-b border-border shrink-0">
                <SectionHeader label="Rig Template" open={sectionsOpen.rig} onToggle={() => toggleSection('rig')} />
                {sectionsOpen.rig && (
                    <div className="p-2.5 text-xs flex flex-col gap-1.5">
                        <select
                            value={rigTemplate}
                            onChange={e => setRigTemplate(e.target.value as RigTemplate)}
                            className="w-full px-1.5 py-1 text-[11px] bg-input border border-border rounded text-foreground outline-none text-center"
                        >
                            {templateOptions.map(([value, label]) => (
                                <option key={value} value={value}>{label}</option>
                            ))}
                        </select>
                        <PanelButton icon={<Bone size={12} strokeWidth={2} />} label="New Rig" onClick={() => generateRig()} />
                        <PanelButton icon={<RotateCcw size={12} strokeWidth={2} />} label="Reset View" onClick={() => window.dispatchEvent(new CustomEvent('viewport:resetView'))} />
                        {/* Camera presets */}
                        <div className="flex gap-1">
                            {([
                                ['F', 0, 0],
                                ['S', 90, 0],
                                ['T', 0, 89],
                                ['B', 180, 0],
                                ['¾', 35, 25],
                            ] as [string, number, number][]).map(([label, az, el]) => (
                                <button
                                    key={label}
                                    className="flex-1 px-1 py-1 text-[10px] bg-input border border-border rounded text-muted-foreground hover:text-foreground hover:bg-muted cursor-pointer"
                                    onClick={() => useEditorStore.getState().updateViewport({ azimuth: az, elevation: el })}
                                    title={label === 'F' ? 'Front' : label === 'S' ? 'Side' : label === 'T' ? 'Top' : label === 'B' ? 'Back' : '¾ View'}
                                >
                                    {label}
                                </button>
                            ))}
                        </div>
                        {/* Grid/Axes toggles */}
                        <div className="flex gap-2 text-[10px]">
                            <label className="flex items-center gap-1 cursor-pointer text-muted-foreground hover:text-foreground">
                                <input type="checkbox" checked={showGrid} onChange={e => setShowGrid(e.target.checked)} className="w-3 h-3 accent-primary" />
                                Grid
                            </label>
                            <label className="flex items-center gap-1 cursor-pointer text-muted-foreground hover:text-foreground">
                                <input type="checkbox" checked={showAxes} onChange={e => setShowAxes(e.target.checked)} className="w-3 h-3 accent-primary" />
                                Axes
                            </label>
                        </div>
                    </div>
                )}
            </div>

            {/* ── Game ── */}
            <div className="border-b border-border shrink-0">
                <SectionHeader label="Game" open={sectionsOpen.game} onToggle={() => toggleSection('game')} />
                {sectionsOpen.game && (
                    <div className="p-2.5 text-xs flex flex-col gap-1.5">
                        <select
                            value={gameType}
                            onChange={e => setGameType(e.target.value as GameType)}
                            className="w-full px-1.5 py-1 text-[11px] bg-input border border-border rounded text-foreground outline-none text-center"
                        >
                            {gameOptions.map(([value, label]) => (
                                <option key={value} value={value}>{label}</option>
                            ))}
                        </select>
                        <PanelButton icon={<Bone size={12} strokeWidth={2} />} label="Generate Game Rig" onClick={() => generateRig()} />
                        <button
                            onClick={clearRig}
                            disabled={!skeleton}
                            className="w-full px-2.5 py-1.5 bg-destructive/10 border border-destructive/30 rounded text-destructive text-xs cursor-pointer flex items-center justify-center gap-1.5 hover:bg-destructive/20 disabled:opacity-40 disabled:cursor-not-allowed"
                        >
                            <Trash2 size={12} strokeWidth={2} /> Clear Rig
                        </button>
                    </div>
                )}
            </div>

            {/* ── Bones (scrollable) ── */}
            <div className={`flex flex-col min-h-0 ${sectionsOpen.bones ? 'flex-1 overflow-hidden' : 'shrink-0'}`}>
                <SectionHeader
                    label={`Bones (${boneCount})`}
                    open={sectionsOpen.bones}
                    onToggle={() => toggleSection('bones')}
                />
                {sectionsOpen.bones && (
                    <div className="flex-1 overflow-y-auto">
                        <BoneCollectionsList collections={collections} />
                    </div>
                )}
            </div>

            {/* ── Bone Inspector (selected bone) ── */}
            <BoneInspector />
        </div>
    )
}

// ── Section header — identical to AnimationsPage ──
function SectionHeader({ label, open, onToggle }: { label: string; open: boolean; onToggle: () => void }) {
    return (
        <button
            onClick={onToggle}
            className="h-6 w-full flex items-center px-2.5 bg-background border-b border-border shrink-0 cursor-pointer border-x-0 border-t-0"
        >
            {open
                ? <ChevronDown size={10} className="text-muted-foreground mr-1" />
                : <ChevronRight size={10} className="text-muted-foreground mr-1" />
            }
            <span className="text-[11px] font-bold uppercase tracking-wider text-muted-foreground">
                {label}
            </span>
        </button>
    )
}

// ── Standard panel button — centered label ──
function PanelButton({ icon, label, onClick, disabled }: {
    icon: React.ReactNode; label: string; onClick?: () => void; disabled?: boolean
}) {
    return (
        <button
            onClick={onClick}
            disabled={disabled}
            className="w-full px-2.5 py-1.5 bg-input border border-border rounded text-foreground text-xs cursor-pointer flex items-center justify-center gap-1.5 hover:bg-muted disabled:opacity-40 disabled:cursor-not-allowed"
        >
            {icon} {label}
        </button>
    )
}

// ── Bone collections list ──
function BoneCollectionsList({
    collections,
}: {
    collections: { name: string; color: string; bones: string[] }[]
}) {
    const [expanded, setExpanded] = useState<Set<string>>(new Set())
    const selectedBone = useEditorStore(s => s.selectedBone)
    const selectBone = useEditorStore(s => s.selectBone)
    const hiddenCollections = useEditorStore(s => s.hiddenCollections)
    const toggleCollectionVisibility = useEditorStore(s => s.toggleCollectionVisibility)

    const toggle = (name: string) => {
        setExpanded(prev => {
            const next = new Set(prev)
            if (next.has(name)) next.delete(name)
            else next.add(name)
            return next
        })
    }

    return (
        <div className="px-2.5 py-1.5">
            {collections.map(col => {
                const isOpen = expanded.has(col.name)
                const isHidden = hiddenCollections.has(col.name)
                return (
                    <div key={col.name}>
                        <div className="flex items-center">
                            <button
                                className={`flex-1 flex items-center gap-1.5 px-1 py-0.5 text-xs bg-transparent border-none cursor-pointer rounded hover:bg-muted ${isHidden ? 'text-muted-foreground/30' : 'text-muted-foreground hover:text-foreground'}`}
                                onClick={() => toggle(col.name)}
                            >
                                <span
                                    className="w-2 h-2 rounded-sm shrink-0"
                                    style={{ backgroundColor: col.color, opacity: isHidden ? 0.3 : 1 }}
                                />
                                {isOpen
                                    ? <ChevronDown size={10} className="text-muted-foreground" />
                                    : <ChevronRight size={10} className="text-muted-foreground" />
                                }
                                <span className="flex-1 text-left text-[11px]">{col.name}</span>
                                <span className="text-muted-foreground/50 text-[10px]">{col.bones.length}</span>
                            </button>
                            <button
                                className="px-1 py-0.5 bg-transparent border-none cursor-pointer text-muted-foreground/40 hover:text-foreground"
                                onClick={(e) => { e.stopPropagation(); toggleCollectionVisibility(col.name) }}
                                title={isHidden ? 'Show collection' : 'Hide collection'}
                            >
                                {isHidden ? <EyeOff size={10} /> : <Eye size={10} />}
                            </button>
                        </div>
                        {isOpen && (
                            <div className="ml-6 text-[10px]">
                                {col.bones.map(bname => (
                                    <div
                                        key={bname}
                                        className={`py-px cursor-pointer rounded px-1 ${selectedBone === bname
                                            ? 'text-foreground bg-primary/20 border-l-2 border-primary'
                                            : 'text-muted-foreground/50 hover:text-foreground hover:bg-muted'
                                            }`}
                                        onClick={() => selectBone(bname)}
                                    >
                                        {bname}
                                    </div>
                                ))}
                            </div>
                        )}
                    </div>
                )
            })}
        </div>
    )
}
