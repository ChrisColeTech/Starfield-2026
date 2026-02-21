# 13 — Battle System & UI: Consolidated Reference

**Date:** 2026-02-21
**Source:** Merged from POC docs `08-BATTLE-SYSTEM-DESIGN.md` and `11-UI-SYSTEM-LESSONS.md`
**Project:** Starfield-2026

---

## 1. Work Completed

### Battle Architecture
- **BattleScreen3D** — Thin orchestrator delegating to `BattleCamera`, `BattleSceneRenderer`, and `BattleUIManager`. Manages battle entry, state machine phases, and exit transitions.
- **BattleTurnManager** — Full turn-order resolution. Compares speed stats, queues attacker/defender sequence, applies damage via `DamageCalc`, fires animation callbacks (`OnAllyAttack`, `OnFoeAttack`, `OnAllyFaint`, `OnFoeFaint`, `OnReturnToIdle`), and handles faint detection with victory/defeat messaging.
- **BattlePokemon / PartyPokemon** — Data structures for in-battle and party-level Pokemon with HP, stats, moves (4-slot with PP tracking), level, nickname, gender, status, held item, and EXP tracking. `BattlePokemon.FromParty()` converts party data to battle-ready state.
- **MoveRegistry / PokemonRegistry** — Static registries for move definitions (name, power, accuracy, type, PP) and species data.
- **DamageCalc** — Simplified Gen 1 formula: `((2*Level/5+2) * Power * Atk / Def) / 50 + 2`, with 85–100% random multiplier and type effectiveness.
- **TypeChart** — Type effectiveness table (super effective 2x, not very effective 0.5x, immune 0x).
- **BattleCamera** — Intro zoom on foe, smooth ease-out zoom to default position, reset on battle entry.
- **BattleSceneRenderer** — 3D scene with DAE model loading (ally/foe), placeholder cubes, background rendering, deploy/clear logic.
- **Encounter Detection** — Random chance roll on encounter tiles triggers battle transition via fade system.
- **Fade Transitions** — Reuses the existing overworld fade-to-black system for battle entry and exit.
- **Game State Branching** — `GameMode.Battle` routes `Update()` and `Draw()` to the battle subsystem, bypassing overworld rendering.

### UI System
- **MessageBox** — Typewriter text with message queue, confirm-to-skip/advance, `OnFinished` callback. Properly resets all state (including callbacks and blink timer) on `Clear()`.
- **MenuBox** — Grid/list menu with arrow key + mouse navigation. `MenuItem` with label, enabled flag, and `Action? OnConfirm`. Supports 2-column grid (battle) and single-column list layouts.
- **UIStyle** — Centralized rendering utilities: `DrawShadowedText`, `DrawHPBar`, `DrawEXPBar`, `DrawTripleGradient`, `DrawRightArrow`, `DrawDownArrow`.
- **Arrow Cursors** — Pixel-drawn right-pointing triangle for menu selection, down-pointing triangle for message advance. No font character hacks.
- **PixelFont** — Custom 5×7 procedural glyph renderer with swappable glyph dictionaries (`CurrentGlyphs`), dynamic `Scale`, `MeasureWidth()`, and `CharHeight`/`CharWidth` properties. Replaces both the old KermFont system and MonoGame SpriteFont.
- **UITheme** — Global static class for font scale derivation (`GetFontScale`), shared colors (`MenuBackground`), and centralized style management.
- **IScreenOverlay** — Interface for full-screen overlays (PartyScreen, BagScreen, CharacterSelectScreen) pushed onto a stack in BattleUIManager.
- **BattleInfoBar** — Procedural HP bars, name/level labels, gender symbols, status abbreviations, and EXP bar for ally/foe panels.

### Bug Fixes Applied
- **MessageBox state leak** — `Clear()` now resets `OnFinished` callback and `_blinkTimer` to prevent stale callbacks across battles.
- **Mouse click reliability** — `MenuBox.Update()` guards mouse checks with `LastBounds.Contains()` and separates click/hover paths.
- **Encounter dead zones** — Encounter checks now cover both head and feet tile positions to match the renderer's grass detection.

---

## 2. Battle Flow — Full Specification

