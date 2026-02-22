// ── Menu types ──

export interface MenuItem {
    label: string;
    shortcut?: string;
    disabled?: boolean;
    danger?: boolean;
    separator?: boolean;
    onClick?: () => void;
}

export interface MenuDefinition {
    label: string;
    items: MenuItem[];
}
