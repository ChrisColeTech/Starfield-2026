import { create } from 'zustand';
import type { GeneratorType, GalleryItem } from '../types/generator';

export interface ImportFile {
  file: File;
  preview: string; // object URL
  order: number;
}

interface SpriteGenState {
  // Generator
  selectedType: GeneratorType;
  selectedVariant: string | null;
  seed: number;
  frameCount: number;
  setType: (type: GeneratorType) => void;
  setVariant: (variant: string | null) => void;
  setSeed: (seed: number) => void;
  setFrameCount: (count: number) => void;
  randomizeSeed: () => void;

  // Preview
  frames: string[];
  currentFrame: number;
  isPlaying: boolean;
  playbackSpeed: number;
  setFrames: (frames: string[]) => void;
  setCurrentFrame: (index: number) => void;
  togglePlayback: () => void;
  setPlaybackSpeed: (speed: number) => void;

  // Import
  importFiles: ImportFile[];
  importBaseName: string;
  addImportFiles: (files: File[]) => void;
  removeImportFile: (index: number) => void;
  reorderImportFile: (from: number, to: number) => void;
  clearImportFiles: () => void;
  setImportBaseName: (name: string) => void;

  // Gallery
  galleryItems: GalleryItem[];
  galleryFilter: string;
  setGalleryItems: (items: GalleryItem[]) => void;
  setGalleryFilter: (filter: string) => void;

  // UI
  isLoading: boolean;
  error: string | null;
  sidebarCollapsed: boolean;
  galleryCollapsed: boolean;
  showGrid: boolean;
  setLoading: (loading: boolean) => void;
  setError: (error: string | null) => void;
  toggleSidebar: () => void;
  toggleGallery: () => void;
  toggleGrid: () => void;
}

export const useStore = create<SpriteGenState>((set, get) => ({
  // Generator defaults
  selectedType: 'grass',
  selectedVariant: null,
  seed: Math.floor(Math.random() * 99999),
  frameCount: 5,
  setType: (type) => set({ selectedType: type, selectedVariant: null }),
  setVariant: (variant) => set({ selectedVariant: variant }),
  setSeed: (seed) => set({ seed }),
  setFrameCount: (count) => set({ frameCount: Math.max(1, Math.min(12, count)) }),
  randomizeSeed: () => set({ seed: Math.floor(Math.random() * 99999) }),

  // Preview defaults
  frames: [],
  currentFrame: 0,
  isPlaying: false,
  playbackSpeed: 200,
  setFrames: (frames) => set({ frames, currentFrame: 0 }),
  setCurrentFrame: (index) => set({ currentFrame: index }),
  togglePlayback: () => set((s) => ({ isPlaying: !s.isPlaying })),
  setPlaybackSpeed: (speed) => set({ playbackSpeed: speed }),

  // Import defaults
  importFiles: [],
  importBaseName: 'tile_custom',
  addImportFiles: (files) => {
    const current = get().importFiles;
    const newFiles: ImportFile[] = files.map((file, i) => ({
      file,
      preview: URL.createObjectURL(file),
      order: current.length + i,
    }));
    set({ importFiles: [...current, ...newFiles] });
  },
  removeImportFile: (index) => {
    const files = [...get().importFiles];
    const [removed] = files.splice(index, 1);
    URL.revokeObjectURL(removed.preview);
    set({ importFiles: files.map((f, i) => ({ ...f, order: i })) });
  },
  reorderImportFile: (from, to) => {
    const files = [...get().importFiles];
    const [moved] = files.splice(from, 1);
    files.splice(to, 0, moved);
    set({ importFiles: files.map((f, i) => ({ ...f, order: i })) });
  },
  clearImportFiles: () => {
    get().importFiles.forEach((f) => URL.revokeObjectURL(f.preview));
    set({ importFiles: [] });
  },
  setImportBaseName: (name) => set({ importBaseName: name }),

  // Gallery defaults
  galleryItems: [],
  galleryFilter: '',
  setGalleryItems: (items) => set({ galleryItems: items }),
  setGalleryFilter: (filter) => set({ galleryFilter: filter }),

  // UI defaults
  isLoading: false,
  error: null,
  sidebarCollapsed: false,
  galleryCollapsed: false,
  showGrid: true,
  setLoading: (loading) => set({ isLoading: loading }),
  setError: (error) => set({ error }),
  toggleSidebar: () => set((s) => ({ sidebarCollapsed: !s.sidebarCollapsed })),
  toggleGallery: () => set((s) => ({ galleryCollapsed: !s.galleryCollapsed })),
  toggleGrid: () => set((s) => ({ showGrid: !s.showGrid })),
}));