### Phase Sequence
```
EnterBattle → Intro → ZoomOut → PlayerChoice → (sub-menu) → ExecuteTurn → CheckFaint → (loop or end) → FadeOut → Overworld
```

### Phase Details

| Phase | What Happens | Input | Advances To |
|-------|-------------|-------|-------------|
| **Intro** | "Wild POKEMON appeared!" typewriter text, camera zoomed on foe | Confirm skips text | ZoomOut |
| **ZoomOut** | Camera ease-out lerp to default battle view | Automatic | SendOut |
| **SendOut** | "Go, POKEMON!" message, ally model deploys | Confirm | PlayerChoice |
| **PlayerChoice** | "What will POKEMON do?" — Main menu appears: **Fight / Bag / Pokemon / Run** (2×2 grid) | Arrow keys + Confirm | Sub-menu |
| **Fight** | Move list (up to 4 moves, 2×2 grid) with PP display. Back button returns to main menu | Arrow keys + Confirm/Cancel | ExecuteTurn |
| **Bag** | Full-screen overlay — item pouch tabs, 4×5 item grid, pagination, cancel button | Navigation + Confirm/Cancel | PlayerChoice |
| **Pokemon** | Full-screen overlay — 2×3 party card grid, HP bars, switch/summary options | Navigation + Confirm/Cancel | PlayerChoice or SwitchIn |
| **Run** | "You got away safely!" message | Automatic | FadeOut |
| **ExecuteTurn** | Speed comparison → faster attacks first → damage calc → HP bar animates → message → slower attacks → check | Automatic | CheckFaint |
| **CheckFaint** | If foe HP ≤ 0: "Foe fainted!" → EXP award → Victory. If ally HP ≤ 0: prompt switch or Defeat | Automatic | Victory/Defeat/Switch |
| **Victory** | "You won!" message | Confirm | FadeOut |
| **Defeat** | "You blacked out!" message | Confirm | FadeOut |
| **FadeOut** | Fade to black → `CleanupBattle()` → restore overworld | Automatic | Overworld |

### Menu Content (Fight Sub-Menu)

```
┌──────────────────────────────────────────┬─────────────────────┐
│  > Scratch        Growl                  │  Back               │
│    Ember          Leer                   │  Mega               │
│                                          │  Power              │
│                              PP: 25/25   │                     │
└──────────────────────────────────────────┴─────────────────────┘
   Move panel (left 2/3)                     Action panel (right 1/3)
```

- Moves shown in 2×2 grid with arrow navigation
- PP counter for highlighted move
- Disabled moves (PP = 0) shown but unselectable
- Back/Mega/Power buttons on the right side panel
- Cancel returns to the main menu

---

## 3. Lessons Learned

### State Machine Discipline
Every `MessageBox.Clear()` or battle exit must reset **all** state — including callbacks, timers, and phase flags. Partial resets cause stale callbacks to fire in subsequent battles (e.g., camera zoom re-triggering).

### UI Coordinate Consistency
When a visual effect is tied to a tile position, the game logic check must use the **same** tile coordinate. The renderer drew grass at `TileY + 1` (feet) but encounter checks used `TileY` (head). Always verify that detection and rendering agree on positions.

### Mouse Hit Testing Timing
`MenuBox.GetItemRect()` depends on `LastBounds` set during `Draw()`. Since `Update()` runs before `Draw()`, the first frame a menu appears has stale bounds. Guard all mouse checks with `LastBounds.Contains()`.

### Fully Procedural UI
No image assets needed for the UI — all windows, HP bars, menus, arrows, and text are drawn as colored rectangles plus PixelFont glyphs. This makes theming trivial (just change `UITheme` values) at the cost of more draw calls.

### Font Evolution Path
KermFont (binary bitmap) → SpriteFont (MonoGame native) → PixelFont (procedural 5×7). The final choice trades typographic flexibility for perfect pixel consistency across all resolutions and zero asset dependencies. New glyph sets can be hot-swapped via `PixelFont.CurrentGlyphs`.

### Separation of Concerns
The three-class split (`BattleScreen3D` orchestrator → `BattleSceneRenderer` for 3D → `BattleUIManager` for 2D) keeps rendering, logic, and UI independent. This allowed font system rewrites without touching 3D code.

---

## 4. Missing Features — Not Yet Implemented

