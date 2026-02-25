import { useState } from 'react'
import { ChevronDown, ChevronRight, Bone, Play, Square, RotateCcw, Import, Trash2 } from 'lucide-react'
import { useEditorStore } from '../store/editorStore'
import { BONE_COLLECTIONS, GAME_LABELS, type GameType } from '../data/skeletons'

export default function AutoRigPanel() {
    const skeleton = useEditorStore(s => s.skeleton)
    const gameType = useEditorStore(s => s.gameType)
    const generateRig = useEditorStore(s => s.generateRig)
    const setGameType = useEditorStore(s => s.setGameType)
    const clearRig = useEditorStore(s => s.clearRig)

    const [collectionsOpen, setCollectionsOpen] = useState(true)
    const [visibleGroups, setVisibleGroups] = useState<Set<string>>(
        new Set(BONE_COLLECTIONS.map(c => c.name))
    )

    const boneCount = skeleton?.length ?? 0
    const gameOptions = Object.entries(GAME_LABELS) as [GameType, string][]

    const toggleGroup = (name: string) => {
        setVisibleGroups(prev => {
            const next = new Set(prev)
            if (next.has(name)) next.delete(name)
            else next.add(name)
            return next
        })
    }

    return (
        <div className="flex flex-col h-full text-[13px] select-none">
            {/* ── Auto Rig ── */}
            <Section title="AUTO RIG">
                <div className="flex flex-col gap-1.5">
                    <ActionButton
                        icon={<Bone size={14} />}
                        label="New Rig"
                        onClick={() => generateRig()}
                    />
                    <ActionButton
                        icon={<RotateCcw size={14} />}
                        label="Reset View"
                        onClick={() => {
                            // Dispatch a custom event the viewport listens for
                            window.dispatchEvent(new CustomEvent('viewport:resetView'))
                        }}
                    />
                </div>
            </Section>

            {/* ── Model ── */}
            <Section title="MODEL">
                <div className="flex flex-col gap-1.5">
                    <ActionButton
                        icon={<Import size={14} />}
                        label="Load Model"
                        disabled={true}
                        onClick={() => { }}
                    />
                    <ActionButton
                        icon={<Play size={14} />}
                        label="Load Animation"
                        disabled={true}
                        onClick={() => { }}
                    />
                    <ActionButton
                        icon={<Square size={14} />}
                        label="Unload Animation"
                        disabled={true}
                        onClick={() => { }}
                    />
                </div>
            </Section>

            {/* ── Rigging ── */}
            <Section title="RIGGING">
                <ActionButton
                    icon={<Bone size={14} />}
                    label="Fit Rig to Model"
                    disabled={true}
                    onClick={() => { }}
                />
            </Section>

            {/* ── Game ── */}
            <Section title="GAME">
                <div className="flex flex-col gap-1.5">
                    <select
                        value={gameType}
                        onChange={e => setGameType(e.target.value as GameType)}
                        className="w-full h-7 px-2 bg-muted border border-border rounded text-foreground text-[13px] focus:outline-none focus:border-primary"
                    >
                        {gameOptions.map(([value, label]) => (
                            <option key={value} value={value}>{label}</option>
                        ))}
                    </select>
                    <ActionButton
                        icon={<Bone size={14} />}
                        label="Generate Game Rig"
                        onClick={() => generateRig()}
                    />
                    {skeleton && (
                        <ActionButton
                            icon={<Trash2 size={14} />}
                            label="Clear Rig"
                            danger
                            onClick={clearRig}
                        />
                    )}
                </div>
            </Section>

            {/* ── Bone Collections ── */}
            {skeleton && (
                <Section
                    title={`BONES (${boneCount})`}
                    collapsible
                    open={collectionsOpen}
                    onToggle={() => setCollectionsOpen(!collectionsOpen)}
                >
                    <div className="flex flex-col gap-0.5">
                        {BONE_COLLECTIONS.map(col => {
                            const groupBones = skeleton.filter(b => col.bones.includes(b.name))
                            if (groupBones.length === 0) return null
                            const visible = visibleGroups.has(col.name)
                            return (
                                <div key={col.name}>
                                    <button
                                        className="w-full flex items-center gap-1.5 px-1 py-0.5 text-[12px] bg-transparent border-none cursor-pointer hover:bg-muted text-foreground rounded"
                                        onClick={() => toggleGroup(col.name)}
                                    >
                                        <span
                                            className="w-2.5 h-2.5 rounded-sm shrink-0"
                                            style={{ backgroundColor: col.color }}
                                        />
                                        <span className="flex-1 text-left">{col.name}</span>
                                        <span className="text-muted-foreground text-[11px]">
                                            {groupBones.length}
                                        </span>
                                    </button>
                                    {visible && (
                                        <div className="ml-5 text-[11px] text-muted-foreground">
                                            {groupBones.map(b => (
                                                <div key={b.name} className="py-px hover:text-foreground cursor-default">
                                                    {b.name}
                                                </div>
                                            ))}
                                        </div>
                                    )}
                                </div>
                            )
                        })}
                    </div>
                </Section>
            )}
        </div>
    )
}

// ── Reusable sub-components ──

function Section({
    title,
    children,
    collapsible,
    open,
    onToggle,
}: {
    title: string
    children: React.ReactNode
    collapsible?: boolean
    open?: boolean
    onToggle?: () => void
}) {
    return (
        <div className="border-b border-border">
            <button
                className="w-full flex items-center gap-1 px-2.5 py-1.5 text-[11px] font-bold uppercase tracking-wider text-muted-foreground bg-transparent border-none cursor-pointer hover:text-foreground"
                onClick={onToggle}
            >
                {collapsible && (
                    open ? <ChevronDown size={12} /> : <ChevronRight size={12} />
                )}
                {title}
            </button>
            {(!collapsible || open) && (
                <div className="px-2.5 pb-2.5">{children}</div>
            )}
        </div>
    )
}

function ActionButton({
    icon,
    label,
    onClick,
    disabled,
    danger,
}: {
    icon: React.ReactNode
    label: string
    onClick: () => void
    disabled?: boolean
    danger?: boolean
}) {
    return (
        <button
            className={`w-full h-8 flex items-center gap-2 px-3 rounded border border-border text-[13px] cursor-pointer transition-colors
        ${danger
                    ? 'bg-destructive/10 text-destructive hover:bg-destructive/20 border-destructive/30'
                    : 'bg-muted text-foreground hover:bg-muted/80'
                }
        ${disabled ? 'opacity-40 cursor-not-allowed' : ''}`}
            onClick={onClick}
            disabled={disabled}
        >
            {icon}
            {label}
        </button>
    )
}
