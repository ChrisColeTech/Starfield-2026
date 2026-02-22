export interface MetadataCredit {
  file: string;
  notes?: string;
  authors?: string[];
  licenses?: string[];
  urls?: string[];
}

export interface MetadataLayer {
  zPos?: number;
  custom_animation?: string;
  [bodyType: string]: string | number | undefined;
}

export interface MetadataSourceItem {
  name?: string;
  priority?: number | null;
  type_name?: string;
  required?: string[];
  animations?: string[];
  tags?: string[];
  required_tags?: string[];
  excluded_tags?: string[];
  path?: string[];
  variants?: string[];
  layers?: Record<string, MetadataLayer>;
  credits?: MetadataCredit[];
  [key: string]: unknown;
}

export interface MetadataItem {
  id: string;
  name: string;
  typeName: string;
  priority: number | null;
  required: string[];
  animations: string[];
  tags: string[];
  requiredTags: string[];
  excludedTags: string[];
  path: string[];
  variants: string[];
  layers: Record<string, MetadataLayer>;
  credits: MetadataCredit[];
  raw: MetadataSourceItem;
}

export interface CategoryTreeNode {
  items: string[];
  children: Record<string, CategoryTreeNode>;
  label?: string;
  priority?: number;
  required?: string[];
  animations?: string[];
}

export interface LoadedMetadata {
  sourcePath: string;
  itemsById: Record<string, MetadataItem>;
  items: MetadataItem[];
  categoryTree: CategoryTreeNode;
  bodyTypes: string[];
}
