import { useState } from 'react'
import { ChevronDown, ChevronRight, Bone, RotateCcw, Trash2 } from 'lucide-react'
import { useEditorStore } from '../store/editorStore'
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
            <div className="flex-1 flex flex-col min-h-0 overflow-hidden">
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
                return (
                    <div key={col.name}>
                        <button
                            className="w-full flex items-center gap-1.5 px-1 py-0.5 text-xs bg-transparent border-none cursor-pointer rounded text-muted-foreground hover:bg-muted hover:text-foreground"
                            onClick={() => toggle(col.name)}
                        >
                            <span
                                className="w-2 h-2 rounded-sm shrink-0"
                                style={{ backgroundColor: col.color }}
                            />
                            {isOpen
                                ? <ChevronDown size={10} className="text-muted-foreground" />
                                : <ChevronRight size={10} className="text-muted-foreground" />
                            }
                            <span className="flex-1 text-left text-[11px]">{col.name}</span>
                            <span className="text-muted-foreground/50 text-[10px]">{col.bones.length}</span>
                        </button>
                        {isOpen && (
                            <div className="ml-6 text-[10px] text-muted-foreground/50">
                                {col.bones.map(bname => (
                                    <div key={bname} className="py-px hover:text-foreground cursor-default">
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
