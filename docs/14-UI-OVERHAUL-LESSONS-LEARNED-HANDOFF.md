# 14 — UI Overhaul (SRP Modernization): Lessons Learned & Handoff

**Date:** 2026-02-21
**Project:** Starfield-2026

---

## 1. What Was Accomplished

### New UI Layer Architecture (SRP)
- **Layer 1 — `UIDraw.cs`**: Low-level drawing primitives (rounded rects, shadowed text, vertical gradients). Scan-line based `RoundedRect` that avoids double-alpha artifacts with semi-transparent colors.
- **Layer 2 — Widgets**: `HPBar.cs` (triple-line HP bar with color transitions), `EXPBar.cs` (simple progress bar), `MessageBox.cs` (typewriter text, advance-on-key, callback), `MenuBox.cs` (grid menu with navigation), `BattlePanel.cs` (unified battle menu: static header text + configurable button grid with vertical separator, d-pad navigation, and separator-skipping logic).
- **Layer 3 — Screens**: `PartyScreen.cs` and `BagScreen.cs` (stubs — marked for deletion/rewrite).

### Files Updated
- **`BattleInfoBar.cs`** — Replaced all `UIStyle.*` calls with `UIDraw`/`HPBar`/`EXPBar`. Removed borders.
- **`HUDRenderer.cs`** — Corner-mounted panels (no borders, no shadows, no margin). Right panel: HP bar + HP text + coins. Left panel: ammo + boosts. Speed panel at bottom-left. All use matching `RoundedRect` with alpha 150.
- **`CharacterSelectScreen.cs`** — Replaced `UIStyle.DrawTripleGradient` → `UIDraw.VerticalGradient`, `UIStyle.DrawShadowedText` → `UIDraw.ShadowedText`.
- **`BattleUIManager.cs`** — Refactored to use `MessageBox` for narration and `BattlePanel` for menus separately. Main menu is a 2-column grid (Fight/Bag/Pokemon/Run). Fight menu grid (4-col, 2-row) is still assembled inline in `OpenFightMenu()` — needs to move into `BattlePanel`.
- **`MenuItem.cs`** — Added `IsSeparator` property for divider lines and a static `Separator()` factory method.

### Deleted
- **`UIStyle.cs`** — Monolithic class replaced by focused components.

---

## 2. What Work Remains

| Priority | Item | Notes |
|----------|------|-------|
| **HIGH** | **PartyScreen + BagScreen rewrite** | Current versions are placeholder quality. Need proper BDSP-modernized design with party sprites, correct color palette, and layout matching the game's identity. |
| ~~HIGH~~ | ~~**Move fight grid into BattlePanel**~~ | **DONE** — `ShowFightMenu()` method added to `BattlePanel`. Grid layout (moves left, actions right, separator) is now owned by the panel. |
| ~~HIGH~~ | ~~**BattlePanel fight menu proportions**~~ | **DONE** — Non-uniform `_columnWeights` (`{0.30, 0.30, 0.20, 0.20}`) implemented in `Draw()`. Moves columns are 50% wider than action columns. |
| **MED** | **RoundedRect perf** | Scan-line approach draws 1 rect per pixel row. Fine for small panels, costly for large. Consider batching middle rows into one draw call. |
| **MED** | **MessageBox + BattlePanel sizing** | Both currently fill the full bottom 1/4 of screen. May need independent sizing to match the old split layout proportions. |
| **LOW** | **MenuBox cleanup** | Old `MenuBox` still exists and is used by non-battle menus. Evaluate if it should share code with `BattlePanel` or stay separate. |

---

## 3. Optimizations — Prime Suspects

1. **`UIDraw.RoundedRect` draw call count** — Currently draws `bounds.Height` individual 1-pixel-tall rectangles. Fix: draw 3 rects for the middle body (top strip, center block, bottom strip) and only use scan-lines for the corner regions (top `r` rows and bottom `r` rows). This reduces draw calls from `~height` to `~2r + 3`.

2. **`VerticalGradient` draw call count** — Draws 1 rect per pixel row for the full gradient. Fix: use a pre-generated texture (1×256 gradient strip) and draw it as a single stretched sprite.

