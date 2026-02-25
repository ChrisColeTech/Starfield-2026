import { useState } from 'react'
import { ChevronDown, ChevronRight, Bone, Play, Square, RotateCcw, Import, Trash2 } from 'lucide-react'
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

    const [collectionsOpen, setCollectionsOpen] = useState(true)

    const boneCount = skeleton?.length ?? 0
    const templateOptions = Object.entries(RIG_TEMPLATE_LABELS) as [RigTemplate, string][]
    const gameOptions = Object.entries(GAME_LABELS) as [GameType, string][]

    // Auto-detect bone collections from skeleton names
    const collections = skeleton ? detectBoneCollections(skeleton) : []

    return (
        <div className="flex flex-col h-full text-[13px] select-none">
            {/* ── Auto Rig ── */}
            <Section title="AUTO RIG">
                <div className="flex flex-col gap-1.5">
                    <select
                        value={rigTemplate}
                        onChange={e => setRigTemplate(e.target.value as RigTemplate)}
                        className="w-full h-7 px-2 rounded text-[13px] bg-[hsl(var(--muted))] text-[hsl(var(--foreground))] border border-[hsl(var(--border))] focus:outline-none focus:border-[hsl(var(--ring))]"
                    >
                        {templateOptions.map(([value, label]) => (
                            <option key={value} value={value}>{label}</option>
                        ))}
                    </select>
                    <ActionButton
                        icon={<Bone size={14} />}
                        label="New Rig"
                        onClick={() => generateRig()}
                    />
                    <ActionButton
                        icon={<RotateCcw size={14} />}
                        label="Reset View"
                        onClick={() => {
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
                        disabled
                        onClick={() => { }}
                    />
                    <ActionButton
                        icon={<Play size={14} />}
                        label="Load Animation"
                        disabled
                        onClick={() => { }}
                    />
                    <ActionButton
                        icon={<Square size={14} />}
                        label="Unload Animation"
                        disabled
                        onClick={() => { }}
                    />
                </div>
            </Section>

            {/* ── Rigging ── */}
            <Section title="RIGGING">
                <ActionButton
                    icon={<Bone size={14} />}
                    label="Fit Rig to Model"
                    disabled
                    onClick={() => { }}
                />
            </Section>

            {/* ── Game ── */}
            <Section title="GAME">
                <div className="flex flex-col gap-1.5">
                    <select
                        value={gameType}
                        onChange={e => setGameType(e.target.value as GameType)}
                        className="w-full h-7 px-2 rounded text-[13px] bg-[hsl(var(--muted))] text-[hsl(var(--foreground))] border border-[hsl(var(--border))] focus:outline-none focus:border-[hsl(var(--ring))]"
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
                    <BoneCollectionsList collections={collections} skeleton={skeleton} />
                </Section>
            )}
        </div>
    )
}

// ── Sub-components (theme-compliant, no hardcoded colors in UI) ──

function BoneCollectionsList({
    collections,
    skeleton,
}: {
    collections: { name: string; color: string; bones: string[] }[]
    skeleton: { name: string }[]
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
        <div className="flex flex-col gap-0.5">
            {collections.map(col => {
                const isOpen = expanded.has(col.name)
                return (
                    <div key={col.name}>
                        <button
                            className="w-full flex items-center gap-1.5 px-1 py-0.5 text-[12px] bg-transparent border-none cursor-pointer rounded text-[hsl(var(--foreground))] hover:bg-[hsl(var(--muted))]"
                            onClick={() => toggle(col.name)}
                        >
                            <span
                                className="w-2.5 h-2.5 rounded-sm shrink-0"
                                style={{ backgroundColor: col.color }}
                            />
                            {isOpen ? <ChevronDown size={10} /> : <ChevronRight size={10} />}
                            <span className="flex-1 text-left">{col.name}</span>
                            <span className="text-[hsl(var(--muted-foreground))] text-[11px]">
                                {col.bones.length}
                            </span>
                        </button>
                        {isOpen && (
                            <div className="ml-6 text-[11px] text-[hsl(var(--muted-foreground))]">
                                {col.bones.map(bname => (
                                    <div key={bname} className="py-px hover:text-[hsl(var(--foreground))] cursor-default">
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
        <div className="border-b border-[hsl(var(--border))]">
            <button
                className="w-full flex items-center gap-1 px-2.5 py-1.5 text-[11px] font-bold uppercase tracking-wider bg-transparent border-none cursor-pointer text-[hsl(var(--muted-foreground))] hover:text-[hsl(var(--foreground))]"
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
    const base = 'w-full h-8 flex items-center gap-2 px-3 rounded border text-[13px] cursor-pointer transition-colors'
    const normal = 'bg-[hsl(var(--muted))] text-[hsl(var(--foreground))] border-[hsl(var(--border))] hover:bg-[hsl(var(--secondary))]'
    const dangerCls = 'bg-[hsl(var(--destructive)/0.1)] text-[hsl(var(--destructive))] border-[hsl(var(--destructive)/0.3)] hover:bg-[hsl(var(--destructive)/0.2)]'
    const disabledCls = disabled ? 'opacity-40 cursor-not-allowed' : ''

    return (
        <button
            className={`${base} ${danger ? dangerCls : normal} ${disabledCls}`}
            onClick={onClick}
            disabled={disabled}
        >
            {icon}
            {label}
        </button>
    )
}
