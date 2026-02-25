# Color Palette Guide

**Version:** 1.0
**Last Updated:** 2025-12-22
**Status:** Living Document

---

## Overview

This guide documents color palette decisions for the application, a cross-platform desktop download manager. The recommendations are based on research into modern UI design trends, successful applications (Linear, Discord, VS Code, Raycast), and established design systems (Tailwind CSS, Radix UI, shadcn/ui).

---

## Table of Contents

1. [Design Philosophy](#design-philosophy)
2. [Research Findings](#research-findings)
3. [Gray Scale Selection](#gray-scale-selection)
4. [Accent Color Options](#accent-color-options)
5. [Recommended Palette](#recommended-palette)
6. [Dark Mode Considerations](#dark-mode-considerations)
7. [Accessibility Requirements](#accessibility-requirements)
8. [Implementation](#implementation)
9. [Sources](#sources)

---

## Design Philosophy

### Core Principles

| Principle | Description |
|-----------|-------------|
| **Neutral Foundation** | Gray is the foundation - text, backgrounds, borders, dividers are all gray |
| **Minimal Accent** | Limit color usage; use accent sparingly for focus and CTAs |
| **High Contrast** | Prioritize readability with sufficient contrast ratios |
| **Cross-Platform** | Avoid platform-specific aesthetics (no Windows blue, no macOS aqua) |
| **Dark-First** | Design for dark mode first; 85%+ of developers prefer dark interfaces |

### The 60/30/10 Rule

- **60%** - Neutral colors (backgrounds, surfaces)
- **30%** - Secondary colors (cards, borders, muted elements)
- **10%** - Accent colors (primary actions, focus indicators)

---

## Research Findings

### Industry Trends (2024-2025)

1. **Linear Design Trend**: Named after Linear app - emphasizes neutral, near-monochromatic palettes with minimal chrome. Linear reduced blue usage in 2024-2025, moving toward more neutral appearance.

2. **Dark Mode Dominance**: Over 70% of users prefer dark mode. Best practice: avoid pure black (#000); use dark grays like #181A1B, #1e1e1e, or #18181b for softer appearance.

3. **Accent Restraint**: Modern apps use accent color sparingly - only for primary actions and focus states. Discord's "blurple" (#5865F2), Linear's indigo, and VS Code's blue (#007acc) are used minimally against neutral backgrounds.

4. **Semantic Color Structure**:
   - Base colors (primitives): Core brand colors
   - Usage colors (semantics): Surface, fill, text, border categories
   - Status colors: Success (green), warning (amber), error (red), info (blue)

### Reference Applications

| Application | Dark Background | Accent | Style |
|-------------|-----------------|--------|-------|
| VS Code | #1e1e1e | #007acc (blue) | Minimal, professional |
| Discord | #313338 / #1e1f22 | #5865f2 (blurple) | Modern, friendly |
| Linear | Woodsmoke gray | Indigo | Neutral, focused |
| Raycast | Dark purple-gray | Purple | Modern, polished |

---

## Gray Scale Selection

### Tailwind Gray Options

Tailwind provides five gray scales. Each has different undertones:

| Scale | Undertone | Best For |
|-------|-----------|----------|
| **Slate** | Cool blue | Tech apps, code editors |
| **Gray** | Neutral-cool | General purpose |
| **Zinc** | Warm-neutral | Modern SaaS, balanced |
| **Neutral** | Pure gray (no hue) | Minimalist, photography |
| **Stone** | Warm beige | Organic, earthy |

### Recommendation: Zinc or Slate

**Zinc** - Best for modern cross-platform apps:
- Subtle warmth without being yellow/brown
- Feels modern and professional
- Used by many successful SaaS products

**Slate** - Best for developer tools:
- Cool blue undertone
- Feels technical and precise
- Similar to VS Code, GitHub

### Key Zinc Values (Tailwind)

```
zinc-50:  #fafafa  (light bg)
zinc-100: #f4f4f5  (light surface)
zinc-200: #e4e4e7  (light border)
zinc-300: #d4d4d8  (light muted)
zinc-400: #a1a1a6  (muted text light)
zinc-500: #71717a  (placeholder)
zinc-600: #52525b  (muted text dark)
zinc-700: #3f3f46  (dark border)
zinc-800: #27272a  (dark surface)
zinc-900: #18181b  (dark bg)
zinc-950: #09090b  (darkest)
```

### Key Slate Values (Tailwind)

```
slate-50:  #f8fafc  (light bg)
slate-100: #f1f5f9  (light surface)
slate-200: #e2e8f0  (light border)
slate-300: #cbd5e1  (light muted)
slate-400: #94a3b8  (muted text light)
slate-500: #64748b  (placeholder)
slate-600: #475569  (muted text dark)
slate-700: #334155  (dark border)
slate-800: #1e293b  (dark surface)
slate-900: #0f172a  (dark bg)
slate-950: #020617  (darkest)
```

---

## Accent Color Options

### Option 1: Indigo/Violet (Recommended)

Modern, professional, works well in both light and dark modes.

```
indigo-500: #6366f1
indigo-600: #4f46e5
violet-500: #8b5cf6
```

**Pros**: Distinctive, modern, not overused like blue
**Cons**: Can feel "techy" - may not suit all brands

### Option 2: Teal/Cyan

Calm, trustworthy, differentiates from typical blue.

```
teal-500:  #14b8a6
teal-600:  #0d9488
cyan-500:  #06b6d4
```

**Pros**: Fresh, distinctive, calming
**Cons**: Can feel cold, less energetic

### Option 3: Blue (Classic)

Traditional, safe, universally understood as "action".

```
blue-500: #3b82f6
blue-600: #2563eb
```

**Pros**: Familiar, accessible, professional
**Cons**: Overused, can feel generic

### Option 4: Blurple (Discord-style)

Modern, friendly, memorable.

```
blurple: #5865f2
```

**Pros**: Modern, distinctive, great accessibility
**Cons**: Strongly associated with Discord

---

## Recommended Palette

Based on research, here's the recommended palette using **Zinc gray** with **Indigo accent**:

### Light Theme

```css
:root {
  /* Backgrounds */
  --background: 240 5% 96%;        /* zinc-100 equivalent */
  --foreground: 240 6% 10%;        /* zinc-900 equivalent */

  /* Cards & Surfaces */
  --card: 0 0% 100%;               /* white */
  --card-foreground: 240 6% 10%;

  /* Primary Accent */
  --primary: 239 84% 67%;          /* indigo-500 */
  --primary-foreground: 0 0% 100%;

  /* Secondary */
  --secondary: 240 5% 93%;         /* zinc-200 equivalent */
  --secondary-foreground: 240 6% 15%;

  /* Muted */
  --muted: 240 5% 93%;
  --muted-foreground: 240 4% 46%;  /* zinc-500 equivalent */

  /* Borders */
  --border: 240 6% 87%;            /* zinc-200 equivalent */
  --input: 240 6% 87%;
  --ring: 239 84% 67%;             /* matches primary */

  /* Status */
  --success: 142 76% 36%;          /* green-600 */
  --warning: 38 92% 50%;           /* amber-500 */
  --destructive: 0 84% 60%;        /* red-500 */
  --info: 199 89% 48%;             /* sky-500 */
}
```

### Dark Theme

```css
.dark {
  /* Backgrounds - avoid pure black */
  --background: 240 6% 10%;        /* zinc-900 equivalent */
  --foreground: 240 5% 84%;        /* zinc-300 equivalent */

  /* Cards & Surfaces */
  --card: 240 5% 13%;              /* zinc-850 equivalent */
  --card-foreground: 240 5% 84%;

  /* Primary Accent - slightly brighter for dark */
  --primary: 239 84% 70%;          /* indigo-400 */
  --primary-foreground: 0 0% 100%;

  /* Secondary */
  --secondary: 240 4% 18%;         /* zinc-800 equivalent */
  --secondary-foreground: 240 5% 84%;

  /* Muted */
  --muted: 240 4% 18%;
  --muted-foreground: 240 4% 55%;  /* zinc-500 equivalent */

  /* Borders */
  --border: 240 4% 22%;            /* zinc-700 equivalent */
  --input: 240 4% 22%;
  --ring: 239 84% 70%;

  /* Status - brighter for dark mode */
  --success: 142 69% 45%;
  --warning: 38 92% 55%;
  --destructive: 0 72% 55%;
  --info: 199 89% 55%;
}
```

---

## Dark Mode Considerations

### Background Depth Hierarchy

Create visual depth using progressively lighter surfaces:

| Layer | Light Mode | Dark Mode | Usage |
|-------|------------|-----------|-------|
| Base | zinc-100 | zinc-900 | App background |
| Surface | white | zinc-850 | Cards, panels |
| Elevated | white + shadow | zinc-800 | Dropdowns, modals |
| Overlay | white + shadow | zinc-750 | Tooltips, popovers |

### Why Not Pure Black?

Pure black (#000000) creates harsh contrast and can cause eye strain. Research shows dark gray backgrounds (#18181b, #1e1e1e) are:
- Easier on the eyes
- Better for OLED screens (less haloing)
- More professional appearance
- Allow for depth hierarchy

### Contrast Ratios

Text on backgrounds should meet WCAG AA standards:

| Combination | Minimum Ratio | Target |
|-------------|---------------|--------|
| Body text | 4.5:1 | 7:1 (AAA) |
| Large text (18px+) | 3:1 | 4.5:1 |
| UI components | 3:1 | 4.5:1 |

---

## Accessibility Requirements

### Minimum Contrast Ratios (WCAG 2.1)

| Element | AA Standard | AAA Standard |
|---------|-------------|--------------|
| Normal text | 4.5:1 | 7:1 |
| Large text | 3:1 | 4.5:1 |
| UI components | 3:1 | 3:1 |

### Color-Blind Considerations

- Never use color alone to convey information
- Pair status colors with icons or text
- Test with color blindness simulators
- Use patterns/textures as secondary indicators

### Status Color Accessibility

| Status | Color | Icon | Text Alternative |
|--------|-------|------|------------------|
| Success | Green | Checkmark | "Complete", "Success" |
| Warning | Amber | Triangle/! | "Warning", "Caution" |
| Error | Red | X or Circle | "Error", "Failed" |
| Info | Blue | i or Circle | "Info", "Note" |

---

## Implementation

### HSL Format

The palette uses HSL format for CSS variables, enabling easy theming:

```css
/* HSL allows easy manipulation */
--primary: 239 84% 67%;

/* Usage */
background-color: hsl(var(--primary));
background-color: hsl(var(--primary) / 0.1);  /* 10% opacity */
```

### Tailwind v4 Integration

With Tailwind v4's `@theme` directive:

```css
@theme {
  --color-background: hsl(var(--background));
  --color-foreground: hsl(var(--foreground));
  --color-primary: hsl(var(--primary));
  /* ... */
}
```

### Usage Examples

```tsx
// Background
<div className="bg-background text-foreground">

// Primary button
<button className="bg-primary text-primary-foreground">

// Muted text
<p className="text-muted-foreground">

// Card
<div className="bg-card border border-border">

// Status
<span className="text-success">Success</span>
<span className="text-destructive">Error</span>
```

---

## Sources

### Design Systems & Documentation
- [Tailwind CSS Colors](https://tailwindcss.com/docs/colors) - Official color palette documentation
- [shadcn/ui Colors](https://ui.shadcn.com/colors) - Theme color formats
- [Radix Themes Color](https://www.radix-ui.com/themes/docs/theme/color) - Accent and gray scale options

### Research Articles
- [Linear Design: The SaaS design trend](https://blog.logrocket.com/ux-design/linear-design/) - LogRocket analysis
- [How we redesigned the Linear UI](https://linear.app/now/how-we-redesigned-the-linear-ui) - Linear's design evolution
- [Dark Mode Design Best Practices 2025](https://muksalcreative.com/2025/07/26/dark-mode-design-best-practices-2025/) - Dark mode trends
- [Color Systems for SaaS](https://www.merveilleux.design/en/blog/article/color-systems-for-saas) - Color system structure

### Color References
- [Discord Color Codes](https://www.eggradients.com/tool/discord-color-codes) - Discord palette
- [VS Code Theme Colors](https://code.visualstudio.com/api/references/theme-color) - VS Code reference
- [Blurple Color Meaning](https://mobbin.com/colors/meaning/blurple) - Modern accent analysis
- [Figma Color Combinations](https://www.figma.com/resource-library/color-combinations/) - Design inspiration

### Industry Statistics
- Stack Overflow Survey: 85%+ of developers prefer dark interfaces
- Mobile users: 70%+ prefer dark mode where available
- Neon colors increase engagement by 20% in digital ads (2025 report)

---

## Version History

- **1.0** (2025-12-22): Initial guide created
  - Research-based recommendations
  - Zinc gray + Indigo accent palette
  - Dark mode best practices
  - Accessibility requirements
  - Implementation guidelines
