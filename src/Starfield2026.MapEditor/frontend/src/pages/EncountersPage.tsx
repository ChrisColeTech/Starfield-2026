import { useState, useMemo, useRef, useEffect, useCallback } from 'react'
import { Plus, Trash2, Upload } from 'lucide-react'
import { useEditorStore } from '../store/editorStore'
import {
  ENCOUNTER_TYPES,
  PROGRESS_MULTIPLIERS,
  type SpeciesInfo,
  type EncounterEntry,
} from '../types/encounters'

// --- Species Search Dropdown ---

function SpeciesSearch({
  value,
  species,
  onChange,
}: {
  value: string
  species: SpeciesInfo[]
  onChange: (speciesId: string) => void
}) {
  const [query, setQuery] = useState(value)
  const [open, setOpen] = useState(false)
  const ref = useRef<HTMLDivElement>(null)

  useEffect(() => { setQuery(value) }, [value])

  useEffect(() => {
    const handler = (e: MouseEvent) => {
      if (ref.current && !ref.current.contains(e.target as Node)) setOpen(false)
    }
    document.addEventListener('mousedown', handler)
    return () => document.removeEventListener('mousedown', handler)
  }, [])

  const filtered = useMemo(() => {
    if (!query) return species.slice(0, 50)
    const q = query.toLowerCase()
    return species.filter(s => s.name.toLowerCase().includes(q)).slice(0, 50)
  }, [query, species])

  return (
    <div ref={ref} className="relative flex-1 min-w-[120px]">
      <input
        type="text"
        value={query}
        onChange={e => { setQuery(e.target.value); setOpen(true) }}
        onFocus={() => setOpen(true)}
        placeholder="Search species..."
        className="w-full px-[6px] py-[3px] bg-input border border-border rounded-[3px] text-text text-[12px] outline-none"
      />
      {open && filtered.length > 0 && (
        <div className="absolute top-full left-0 right-0 max-h-[200px] overflow-y-auto bg-bg border border-border rounded-[3px] z-[100]">
          {filtered.map(s => (
            <button
              key={s.id}
              onClick={() => { onChange(s.name); setQuery(s.name); setOpen(false) }}
              className="flex items-center gap-[6px] w-full px-[8px] py-[4px] border-none text-text text-[12px] cursor-pointer text-left hover:bg-hover"
              style={{
                background: s.name === value ? 'var(--color-active)' : 'transparent',
              }}
            >
              <span className="text-text-secondary text-[10px] min-w-[28px]">#{s.id}</span>
              <span>{s.name}</span>
              <span className="text-text-secondary text-[10px] ml-auto">
                {s.type1}{s.type2 ? ` / ${s.type2}` : ''}
              </span>
            </button>
          ))}
        </div>
      )}
    </div>
  )
}

// --- Weight Bar ---

function WeightBar({ entries }: { entries: EncounterEntry[] }) {
  const totalWeight = entries.reduce((sum, e) => sum + e.weight, 0)
  if (totalWeight === 0) return null

  const colors = [
    '#4a9eff', '#ff6b6b', '#51cf66', '#ffd43b', '#cc5de8',
    '#ff922b', '#20c997', '#e599f7', '#74c0fc', '#f06595',
  ]

  return (
    <div className="flex flex-col gap-[3px] py-[4px]">
      <div className="flex h-[8px] rounded-[4px] overflow-hidden">
        {entries.map((e, i) => {
          const pct = (e.weight / totalWeight) * 100
          if (pct === 0) return null
          return (
            <div
              key={i}
              title={`${e.speciesId || '?'}: ${pct.toFixed(1)}%`}
              style={{ width: `${pct}%`, background: colors[i % colors.length], minWidth: 2 }}
            />
          )
        })}
      </div>
      <div className="flex flex-wrap gap-x-[10px] gap-y-[2px] text-[10px] text-text-secondary">
        {entries.map((e, i) => {
          const pct = (e.weight / totalWeight) * 100
          return (
            <span key={i} className="flex items-center gap-[3px]">
              <span className="inline-block w-[8px] h-[8px] rounded-[2px]" style={{ background: colors[i % colors.length] }} />
              {e.speciesId || '?'} {pct.toFixed(1)}%
            </span>
          )
        })}
      </div>
    </div>
  )
}

// --- Entry Row ---