### Battle Logic
- [ ] **Type effectiveness messages** — "It's super effective!" / "It's not very effective..." / "It doesn't affect..."
- [ ] **Accuracy/evasion checks** — Moves can currently never miss
- [ ] **Critical hits** — ~6.25% chance, 1.5× damage, message
- [ ] **Status conditions** — Poison, Burn, Paralysis, Sleep, Freeze (damage over time, speed reduction, skip turn, thaw chance)
- [ ] **Stat stages** — Attack/Defense/Speed/etc. modifiers from moves like Growl, Leer, Tail Whip (+1/−1 stages, max ±6)
- [ ] **Multi-hit moves** — Moves that hit 2–5 times (e.g., Fury Swipes)
- [ ] **STAB (Same Type Attack Bonus)** — 1.5× damage when move type matches attacker type
- [ ] **Non-damaging move effects** — Growl reduces Attack, Leer reduces Defense, Leech Seed drains HP, etc.
- [ ] **Wild Pokemon AI** — Currently undefined; foe should randomly select from available moves
- [ ] **Flee calculation** — Speed-based escape chance (currently always succeeds)
- [ ] **Catch mechanics** — Pokeball throw, catch rate formula, shake animations

### Party & Progression
- [ ] **Experience gain** — EXP formula on foe faint, EXP bar fill animation
- [ ] **Level-up** — Stat recalculation, learn new move prompts, evolution checks
- [ ] **Party switching** — In-battle switch (takes a turn), forced switch on faint
- [ ] **Multiple Pokemon battles** — Player party of 6, battle continues until all faint
- [ ] **Held items** — In-battle effects (berries heal, etc.)

### Items (Bag Sub-Menu)
- [ ] **Potions** — Heal HP (Potion: 20, Super Potion: 50, Hyper Potion: 200)
- [ ] **Status heals** — Antidote, Paralyze Heal, Awakening, Burn Heal, Ice Heal, Full Heal
- [ ] **Pokeballs** — Pokeball, Great Ball, Ultra Ball, Master Ball with catch formulas
- [ ] **Battle items** — X Attack, X Defense, X Speed (temporary stat boosts)
- [ ] **Item usage flow** — Select item → apply to Pokemon (if applicable) → consume turn

### UI & Presentation
- [ ] **Move type indicator** — Show type icon/label next to each move in the fight menu
- [ ] **Damage number animation** — Floating damage text or HP bar flash
- [ ] **Faint animation** — Model plays death animation, then fades/drops
- [ ] **Battle entry transition** — Flash/swirl effect before fade (old engine uses liquid wipe shader)
- [ ] **Pokemon cry/sound** — Audio on encounter and faint
- [ ] **Battle music** — Transition from overworld BGM to battle BGM, restore on exit

### Overworld Integration
- [ ] **Encounter rate modifiers** — Abilities like Illuminate (2×), Stench (0.5×)
- [ ] **Repel items** — Suppress encounters for N steps
- [ ] **Shaking grass** — Visual warning before encounter triggers
- [ ] **Trainer battles** — NPC line-of-sight triggers, custom teams, dialogue, rewards
- [ ] **Area encounter tables** — Different Pokemon per route/map zone with level ranges
- [ ] **Roaming Pokemon** — Pokemon that move between routes

### Overworld Pause Menu
- [ ] **Start menu** — Pokemon, Bag, Pokedex, Save, Options (vertical list using MenuBox)
- [ ] **Trigger** — ESC or Start button opens/closes
- [ ] **Save system** — Serialize party, inventory, position, badges to disk

---

## 5. Quick Wins

These can each be done in a single session with minimal risk:

