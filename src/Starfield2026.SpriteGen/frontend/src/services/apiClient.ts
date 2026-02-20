import type { GeneratorType, GenerateResponse, GeneratorInfo, GalleryItem } from '../types/generator';

const BASE = '/api';

async function request<T>(path: string, options?: RequestInit): Promise<T> {
  const res = await fetch(`${BASE}${path}`, {
    headers: { 'Content-Type': 'application/json' },
    ...options,
  });
  if (!res.ok) {
    const body = await res.json().catch(() => ({}));
    throw new Error((body as { error?: string }).error ?? `HTTP ${res.status}`);
  }
  return res.json() as Promise<T>;
}

export const api = {
  getGenerators: () => request<GeneratorInfo[]>('/generators'),

  generate: (type: GeneratorType, seed: number, variant?: string, frames?: number) =>
    request<GenerateResponse>('/generate', {
      method: 'POST',
      body: JSON.stringify({ type, seed, variant, frames }),
    }),

  getGallery: (filter?: string) =>
    request<{ items: GalleryItem[]; total: number }>(`/gallery${filter ? `?filter=${filter}` : ''}`),

  save: (type: GeneratorType, seed: number, variant?: string) =>
    request<{ saved: string[]; count: number }>('/save', {
      method: 'POST',
      body: JSON.stringify({ type, seed, variant }),
    }),

  deleteSprite: (filename: string) =>
    request<{ success: boolean }>(`/sprites/${encodeURIComponent(filename)}`, { method: 'DELETE' }),

  clearAll: () => request<{ deleted: number }>('/clear', { method: 'POST' }),

  importFrames: (baseName: string, frames: { index: number; content: string; filename: string }[]) =>
    request<{ saved: string[]; count: number }>('/import', {
      method: 'POST',
      body: JSON.stringify({ baseName, frames }),
    }),
};