function EntryRow({
  entry,
  groupIndex,
  entryIndex,
  species,
}: {
  entry: EncounterEntry
  groupIndex: number
  entryIndex: number
  species: SpeciesInfo[]
}) {
  const updateEntry = useEditorStore(s => s.updateEncounterEntry)
  const removeEntry = useEditorStore(s => s.removeEncounterEntry)

  const handleUpdate = useCallback((partial: Partial<EncounterEntry>) => {
    updateEntry(groupIndex, entryIndex, partial)
  }, [updateEntry, groupIndex, entryIndex])

  const inputCls = "px-[6px] py-[3px] bg-input border border-border rounded-[3px] text-text text-[12px] outline-none text-center"

  return (
    <div className="flex items-center gap-[4px]">
      <SpeciesSearch
        value={entry.speciesId}
        species={species}
        onChange={speciesId => handleUpdate({ speciesId })}
      />
      <input type="number" min={1} max={100} value={entry.minLevel} onChange={e => handleUpdate({ minLevel: +e.target.value })} title="Min Level" className={inputCls} style={{ width: 44 }} />
      <input type="number" min={1} max={100} value={entry.maxLevel} onChange={e => handleUpdate({ maxLevel: +e.target.value })} title="Max Level" className={inputCls} style={{ width: 44 }} />
      <input type="number" min={1} max={255} value={entry.weight} onChange={e => handleUpdate({ weight: +e.target.value })} title="Weight" className={inputCls} style={{ width: 44 }} />
      <input type="number" min={0} max={8} value={entry.requiredBadges ?? 0} onChange={e => handleUpdate({ requiredBadges: +e.target.value || undefined })} title="Required Badges" className={inputCls} style={{ width: 36 }} />
      <input
        type="text"
        value={(entry.requiredFlags ?? []).join(', ')}
        onChange={e => {
          const flags = e.target.value.split(',').map(f => f.trim()).filter(Boolean)
          handleUpdate({ requiredFlags: flags.length > 0 ? flags : undefined })
        }}
        placeholder="flags"
        title="Required Flags (comma-separated)"
        className="px-[6px] py-[3px] bg-input border border-border rounded-[3px] text-text text-[12px] outline-none"
        style={{ width: 100 }}
      />
      <button
        onClick={() => removeEntry(groupIndex, entryIndex)}
        title="Remove entry"
        className="p-[3px] bg-transparent border-none text-danger cursor-pointer hover:text-text"
      >
        <Trash2 size={13} />
      </button>
    </div>
  )
}

// --- Encounter Group Card ---

function EncounterGroupCard({
  groupIndex,
  species,
}: {
  groupIndex: number
  species: SpeciesInfo[]
}) {
  const group = useEditorStore(s => s.encounterData.encounterGroups[groupIndex])
  const updateType = useEditorStore(s => s.updateEncounterGroupType)
  const updateRate = useEditorStore(s => s.updateEncounterGroupRate)
  const addEntry = useEditorStore(s => s.addEncounterEntry)
  const removeGroup = useEditorStore(s => s.removeEncounterGroup)

  const ratePct = ((group.baseEncounterRate / 255) * 100).toFixed(1)

  const inputCls = "px-[6px] py-[3px] bg-input border border-border rounded-[3px] text-text text-[12px] outline-none"

  return (
    <div className="bg-bg border border-border rounded-[4px] overflow-hidden">
      {/* Group header */}
      <div className="flex items-center gap-[8px] px-[10px] py-[6px] bg-surface border-b border-border">
        <select
          value={group.encounterType}
          onChange={e => updateType(groupIndex, e.target.value)}
          className={`${inputCls} flex-1`}
        >
          {ENCOUNTER_TYPES.map(t => (
            <option key={t.id} value={t.id}>{t.label}</option>
          ))}
        </select>
        <div className="flex items-center gap-[4px] text-[11px] text-text-secondary">
          <span>Rate:</span>
          <input
            type="number"
            min={0}
            max={255}
            value={group.baseEncounterRate}
            onChange={e => updateRate(groupIndex, +e.target.value)}
            className={inputCls}
            style={{ width: 48, textAlign: 'center' }}
          />
          <span className="text-[10px]">({ratePct}%)</span>
        </div>
        <button
          onClick={() => removeGroup(groupIndex)}
          title="Delete group"
          className="p-[3px] bg-transparent border-none text-danger cursor-pointer hover:text-text"
        >
          <Trash2 size={14} />
        </button>
      </div>

      {/* Column headers */}
      <div className="flex gap-[4px] px-[10px] py-[4px] text-[10px] text-text-secondary uppercase border-b border-border">
        <span className="flex-1 min-w-[120px]">Species</span>
        <span style={{ width: 44, textAlign: 'center' }}>Min</span>
        <span style={{ width: 44, textAlign: 'center' }}>Max</span>
        <span style={{ width: 44, textAlign: 'center' }}>Wt</span>
        <span style={{ width: 36, textAlign: 'center' }}>Bdg</span>
        <span style={{ width: 100 }}>Flags</span>
        <span style={{ width: 27 }} />
      </div>

      {/* Entries */}
      <div className="p-[6px_10px] flex flex-col gap-[4px]">
        {group.entries.map((entry, i) => (
          <EntryRow
            key={i}
            entry={entry}
            groupIndex={groupIndex}
            entryIndex={i}
            species={species}
          />
        ))}

        {group.entries.length > 0 && <WeightBar entries={group.entries} />}

        <button
          onClick={() => addEntry(groupIndex)}
          className="flex items-center justify-center gap-[4px] self-start px-[8px] py-[4px] bg-input border border-border rounded-[3px] text-text text-[11px] cursor-pointer hover:bg-hover mt-[2px]"
        >
          <Plus size={12} /> Add Entry
        </button>
      </div>
    </div>
  )
}

