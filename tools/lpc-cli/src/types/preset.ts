export interface PresetSelection {
  itemId: string;
  variant?: string;
  enabled?: boolean;
}

export interface LpcPreset {
  version: number;
  bodyType: string;
  selections: PresetSelection[];
}

export interface PresetValidationIssue {
  code: string;
  message: string;
}

export interface PresetValidationResult {
  errors: PresetValidationIssue[];
  warnings: PresetValidationIssue[];
}
