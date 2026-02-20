import { create } from 'zustand'
import * as THREE from 'three'
import type { SplitManifest } from '../types/animation'
import { loadModelOnly, loadClipDae } from '../services/sceneService'
import { useEditorStore } from './editorStore'

const API_BASE = 'http://localhost:3001'

interface AnimationEditorState {
  folderPath: string | null
  manifest: SplitManifest | null
  dirty: boolean
  saving: boolean
  loading: boolean
  error: string | null

  // Currently selected model index (for multi-model manifests)
  activeModelIndex: number

  // Clip selection
  activeClipIndex: number
  clipLoading: boolean

  // Actions
  loadFolder: (dir: string) => Promise<void>
  selectClip: (index: number) => Promise<void>
  tagClip: (index: number, semanticName: string | null) => void
  autoTag: () => void
  save: () => Promise<void>
  reset: () => void
}

/** Overworld character animation slot map — matches C# OhanaCli MapOverworldSlot */
const OVERWORLD_SLOT_MAP: Record<number, string> = {
  0: 'Idle',
  1: 'Walk',
  2: 'Run',
  4: 'Jump',
  5: 'Land',
  7: 'ShortAction1',
  8: 'LongAction1',
  9: 'ShortAction2',
  17: 'MediumAction',
  20: 'Action',
  23: 'Action2',
  30: 'ShortAction3',
  31: 'ShortAction4',
  52: 'IdleVariant',
  54: 'ShortAction5',
  55: 'LongAction2',
  56: 'ShortAction6',
  59: 'Action3',
  61: 'Action4',
  72: 'Action5',
  123: 'LongAction3',
  124: 'Action6',
  125: 'Action7',
  127: 'Action8',
  128: 'Action9',
}

export const useAnimationEditorStore = create<AnimationEditorState>()((set, get) => ({
  folderPath: null,
  manifest: null,
  dirty: false,
  saving: false,
  loading: false,
  error: null,
  activeModelIndex: 0,
  activeClipIndex: -1,
  clipLoading: false,

  loadFolder: async (dir: string) => {
    set({ loading: true, error: null, folderPath: dir, dirty: false, activeClipIndex: -1 })

    try {
      // Fetch manifest from backend
      const res = await fetch(`${API_BASE}/api/manifests/read?dir=${encodeURIComponent(dir)}`)
      if (!res.ok) {
        const err = await res.json().catch(() => ({ error: res.statusText }))
        throw new Error(err.error || `HTTP ${res.status}`)
      }
      const manifest: SplitManifest = await res.json()

      if (!manifest.models || manifest.models.length === 0) {
        throw new Error('Manifest has no models')
      }

      // Load the model DAE (no animations)
      const model = manifest.models[0]
      const scene = await loadModelOnly(dir, model.modelFile)

      // Push scene to editorStore so Viewport renders it
      const editorStore = useEditorStore.getState()
      editorStore.setAnimationPlaying(false)
      useEditorStore.setState({
        scene,
        animations: [],
        sceneName: model.name,
        activeClipIndex: 0,
        animationPlaying: false,
      })

      set({
        manifest,
        loading: false,
        activeModelIndex: 0,
      })

      // Auto-select first clip
      if (model.clips.length > 0) {
        get().selectClip(0)
      }
    } catch (err) {
      set({
        error: err instanceof Error ? err.message : 'Failed to load folder',
        loading: false,
      })
    }
  },

  selectClip: async (index: number) => {
    const { manifest, folderPath, activeModelIndex } = get()
    if (!manifest || !folderPath) return

    const model = manifest.models[activeModelIndex]
    if (!model || index < 0 || index >= model.clips.length) return

    set({ activeClipIndex: index, clipLoading: true })

    try {
      const clip = model.clips[index]
      const editorScene = useEditorStore.getState().scene
      if (!editorScene) return

      const animations = await loadClipDae(folderPath, clip.file, editorScene)

      // Push to editorStore for Viewport playback
      useEditorStore.setState({
        animations,
        activeClipIndex: 0,
        animationPlaying: true,
      })

      set({ clipLoading: false })
    } catch (err) {
      console.warn('[AnimationEditor] Failed to load clip:', err)
      set({ clipLoading: false })
    }
  },

  tagClip: (index: number, semanticName: string | null) => {
    const { manifest, activeModelIndex } = get()
    if (!manifest) return

    const updated = structuredClone(manifest)
    const clip = updated.models[activeModelIndex]?.clips[index]
    if (!clip) return

    clip.semanticName = semanticName
    clip.semanticSource = semanticName ? 'manual' : null

    set({ manifest: updated, dirty: true })
  },

  autoTag: () => {
    const { manifest, activeModelIndex } = get()
    if (!manifest) return

    const updated = structuredClone(manifest)
    const model = updated.models[activeModelIndex]
    if (!model) return

    for (const clip of model.clips) {
      if (clip.semanticName) continue // skip already tagged

      // Try index-based mapping
      const tag = OVERWORLD_SLOT_MAP[clip.index]
      if (tag) {
        clip.semanticName = tag
        clip.semanticSource = 'auto-index'
      }
    }

    set({ manifest: updated, dirty: true })
  },

  save: async () => {
    const { manifest, folderPath } = get()
    if (!manifest || !folderPath) return

    set({ saving: true })
    try {
      const res = await fetch(`${API_BASE}/api/manifests/save`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ dir: folderPath, manifest }),
      })
      if (!res.ok) {
        const err = await res.json().catch(() => ({ error: res.statusText }))
        throw new Error(err.error || `HTTP ${res.status}`)
      }
      set({ saving: false, dirty: false })
    } catch (err) {
      console.error('[AnimationEditor] Save failed:', err)
      set({ saving: false })
    }
  },

  reset: () => {
    set({
      folderPath: null,
      manifest: null,
      dirty: false,
      saving: false,
      loading: false,
      error: null,
      activeModelIndex: 0,
      activeClipIndex: -1,
      clipLoading: false,
    })
  },
}))