3. **`BattleInfoBar` redundant font scale sets** — `font.Scale` is set multiple times per frame across `DrawFoeBar`/`DrawAllyBar`. Cache the scale or set once and pass it through.

4. **`HUDRenderer` conditional panel creation** — Left panel is only drawn when ammo/boosts exist. The `if` checks run every frame. Minor, but could be hoisted to state-change events if perf becomes an issue.

---

## 4. Step-by-Step: Getting the App Fully Working

```
1. cd D:\Projects\Starfield-2026\src\Starfield2026.3D
2. dotnet build           # Verify 0 errors
3. dotnet run             # Game launches

# Battle test:
4. Walk into encounter grass → battle triggers
5. Verify: intro messages → "What will you do?" menu → Fight/Bag/Pokemon/Run
6. Press Fight → verify 4-column grid (moves in cols 0-1, Back/Mega/Power in cols 2-3, vertical separator)
7. Select a move → turn executes → damage applied → HP bars animate
8. Press Run → "You got away safely!" → fade to overworld

# HUD test:
9. Walk around overworld → right panel shows HP + coins
10. Enter driving mode → left panel shows ammo + boosts, bottom-left shows speed
```

---

## 5. How to Start/Test

```bash
# Build
cd D:\Projects\Starfield-2026\src\Starfield2026.3D
dotnet build

# Run
dotnet run

# Debug battle directly (if available):
# Set DebugStartInBattle = true in Starfield2026Game.cs
```

---

## 6. Known Issues & Strategies

### Issue 1: PartyScreen/BagScreen are placeholder garbage
**Strategy:** Delete both files. Design from scratch with a clear mockup (screenshot or hand-drawn) BEFORE writing code. Use the game's own color palette (not generic blue/orange/dark).

### Issue 2: BattlePanel fight menu uses uniform column widths
**Strategy:** The original layout had left 2/3 for moves and right 1/3 for actions. The current 4-column grid uses equal-width columns. Improve by implementing non-uniform column widths in `BattlePanel.Draw()` — accept a `float[] columnWeights` parameter to control proportions (e.g., `{0.33f, 0.33f, 0.17f, 0.17f}`).

### Issue 3: RoundedRect corner artifacts at very small radii
**Strategy:** The scan-line algorithm can produce 1-pixel gaps at small radii due to integer rounding. Clamp minimum radius to 2 and add a 1-pixel overlap row at the corner boundary.

### Issue 4: Font scale inconsistency across screens
**Strategy:** All UI components should derive scale from `UITheme.GetFontScale(screenW)` and not apply their own `scale - 1` reductions. Centralize scale computation and push it down as a parameter.

---

## 7. New Architecture & Features

### Architecture Changes
- **SRP Layer Split**: Drawing primitives (`UIDraw`) → Widgets (`HPBar`, `EXPBar`, `MessageBox`, `MenuBox`, `BattlePanel`) → Screen overlays (`PartyScreen`, `BagScreen`). Each layer only depends downward.
- **`BattlePanel`**: Unified battle menu replacing the old dual-`MenuBox` split. Supports static header text, configurable column count, vertical separator via `SeparatorAfterColumn`, d-pad navigation with separator-skipping, `MenuItem.IsSeparator` for horizontal dividers, and `OnCancel` callback.
- **HUD Panels**: Corner-mounted, no borders, matching semi-transparency across left and right panels.

### Quick Wins
| # | Feature | Effort | Impact |
|---|---------|--------|--------|
| 1 | **Non-uniform column widths** in BattlePanel | ~10 lines | Fix fight menu proportions |
| 2 | **PP display** on highlighted move | ~5 lines in BattlePanel.Draw | Player info |
| 3 | **Move type color** on move labels | ~5 lines | Visual clarity |
| 4 | **Proper party sprite system** | ~30 lines (texture dict + draw call) | PartyScreen prerequisite |
| 5 | **Centralized color palette** in UITheme | ~15 lines | Consistent styling across all screens |
