# 12 - Dynamic Battle Backgrounds & Encounter Hitbox — Lessons Learned & Handoff

**Date:** 2026-02-15
**Sprint:** Dynamic Battle Backgrounds, Encounter Detection Refinement, BG Editor Tool

---

## 1. What We Accomplished

### Dynamic Battle Background System (Complete)

Replaced the hardcoded single-background battle rendering with a multi-background system that selects 3D scenes based on encounter type.

**New files:**
- `Core/Battle/BattleBackground.cs` — Enum: `Grass`, `TallGrass`, `Cave`, `Dark`
- `Core/Battle/BattleBackgroundResolver.cs` — Maps tile `OverlayBehavior` strings to enum values

**Modified files:**
- `Game1.cs` — Dictionary-based model loading with cache, `AlphaTestEffect` rendering, active scene selection in `EnterBattle()`

**Resolver mapping:**
| Overlay Behavior | Background |
|---|---|
| `wild_encounter`, `rare_encounter`, `double_encounter` | TallGrass |
| `cave_encounter` | Cave |
| `water_encounter`, `surf_encounter`, `fishing`, `fire_encounter` | Dark |
| `headbutt`, default/unknown | Grass |

**Model cache pattern:** Shared assets (e.g., `Grass/Grass.dae` used by both Grass and TallGrass sets) are loaded once and referenced by both dictionary entries.

### AlphaTestEffect Fix for Transparency (Complete)

**Problem:** TallGrass platform models (`.dae`) include leaf/shadow textures with alpha transparency. Using `BasicEffect`, transparent pixels wrote to the depth buffer, causing ground geometry behind them to be occluded — appearing as visual "distortion."

**Root cause:** `BasicEffect` doesn't discard transparent fragments before the depth test. Semi-transparent leaf pixels at alpha ~0 still wrote depth, blocking everything behind.

**Fix:** Replaced `BasicEffect` with `AlphaTestEffect`:
```csharp
_battleEffect = new AlphaTestEffect(GraphicsDevice)
{
    VertexColorEnabled = false,
    Alpha = 1f,
    ReferenceAlpha = 128,        // Discard pixels with alpha < 128
    AlphaFunction = CompareFunction.GreaterEqual,
};
```
Model `Draw()` method changed to accept `Effect` base class, setting texture via `effect.Parameters["Texture"]?.SetValue()`.

### Battle Background Editor Tool (Partial)

- `src/Starfield.BgEditor/` — React + Vite + Three.js web tool
- Loads `.dae` Collada models and renders 3D preview
- Color/hue/saturation sliders for texture adjustment
- **Status:** Scene loading works but viewport may show black for some models. `sceneService.ts` has uncommitted simplification changes.

### Encounter Hitbox Refinement (In Progress — Not Working)

Attempted to fix encounters triggering when the player is visually outside the flame sprite area.

**Changes made to `GameWorld.cs`:**
1. Removed y+1 "feet tile" check from `IsEncounterTile()` and `GetEncounterBehavior()` — now checks only the player's actual tile
2. Added center-of-tile detection: encounter only rolls when player position is within a sub-tile rectangle, not at tile boundary crossing
3. Current hitbox: `localX ∈ [0.15, 0.85]`, `localY ∈ [0.0, 0.5]`

**Status: Still not working correctly.** The user reports encounters feel off. This is the primary remaining work item.

---

## 2. What Work Remains

### Critical (Blocking)
1. **Fix encounter hitbox alignment** — Encounters trigger where the player doesn't visually see flames. The sub-tile hitbox approach is the right direction but the coordinates don't match player perception.
2. **Revert WorldRegistry spawn** — `WorldRegistry.cs` currently spawns on `test_map_b` (debug change). Must revert to `test_map_center` before shipping.

### Important
3. **BG Editor viewport** — `sceneService.ts` has uncommitted changes; 3D viewport may not render all model types correctly.
4. **Clean up debug artifacts** — Remove `encounter_log.txt` file-based logging if still present, remove any stale debug code.

