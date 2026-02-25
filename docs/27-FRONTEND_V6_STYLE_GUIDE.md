# Frontend V6 Style Guide

**Version:** 6.0
**Last Updated:** 2025-11-28
**Status:** Living Document

---

## Overview

This guide establishes consistent UI/UX patterns, component usage, and styling conventions for the frontend application (V6). Following these guidelines ensures visual consistency, maintainable code, and a cohesive user experience.

---

## Table of Contents

1. [Button Variants & Usage](#button-variants--usage)
2. [Icon Guidelines](#icon-guidelines)
3. [Layout Patterns](#layout-patterns)
4. [Color System](#color-system)
5. [Typography](#typography)
6. [Component Patterns](#component-patterns)

---

## Button Variants & Usage

### Variant Reference

| Variant | Use Case | Visual Style |
|---------|----------|--------------|
| `outline` | **Primary actions** | Border, transparent background, becomes filled on hover |
| `ghost` | **Secondary actions** | No border/background, subtle hover effect |
| `destructive` | **Destructive actions** (delete, remove) | Red background, white text |
| `default` | Reserved for special cases | Filled primary color background |
| `secondary` | Rarely used | Muted background |
| `link` | Text links only | Underlined text |

### Button Sizing

**Always use icon-only buttons in toolbars and dialogs.**

#### Standard Button Dimensions

- **Toolbar/Dialog buttons:** `h-7 w-7` (28px)
- **Icons within buttons:** `h-3.5 w-3.5` (14px)
- **Button size prop:** `size="icon"`

### Button Order in Toolbars

Left to right:
1. **Destructive actions** (if applicable) - `variant="destructive"`
2. **Secondary actions** - `variant="ghost"`
3. **Primary action** - `variant="outline"`

### Accessibility

All icon-only buttons **must** have a `title` attribute for tooltips.

---

## Icon Guidelines

### Icon Sizing

| Context | Class | Size |
|---------|-------|------|
| Buttons | `h-3.5 w-3.5` | 14px |
| Toolbar icons | `h-4 w-4` | 16px |
| Status indicators | `h-4 w-4` | 16px |
| Large icons | `h-5 w-5` | 20px |

### Icon Library

Use **Lucide React** icons exclusively for consistency.

**Common icons:**
- Save: `<Save />`
- Delete: `<Trash2 />`
- Cancel/Close: `<X />`
- Edit: `<Edit />`
- Refresh: `<RefreshCw />`
- Add: `<Plus />`
- Models: `<Database />`
- Key: `<Key />`

### Icon-only vs. Icon + Text

**Toolbar buttons:** Icon-only (with `title` tooltip)
**Page content buttons:** May use icon + text for clarity in forms

---

## Layout Patterns

### Page Structure

All pages follow a consistent full-height flex layout:
- `PageToolbar` at top (fixed height)
- Scrollable content area below (`flex-1 overflow-y-auto`)
- Content wrapped in `max-w-2xl` container with `space-y-4`

### Toolbar Pattern

Use `PageToolbar` component for all page headers with:
- Icon (left)
- Title and subtitle (center-left)
- Action buttons (right)

### Dialog Structure

**Padding:**
- Use consistent padding: `px-6 py-6` for content areas
- Maintain visual hierarchy with spacing

**Action Buttons:**
- Dialog action buttons are icon-only with `h-7 w-7`

---

## Color System

### Status Colors

| Status | Color Variable | Visual |
|--------|---------------|--------|
| Success/Valid | `text-emerald-500` | Green |
| Error/Invalid | `text-red-500` | Red |
| Warning | `text-amber-500` | Amber/Orange |
| Inactive | `text-muted-foreground` | Gray |
| Info | `text-primary` | Blue |

### Background Colors

| Usage | Class |
|-------|-------|
| Page background | `bg-background` |
| Muted sections | `bg-muted/30` |
| Borders | `border-border/50` |
| Primary element | `bg-primary/10` |
| Destructive background | `bg-destructive/10` |

### Gradient Colors

**Icon container gradients for page headers:**

Use subtle gradients (20% opacity) with matching text colors for visual hierarchy:

| Page Type | Gradient Classes | Text Color | Use Case |
|-----------|-----------------|------------|----------|
| Browser/Web | `bg-gradient-to-br from-blue-500/20 to-cyan-500/20` | `text-blue-500` | Browser setup, Web features, External services |
| Desktop/Local | `bg-gradient-to-br from-violet-500/20 to-purple-500/20` | `text-violet-500` | Electron app, Desktop setup, Local features |
| General/Data | `bg-gradient-to-br from-primary/20 to-primary/5` | `text-primary` | Models, Providers, Data management |
| Success | `bg-gradient-to-br from-emerald-500/20 to-green-500/20` | `text-emerald-500` | Completed states, Success pages |
| Settings | `bg-gradient-to-br from-slate-500/20 to-gray-500/20` | `text-slate-500` | Configuration, Settings |

**Usage pattern:**
```tsx
<div className="flex h-12 w-12 items-center justify-center rounded-2xl bg-gradient-to-br from-blue-500/20 to-cyan-500/20">
  <Icon className="h-6 w-6 text-blue-500" />
</div>
```

**Gradient guidelines:**
- Always use `bg-gradient-to-br` (bottom-right direction)
- First color: 20% opacity (`/20`)
- Second color: 20% opacity or less (`/20` or `/5`)
- Icon text color matches the base gradient color
- Container size: `h-12 w-12` with `rounded-2xl`

---

## Typography

### Text Sizes

| Usage | Class | Size |
|-------|-------|------|
| Page title | `text-xl font-semibold` | 20px |
| Section heading | `text-sm font-semibold` | 14px |
| Body text | `text-sm` | 14px |
| Muted text | `text-xs text-muted-foreground` | 12px |
| Monospace | `font-mono text-xs` | 12px |

### Text Colors

- Primary text: `text-foreground`
- Secondary text: `text-muted-foreground`
- Accent text: `text-primary`

---

## Component Patterns

### Page Headers

**Polished header pattern with colored icon container:**

```tsx
<div className="flex-shrink-0 px-6 py-5 border-b border-border/50">
  <div className="flex items-center gap-4">
    <div className="flex h-12 w-12 items-center justify-center rounded-2xl bg-gradient-to-br from-blue-500/20 to-cyan-500/20">
      <Icon className="h-6 w-6 text-blue-500" />
    </div>
    <div>
      <h1 className="text-xl font-semibold">Page Title</h1>
      <p className="text-sm text-muted-foreground">
        Page description or subtitle
      </p>
    </div>
  </div>
</div>
```

**Icon container gradient colors by page type:**
- **Blue/Cyan** (`from-blue-500/20 to-cyan-500/20`, `text-blue-500`): Browser, Web, External
- **Violet/Purple** (`from-violet-500/20 to-purple-500/20`, `text-violet-500`): Desktop, Electron, Local
- **Primary** (`from-primary/20 to-primary/5`, `text-primary`): General pages, Models, Data

**Header structure:**
- Container: `rounded-2xl` with `h-12 w-12`
- Gradient: `bg-gradient-to-br` with 20% opacity colors
- Icon: `h-6 w-6` matching the gradient base color
- Spacing: `px-6 py-5` for header, `gap-4` between icon and text
- Border: `border-b border-border/50` for subtle separation

### Step Cards (for Setup/Guide Pages)

**Interactive step cards with completion states:**

```tsx
<div className={cn(
  'flex items-start gap-4 rounded-xl p-4 transition-colors',
  status === 'complete' ? 'bg-emerald-500/5' : 'bg-muted/30'
)}>
  <div className={cn(
    'flex h-10 w-10 items-center justify-center rounded-xl flex-shrink-0',
    status === 'complete' ? 'bg-emerald-500/20' : 'bg-muted'
  )}>
    {status === 'complete' ? (
      <CheckCircle2 className="h-5 w-5 text-emerald-500" />
    ) : (
      <Icon className="h-5 w-5 text-muted-foreground" />
    )}
  </div>
  <div className="flex-1 min-w-0">
    <div className="flex items-center gap-2">
      <span className={cn(
        'text-xs font-medium px-2 py-0.5 rounded-full',
        status === 'complete'
          ? 'bg-emerald-500/20 text-emerald-600 dark:text-emerald-400'
          : 'bg-muted text-muted-foreground'
      )}>
        Step {number}
      </span>
      {status === 'complete' && (
        <ArrowRight className="h-3 w-3 text-emerald-500" />
      )}
    </div>
    <div className="font-medium mt-1">{title}</div>
    <div className="text-sm text-muted-foreground mt-0.5">{description}</div>
  </div>
</div>
```

**Step card states:**
- **Pending**: `bg-muted/30` background, gray icon in `bg-muted` circle
- **Complete**: `bg-emerald-500/5` background, green checkmark in `bg-emerald-500/20` circle, green "Step N" badge with arrow

### Info Sections

**Rounded info boxes with subtle background:**

```tsx
<section className="rounded-xl bg-muted/30 p-4">
  <p className="text-sm text-muted-foreground">
    Informational text content...
  </p>
</section>
```

**Grid status cards:**

```tsx
<div className="grid grid-cols-2 gap-4">
  <div className="rounded-xl bg-muted/30 p-4">
    <div className="text-xs text-muted-foreground mb-2">Label</div>
    <div className="flex items-center gap-2">
      <StatusIndicator status="success" size="sm" />
      <span className="font-medium">Value</span>
    </div>
  </div>
</div>
```

**Pattern details:**
- Container: `rounded-xl bg-muted/30 p-4`
- Label: `text-xs text-muted-foreground mb-2`
- Value: `font-medium` with StatusIndicator
- Grid: `grid-cols-2` or `grid-cols-3` with `gap-4`

### Section Headers

**Uppercase section labels:**

```tsx
<h2 className="text-sm font-medium text-muted-foreground uppercase tracking-wide">
  Section Title
</h2>
```

**Pattern:**
- Small text: `text-sm`
- Medium weight: `font-medium`
- Muted color: `text-muted-foreground`
- Uppercase: `uppercase`
- Tracking: `tracking-wide`
- Used for: "Setup Steps", "Current Status", "Connection Info"

### Interactive Copy Cards

**Hover-to-copy pattern:**

```tsx
<div
  className="rounded-xl bg-muted/30 p-4 cursor-pointer group hover:bg-muted/50 transition-colors"
  onClick={() => copyToClipboard(text)}
>
  <div className="flex items-center justify-between">
    <div>
      <div className="text-xs text-muted-foreground mb-1">Label</div>
      <div className="font-mono text-sm">{value}</div>
    </div>
    <Button
      variant="ghost"
      size="icon"
      className="h-7 w-7 opacity-0 group-hover:opacity-100 transition-opacity"
    >
      {copied ? (
        <Check className="h-3.5 w-3.5 text-emerald-500" />
      ) : (
        <Copy className="h-3.5 w-3.5" />
      )}
    </Button>
  </div>
</div>
```

**Features:**
- Hover effect: `hover:bg-muted/50`
- Hidden button: `opacity-0 group-hover:opacity-100`
- Copy feedback: Check icon in green when copied
- Monospace values: `font-mono text-sm`

### Forms

- Use `space-y-4` for form container
- Use `space-y-2` for individual field groups
- Always pair `Label` with `Input` using matching `id`/`htmlFor`
- Provide meaningful placeholder text

### Status Indicators

Use `StatusIndicator` component:
- **Status values:** `'success' | 'error' | 'warning' | 'inactive'`
- **Sizes:** `'sm' | 'md' | 'lg'`
- **Optional label:** Set `showLabel={false}` for icon-only

### Action Lists

Use `ActionList` for list-based UIs with built-in toolbar support:

**Header styling:**
- Gradient icon container: `h-12 w-12 rounded-2xl bg-gradient-to-br from-primary/20 to-primary/5`
- Icon size: `h-6 w-6`
- Padding: `px-6 py-5`
- Border: `border-b border-border/50`

**Row sizing:**
- Small: `px-4 py-1.5`
- Default: `px-5 py-2`
- Large: `px-6 py-2.5`

**Row icon container:**
- Size: `h-9 w-9` (smaller than header's h-12)
- Border radius: `rounded-lg` (less rounded than header's rounded-2xl)
- Background: `bg-muted/30` (lighter than header)
- Hover: `hover:bg-muted/50`

**Features:**
- Consistent header with gradient icon
- Toolbar with tabs, search, filters
- Empty state messaging
- Compact rows visually distinct from header

---

## Best Practices

### DO ✅

- Use icon-only buttons in toolbars and dialogs
- Always provide `title` attributes for icon buttons
- Follow the button variant hierarchy (primary=outline, secondary=ghost)
- Use consistent icon sizes (`h-3.5 w-3.5` in buttons)
- Use `PageToolbar` for page headers
- Maintain consistent spacing with Tailwind utility classes

### DON'T ❌

- Don't use text labels on toolbar buttons
- Don't mix button variants inconsistently
- Don't use `variant="default"` for primary actions
- Don't forget accessibility attributes
- Don't create custom button sizes - use the standard `h-7 w-7`
- Don't use inline styles - use Tailwind classes

---

## Reference Examples

See actual implementations:
- **Form Page:** `ApiKeyFormPage.tsx`
- **Dialog with Actions:** `ModelsDialog.tsx`
- **Status Display:** `ProxyDialog.tsx`

---

## Version History

- **6.1** (2025-11-28): Added polished UI patterns
  - Page headers with colored gradient icon containers
  - Step cards with completion states
  - Info sections and grid status cards
  - Section headers with uppercase labels
  - Interactive copy cards with hover effects
  - Gradient color system for icon containers
  - ActionList row sizing and visual hierarchy guidelines

- **6.0** (2025-11-28): Initial style guide created
  - Established button variant conventions
  - Defined icon-only button pattern
  - Documented layout patterns and component usage
