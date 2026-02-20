/** Color palettes for sprite generation, matching Game Boy-era aesthetics. */

export const GRASS_PALETTE = {
  base: ['#4a8c3f', '#3d7a35', '#5a9c4f'],
  highlight: ['#6ab85a', '#7cc86a'],
  shadow: ['#2d6b25', '#1f5c1a'],
};

export const FLOWER_PALETTES: Record<string, { petals: string[]; center: string }> = {
  red:    { petals: ['#e74c3c', '#c0392b', '#ff6b6b'], center: '#f1c40f' },
  blue:   { petals: ['#3498db', '#2980b9', '#5dade2'], center: '#f1c40f' },
  yellow: { petals: ['#f1c40f', '#f39c12', '#ffd700'], center: '#e67e22' },
  pink:   { petals: ['#e91e8c', '#c0196c', '#ff69b4'], center: '#f1c40f' },
  white:  { petals: ['#ecf0f1', '#d5dbdb', '#ffffff'], center: '#f1c40f' },
};

export const TREE_PALETTES = {
  green: {
    leaves: ['#2d8c3f', '#3a9c4f', '#1f7a2f'],
    trunk: ['#8b6914', '#6b4f10'],
  },
  autumn: {
    leaves: ['#e74c3c', '#e67e22', '#f1c40f', '#d35400'],
    trunk: ['#8b6914', '#6b4f10'],
  },
};

export const BUSH_PALETTE = {
  leaves: ['#3d7a35', '#4a8c3f', '#2d6b25'],
  berry: ['#e74c3c', '#c0392b'],
};
