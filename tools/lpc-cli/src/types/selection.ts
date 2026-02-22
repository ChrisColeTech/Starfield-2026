export interface NormalizedSelection {
  itemId: string;
  variant: string | null;
}

export interface NormalizedPreset {
  bodyType: string;
  selections: NormalizedSelection[];
}