// --- Main Page ---

export default function EncountersPage() {
  const mapName = useEditorStore(s => s.mapName)
  const encounterData = useEditorStore(s => s.encounterData)
  const species = useEditorStore(s => s.species)
  const speciesLoaded = useEditorStore(s => s.speciesLoaded)
  const loadSpecies = useEditorStore(s => s.loadSpecies)
  const setProgressMultiplier = useEditorStore(s => s.setProgressMultiplier)
  const addEncounterGroup = useEditorStore(s => s.addEncounterGroup)

  const mapId = mapName.toLowerCase().replace(/\s+/g, '_').replace(/[^a-z0-9_]/g, '')

  return (
    <div className="flex flex-col h-full overflow-hidden">
      {/* Top bar */}
      <div className="flex items-center gap-[12px] px-[16px] py-[8px] bg-surface border-b border-border shrink-0">
        <span className="text-[12px] text-text-secondary">
          Map: <span className="text-text font-semibold">{mapId || 'untitled'}</span>
        </span>
        <div className="flex items-center gap-[4px]">
          <span className="text-[11px] text-text-secondary">Scaling:</span>
          <select
            value={encounterData.progressMultiplier}
            onChange={e => setProgressMultiplier(+e.target.value)}
            className="px-[6px] py-[3px] bg-input border border-border rounded-[3px] text-text text-[12px] outline-none"
          >
            {PROGRESS_MULTIPLIERS.map(p => (
              <option key={p.value} value={p.value}>{p.label}</option>
            ))}
          </select>
        </div>
        <div className="flex-1" />
        {!speciesLoaded && (
          <button
            onClick={loadSpecies}
            className="flex items-center gap-[4px] px-[8px] py-[4px] rounded-[3px] text-text text-[11px] cursor-pointer border-none"
            style={{ background: 'var(--color-active)' }}
          >
            <Upload size={12} /> Load Species Data
          </button>
        )}
        {speciesLoaded && (
          <span className="text-[10px] text-text-secondary">{species.length} species loaded</span>
        )}
        <button
          onClick={() => addEncounterGroup()}
          className="flex items-center gap-[4px] px-[8px] py-[4px] rounded-[3px] text-text text-[11px] cursor-pointer border-none"
          style={{ background: 'var(--color-active)' }}
        >
          <Plus size={12} /> Add Group
        </button>
      </div>

      {/* Content */}
      <div className="flex-1 overflow-auto p-[16px] flex flex-col gap-[12px]">
        {encounterData.encounterGroups.length === 0 ? (
          <div className="flex-1 flex flex-col items-center justify-center text-text-secondary text-[13px] gap-[8px]">
            <span>No encounter groups defined for this map.</span>
            <span className="text-[11px]">Click "Add Group" to create one.</span>
          </div>
        ) : (
          encounterData.encounterGroups.map((_, i) => (
            <EncounterGroupCard key={i} groupIndex={i} species={species} />
          ))
        )}
      </div>
    </div>
  )
}