### Future
5. **Pokemon sprite billboards** on platforms
6. **HP bars, stat displays** overlaying the 3D scene
7. **Battle turn logic** (speed, damage calc, type effectiveness)
8. **Encounter tables** per map/region (which Pokemon spawn where)

---

## 3. Prime Suspects — Where to Begin Debugging the Encounter Hitbox

### Suspect 1: Player Position vs. Visual Position Mismatch

The player sprite renders with visual offsets (jump height, animation centering), but `Player.X`/`Player.Y` are the raw float coordinates. The player's **perceived** position on screen may not match the coordinate used for encounter checks.

**Investigation:** Add a debug overlay that draws the player's `(X, Y)` as a dot and the encounter hitbox rectangle on the tile. This makes the mismatch visible.

### Suspect 2: The Flame Sprite Doesn't Visually Fill the Hitbox Area

Current flame rendering (TileRenderer.cs, lines 188-225):
```
tileSize = 16px, objSize = 8px (half tile)

Double layer:
  Sprite 1: (worldX, worldY, 8, 8)           → top-left quadrant
  Sprite 2: (worldX+8, worldY-4, 8, 8)       → top-right, 4px above tile

Single layer (at player feet):
  Sprite: (worldX+8, worldY, 8, 8)           → top-right quadrant only
```

The flames are 8x8 sprites in 16x16 tiles, drawn in the **upper portion** with the second sprite extending 4px above the tile boundary. The bottom half of the tile has no visible flame at all. But the encounter hitbox (Y 0.0–0.5) corresponds to the top half in tile coords — **which is the correct area**.

The issue might be that the player walks bottom-to-top through the tile. When `localY = 0.0`, the player is at the TOP of the tile (where flames are), but if the player entered from below, `localY` starts near 1.0 and decreases. They'd trigger at Y=0.5 which is the **middle** of the tile, not where they see flames.

**Key insight:** `TileY = Floor(Y)`. When walking UP (Y decreasing), the player crosses the tile boundary when Y goes from e.g. 6.01 → 5.99. At that moment `TileY` changes from 6 to 5, and `localY = 5.99 - 5 = 0.99`. As they continue upward, `localY` decreases toward 0.0. The hitbox `[0.0, 0.5]` means they must walk halfway through the tile before triggering. **This could feel wrong** — by the time `localY` reaches 0.5, they're visually IN the flames, but if the flames are in the upper 8px (localY 0.0–0.5), then localY=0.5 is the bottom edge of the flames. The player would trigger at exactly the flame boundary, which is correct.

**BUT** — entering from the RIGHT (localX starts at 0.0 and increases): the hitbox starts at 0.15, which means almost immediately. Entering from the LEFT (localX starts near 1.0): they never hit 0.85 because they'd change tiles at 1.0. **This is asymmetric and likely the core bug.**

### Suspect 3: `Floor()` Boundary Direction Asymmetry

When moving RIGHT: X goes from 4.9 → 5.0. `TileX` changes to 5, `localX = 0.0`. Player enters from left edge, localX increases. Hitbox starts at 0.15 — triggers quickly.

When moving LEFT: X goes from 5.0 → 4.99. `TileX` changes to 4, `localX = 0.99`. Player enters from right edge, localX decreases. Hitbox is `<= 0.85` which is immediately true. **Both ends are near-immediate!**

Wait — that means the hitbox is mostly working. The problem might be more subtle.

### Suspect 4: Camera Zoom Makes Small Offsets Feel Large

At 16px tiles with high camera zoom (viewport fills ~5 tiles vertically), even a 4px visual offset is significant. The flame double-layer second sprite is drawn at `worldY - halfTile/2 = worldY - 4`, meaning it renders 4px ABOVE the tile boundary into the tile above. The player visually sees "flames" in the tile above but that tile has no encounter overlay. This creates the perception of walking "in flames" without triggering encounters.

**Fix approach:** Either (a) stop drawing flames above their tile boundary, or (b) extend encounter detection to account for the visual bleed.