| # | Feature | Effort | Impact |
|---|---------|--------|--------|
| 1 | **STAB bonus** | ~5 lines in `DamageCalc` — check if `move.Type == attacker.Type`, multiply by 1.5 | Correct damage feel |
| 2 | **Type effectiveness messages** | ~10 lines in `BattleTurnManager` after damage — check multiplier, queue "super effective" / "not very effective" message | Player feedback |
| 3 | **Critical hits** | ~8 lines in `DamageCalc` — `Random.Next(16) == 0` → 1.5× damage, return a flag → show "Critical hit!" message | Battle variety |
| 4 | **Wild Pokemon AI** | ~5 lines — foe randomly picks from moves with PP > 0 | Foe actually fights back |
| 5 | **Stat stage moves** | ~15 lines — track `int[] StatStages` on `BattlePokemon`, apply on Growl/Leer/etc., multiply Attack/Defense by stage modifier | Non-damaging moves work |
| 6 | **Accuracy checks** | ~5 lines in `DamageCalc` — `Random.Next(100) < move.Accuracy` else miss, show "Attack missed!" | Battle realism |
| 7 | **Flee speed check** | ~5 lines in `TryRun` — compare ally/foe speed, calculate escape odds, increment attempt counter | Escape tension |
| 8 | **Potion usage** | ~20 lines — detect "Potion" in Bag, heal 20 HP, consume item, consume turn | First usable item |
| 9 | **EXP gain on victory** | ~15 lines — calculate EXP from foe level/species, add to ally, animate EXP bar | Progression loop |
| 10 | **Overworld pause menu** | ~30 lines — ESC triggers a `MenuBox` overlay with Pokemon/Bag/Save/Options, reuses existing MenuBox class | Core game feature |

---

## 6. Architecture Reference

### Current File Layout
```
Starfield2026.Core/
├── Battle/
│   ├── BattleScreen3D.cs       — Orchestrator (entry, phases, exit)
│   ├── BattleUIManager.cs      — 2D UI stack (menus, messages, overlays)
│   ├── BattleCamera.cs         — Intro zoom, smooth follow
│   ├── BattleSceneRenderer.cs  — 3D scene (models, backgrounds)
│   ├── BattleTurnManager.cs    — Turn order, damage, faint detection
│   ├── BattlePokemon.cs        — In-battle Pokemon state
│   ├── BattleInfoBar.cs        — HP/name/level/status panels
│   ├── DamageCalc.cs           — Damage formula
│   └── TypeChart.cs            — Type effectiveness table
├── Pokemon/
│   ├── PartyPokemon.cs         — Persistent party data
│   ├── PokemonRegistry.cs      — Species definitions
│   ├── MoveRegistry.cs         — Move definitions
│   └── Gender.cs
├── UI/
│   ├── UIStyle.cs              — Shared drawing utilities
│   ├── UITheme.cs              — Global font/color/scale settings
│   ├── MenuBox.cs              — Grid/list menu component
│   ├── MessageBox.cs           — Typewriter text component
│   ├── IScreenOverlay.cs       — Full-screen overlay interface
│   └── Screens/
│       ├── PartyScreen.cs      — Party management overlay
│       ├── BagScreen.cs        — Inventory overlay
│       └── CharacterSelectScreen.cs — Model picker overlay
├── Rendering/
│   └── PixelFont.cs            — 5×7 procedural font with swappable glyphs
└── Items/
    ├── PlayerInventory.cs
    └── ItemRegistry.cs
```

### Battle Entry Call Chain
```
Starfield2026Game.Update()
  → input.ConfirmPressed on encounter tile
  → _battleScreen.EnterBattle(ally, foe, bg)
    → BattleUIManager.ShowMessage("Wild POKEMON appeared!")
    → BattleCamera.Reset() + StartZoom()
    → BattleSceneRenderer.DeployFoe()

Starfield2026Game.Draw()
  → if GameMode.Battle:
    → _battleScreen.Draw3DScene(device)   // 3D models + background
    → _battleScreen.DrawUI(fontScale, w, h) // 2D menus + info bars
```

---

## 7. Testing

```bash
cd D:\Projects\Starfield-2026\src\Starfield2026.3D
dotnet build    # Should produce 0 errors, 0 warnings
dotnet run      # Game launches

# Battle test: set DebugStartInBattle = true in Starfield2026Game.cs
# Overworld test: walk into encounter grass
```

### Verify
- Battle opens with typewriter "Wild POKEMON appeared!"
- Confirm skips/advances text, camera zooms out
- "Go, POKEMON!" → ally deploys → menu appears
- Fight/Bag/Pokemon/Run in 2×2 grid
- Arrow keys navigate, cyan arrow highlights selection
- Fight → 4 moves with PP, arrow navigation
- Run exits battle with fade transition
- Overlays (Party, Bag) open and close correctly
