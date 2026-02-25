// ── Menu types ──

export interface MenuItem {
    label: string;
    shortcut?: string;
    disabled?: boolean;
    danger?: boolean;
    separator?: boolean;
    onClick?: () => void;
    children?: MenuItem[];
}

export interface MenuDefinition {
    label: string;
    items: MenuItem[];
    active?: boolean;
}