---

## 4. Step-by-Step Approach to Get Fully Working

### Phase 1: Visual Debug Overlay
1. Add a debug draw mode (toggled by a key, e.g., F3) that renders:
   - A colored rectangle on each encounter tile
   - The sub-tile hitbox area as a different colored rectangle
   - The player's actual `(X, Y)` position as a crosshair
   - The `TileX, TileY` grid position as text
2. Walk around Map B and visually verify the hitbox aligns with where flames appear
3. Screenshot/record the mismatch

### Phase 2: Fix the Visual/Detection Alignment
Based on Phase 1 findings, choose one approach:
- **Option A:** Adjust flame sprite positions to fill the full tile (user rejected once — try a different visual approach, like 3-4 smaller sprites spread across the tile)
- **Option B:** Adjust the encounter hitbox to match exactly where the sprites render (including the above-tile bleed)
- **Option C:** Render a transparent "encounter zone" indicator (like the original Pokemon games' rustling grass) that fills the full tile, making the hitbox feel natural

### Phase 3: Test All Entry Directions
Walk into flame tiles from all 4 directions and verify:
- Encounter triggers feel natural (not too early, not too late)
- No encounters on tiles without visible flames
- Encounters feel consistent regardless of approach direction

### Phase 4: Revert Debug Changes
1. Revert `WorldRegistry.cs` spawn to `test_map_center`
2. Remove debug overlay code (or leave behind F3 toggle for future use)
3. Remove file-based logging
4. Commit clean state

---

## 5. How to Start & Test

### Running the Game
```bash
cd D:\Projects\Starfield2026dotnet run --project src/Starfield/Starfield.csproj
```

**Current spawn:** Map B (fire level) — `WorldRegistry.cs` is set to `test_map_b` for testing.
**Normal spawn:** Revert to `test_map_center` in `WorldRegistry.Initialize()`.

**Controls:**
- Arrow keys / WASD: Move
- Shift: Run
- Space: Jump
- E / Enter: Interact
- Walk into flame tiles (121) to trigger encounters (1-in-10 chance)

### Running the BG Editor
```bash
cd D:\Projects\Starfield2026\src\Starfield.BgEditor
npm install   # first time
npm run dev   # starts on http://localhost:5173
```

### Testing Encounter Detection
1. Spawn on Map B (current default)
2. Walk into the flame cluster (rows 6-9, columns 3-10 in the map)
3. Verify encounters trigger only when visually inside flames
4. Test approach from all 4 directions

### Quick Battle Test
Set `DebugStartInBattle = true` in `Game1.cs` (line ~87) to skip overworld and launch directly into battle with Grass background. Useful for testing 3D rendering without walking around.

---

## 6. Known Issues & Strategies

### Issue 1: Encounter Hitbox Misaligned with Flame Visuals
**Symptom:** Player perceives encounters triggering on non-flame tiles.
**Root cause:** Combination of small sprite size (8x8 in 16x16 tile), above-tile bleed rendering, and sub-tile detection that may not match perceived player position.

**Strategy A — Debug overlay first:** Before changing any logic, add visual debug rendering to SEE the actual coordinates. The problem might be different than assumed.

**Strategy B — Tile-centered flame sprites:** Render flames as 3-4 small sprites distributed across the full tile area instead of 2 sprites clustered in the upper portion. This makes the visual match the full-tile hitbox naturally.

**Strategy C — Per-tile-type hitbox:** Add a `GetEncounterInsets(int tileId)` method (similar to `GetObjectCollisionInsets`) that returns custom hitbox dimensions per encounter tile type. Tall grass (72) might need different insets than flames (121).

**Strategy D — Pixel-perfect sprite bounds:** Calculate the actual bounding box of all sprites drawn for a flame tile in pixel coordinates, convert to tile-local coordinates, and use that as the hitbox. This guarantees visual = detection.

### Issue 2: BG Editor Viewport May Not Render
**Symptom:** Black viewport when loading certain `.dae` models.
**Strategy:** Check `sceneService.ts` for material/texture loading errors. Collada files reference textures by relative path — verify Three.js `ColladaLoader` resolves paths correctly relative to the `.dae` file location.

### Issue 3: MonoGame Logging Limitations
**Symptom:** `Console.WriteLine` and `Debug.WriteLine` produce no visible output.
**Strategy:** Use `File.AppendAllText` to a log file in the executable directory. Or attach a `TraceListener` that writes to file. For real-time tailing: `Get-Content -Wait encounter_log.txt` in PowerShell.

### Issue 4: TallGrass Platform Visual Density
**Symptom:** TallGrass platforms look visually different (higher, denser) than expected after AlphaTestEffect fix.
**Strategy:** This is correct behavior — TallGrass `.dae` models have leaf/grass tufts on the platforms that were previously invisible due to the transparency bug. If the density is too high, adjust in the model files or use the BG Editor to tweak.

---

## 7. Architecture & Features Reference

### Battle Background Pipeline
```
Tile Overlay (121=Flames)
  → TileRegistry.GetTile(121).OverlayBehavior = "fire_encounter"
  → BattleBackgroundResolver.FromOverlayBehavior("fire_encounter") = Dark
  → Game1._battleScenes[Dark] = (Dark.dae, DarkPlatform.dae, DarkPlatform.dae)
  → DrawBattle3D() renders with AlphaTestEffect
```

### 3D Rendering Stack
```
AssimpNet (Collada .dae) → BattleModelLoader → BattleModelData (VB+IB+Texture per mesh)
  → AlphaTestEffect (ref alpha 128, PointClamp sampler, CullNone)
  → Camera: yaw=-22°, pitch=13°, FOV=26°, near=1, far=512
  → Platforms: ally at (0, -0.2, 3), foe at (0, -0.2, -15)
```

### Encounter Detection Pipeline
```
Player.Update() → position changes → TileX/TileY change detected
  → IsEncounterTile(x, y) checks overlay then base for TileCategory.Encounter
  → If encounter tile: set _encounterCheckPending = true
  → Each frame: check if player localX/localY within sub-tile hitbox
  → If in hitbox: roll 1-in-10 chance → BeginBattleTransition()
  → BattleBackgroundResolver selects 3D scene → camera animation → battle UI
```

### Asset Structure
```
Assets/BattleBG/
  ├── Grass/          (sky + field background)
  ├── Cave/           (sky + field background)
  ├── Dark/           (sky + field background)
  ├── PlatformGrassAlly/    PlatformGrassFoe/
  ├── PlatformTallGrassAlly/ PlatformTallGrassFoe/
  ├── PlatformCaveAlly/      PlatformCaveFoe/
  ├── PlatformDark/          (single model for both)
  └── PlatformRotation/      (unused — future VS sequences?)
```

### Quick Wins
1. **Debug overlay (F3 toggle)** — Draw tile grid, encounter hitboxes, and player position. Invaluable for all future tile-based debugging. ~30 min implementation.
2. **Encounter hitbox config** — Move the hardcoded `0.15/0.85/0.0/0.5` values into a method like `GetEncounterHitbox(int tileId)` so different tile types can have different trigger zones. ~10 min.
3. **Flame sprite fill** — Add 2 more small flame sprites to cover the bottom half of the tile, making the visual fill match the tile. ~15 min.
4. **BG Editor auto-camera** — Match the editor camera to the in-game camera (yaw=-22, pitch=13, FOV=26) for WYSIWYG preview. Currently uses a generic orbit camera.

---

## Uncommitted Changes Summary

| File | Change | Notes |
|---|---|---|
| `GameWorld.cs` | Center-of-tile encounter detection, removed y+1 check | Core encounter fix — still needs tuning |
| `WorldRegistry.cs` | Spawn on `test_map_b` | **Must revert** to `test_map_center` |
| `TestMapB.g.cs` | Whitespace cleanup, stale comment fix | Minor |
| `sceneService.ts` | BG Editor simplification | Unrelated to encounter issue |
