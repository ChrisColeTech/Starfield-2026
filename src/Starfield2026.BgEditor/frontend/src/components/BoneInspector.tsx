import { useEditorStore } from '../store/editorStore'
import { detectBoneCollections, type BoneData } from '../data/skeletons'

/**
 * BoneInspector — always shows transform mode controls.
 * When a bone is selected, also shows bone properties (name, parent, XYZ).
 */
export default function BoneInspector() {
    const selectedBone = useEditorStore(s => s.selectedBone)
    const skeleton = useEditorStore(s => s.skeleton)
    const transformMode = useEditorStore(s => s.transformMode)
    const setTransformMode = useEditorStore(s => s.setTransformMode)

    const bone = selectedBone && skeleton ? skeleton.find(b => b.name === selectedBone) : null
    const collections = skeleton ? detectBoneCollections(skeleton) : []
    const collection = bone ? collections.find(c => c.bones.includes(selectedBone!)) : null
    const hasBone = !!bone

    return (
        <div className="border-t border-border shrink-0">
            {/* Header */}
            <div className="h-6 flex items-center px-2.5 bg-background border-b border-border">
                <span className="text-[10px] font-semibold text-muted-foreground tracking-wider uppercase">
                    Transform
                </span>
            </div>

            <div className="p-2.5 text-[11px] flex flex-col gap-2">
                {/* Transform mode selector — always visible */}
                <div className="flex gap-1">
                    {(['translate', 'rotate', 'scale'] as const).map(mode => (
                        <button
                            key={mode}
                            disabled={!hasBone}
                            className={`flex-1 px-1 py-0.5 text-[10px] border rounded cursor-pointer disabled:opacity-40 disabled:cursor-not-allowed ${transformMode === mode
                                ? 'bg-primary/20 border-primary text-primary'
                                : 'bg-input border-border text-muted-foreground hover:text-foreground hover:bg-muted'
                                }`}
                            onClick={() => setTransformMode(mode)}
                        >
                            {mode === 'translate' ? 'G Move' : mode === 'rotate' ? 'R Rotate' : 'S Scale'}
                        </button>
                    ))}
                </div>

                {/* Bone details — only when bone selected */}
                {bone && (
                    <>
                        {/* Bone name + collection */}
                        <div className="flex items-center gap-1.5">
                            {collection && (
                                <span
                                    className="w-2 h-2 rounded-sm shrink-0"
                                    style={{ backgroundColor: collection.color }}
                                />
                            )}
                            <span className="text-foreground font-medium truncate">{bone.name}</span>
                        </div>

                        {bone.parent && (
                            <div className="text-muted-foreground/70 text-[10px]">
                                Parent: <span className="text-muted-foreground">{bone.parent}</span>
                            </div>
                        )}

                        {/* Position readout */}
                        <div className="space-y-1">
                            <div className="text-[10px] text-muted-foreground/50 uppercase tracking-wider">Head Position</div>
                            <div className="grid grid-cols-3 gap-1">
                                {(['X', 'Y', 'Z'] as const).map((axis, i) => (
                                    <div key={axis} className="flex items-center gap-0.5">
                                        <span className={`text-[10px] font-bold ${axis === 'X' ? 'text-red-400' : axis === 'Y' ? 'text-green-400' : 'text-blue-400'
                                            }`}>{axis}</span>
                                        <span className="text-[10px] text-muted-foreground bg-input px-1 py-0.5 rounded border border-border flex-1 text-right tabular-nums">
                                            {bone.head[i].toFixed(4)}
                                        </span>
                                    </div>
                                ))}
                            </div>
                        </div>

                        {/* Tail readout */}
                        <div className="space-y-1">
                            <div className="text-[10px] text-muted-foreground/50 uppercase tracking-wider">Tail Position</div>
                            <div className="grid grid-cols-3 gap-1">
                                {(['X', 'Y', 'Z'] as const).map((axis, i) => (
                                    <div key={axis} className="flex items-center gap-0.5">
                                        <span className={`text-[10px] font-bold ${axis === 'X' ? 'text-red-400' : axis === 'Y' ? 'text-green-400' : 'text-blue-400'
                                            }`}>{axis}</span>
                                        <span className="text-[10px] text-muted-foreground bg-input px-1 py-0.5 rounded border border-border flex-1 text-right tabular-nums">
                                            {bone.tail[i].toFixed(4)}
                                        </span>
                                    </div>
                                ))}
                            </div>
                        </div>

                        {collection && (
                            <div className="text-muted-foreground/70 text-[10px]">
                                Collection: <span className="text-muted-foreground">{collection.name}</span>
                            </div>
                        )}
                    </>
                )}

                {!bone && (
                    <div className="text-[10px] text-muted-foreground/40 italic">
                        Click a bone to inspect
                    </div>
                )}
            </div>
        </div>
    )
}
