# 09 — Lessons Learned & Handoff: OverworldGrid, Warp System & Camera Issues

**Date:** 2026-02-20  
**Session Focus:** Recreating the original wireframe grid as a MapDefinition, warp transitions, camera stability  
**Status:** Grid map renders correctly with wireframe + placed tiles. Warps function. **Camera spin on warp transitions remains UNRESOLVED.**

---

## 1. What We Accomplished

### New Files

| File | Purpose |
|------|---------|
| `Core/Maps/Generated/OverworldGrid.g.cs` | **80×80 open wireframe grid** — the original cyan line grid, now backed by a `MapDefinition` for warps/encounters. No walls. Door at center (40,40) warps to HomeBaseCenter |
| `Core/Maps/Generated/GrassField.g.cs` | **32×32 grass field** — walled outdoor area with grass, trees, rocks, encounter zones, door to HomeBaseCenter. Renamed from the original "OverworldGrid" |

### Modified Files

| File | Changes |
|------|---------|
| `Core/Screens/OverworldScreen.cs` | Dual render path: `GridRenderer` (wireframe) + `MapRenderer3D(skipTileId: 5)` for OverworldGrid; `MapRenderer3D` only for other maps. `LoadMap` sets `WorldHalfSize` dynamically. `SetPlayerPosition` calls `Camera3D.SnapToTarget()`. Warp detection uses `Math.Floor` + `Math.Clamp` |
| `Core/Rendering/Camera3D.cs` | Added `SnapToTarget()` method to instantly teleport camera (attempted camera spin fix) |
| `Core/Rendering/MapRenderer3D.cs` | Added `skipTileId` parameter to `Draw()` — skips rendering a specific background tile |
| `3D/Starfield2026Game.cs` | Default map → `overworld_grid`. Reflection-based map auto-registration |
| `Core/Save/PlayerProfile.cs` | Default `CurrentMapId` → `"overworld_grid"` |

### Rendering Architecture

```
OverworldScreen.Draw():
  if map is OverworldGrid:
    1. GridRenderer (cyan wireframe lines, infinite scroll)
    2. MapRenderer3D(skipTileId: 5) → renders ONLY placed tiles (doors, encounters)
  else:
    MapRenderer3D → renders all tiles as colored 3D geometry
```

### Map Inventory

| Map | Size | Type | Warps |
|-----|------|------|-------|
| `overworld_grid` | 80×80 | Wireframe grid (default) | Door at (40,40) → `home_base_center` |
| `grass_field` | 32×32 | Tile-based outdoor | Door at (16,31) → `home_base_center` |
| `home_base_center` | 16×16 | Tile-based indoor | Door at (7,15) → `home_base_hangar`, Door at (8,0) → `overworld_grid` |
| `home_base_hangar` | 16×16 | Tile-based indoor | Door at (7,0) → `home_base_center` |

---

## 2. What Work Remains

### Critical — UNRESOLVED BUGS

> [!CAUTION]
> These two bugs are the highest priority for the next session.

#### Bug 1: Camera Spins/Snaps on Warp Transitions

**Symptom:** Every time the player walks through a warp tile, the camera rapidly spins or snaps to a different angle instead of smoothly following through the transition.

**What Was Tried (All Failed):**
1. ✅ Added `_warpCooldown = 0.5f` to prevent warp re-triggering — helps with re-trigger but doesn't fix camera
2. ✅ Added `Camera3D.SnapToTarget()` called in `SetPlayerPosition()` — camera still spins
3. ✅ Called `_camera.Follow(newPos)` before `SnapToTarget()` — still spins

**Root Cause Analysis:**
The camera spin has multiple contributing factors that interact:

1. **`PlayerController.Initialize()` resets `Yaw = 0`** — When the player warps, their facing direction resets regardless of which direction they were facing. This causes a sudden rotation.

2. **`Camera3D` smooth follow fights the teleport** — Even with `SnapToTarget()`, the camera's `Yaw`/`Pitch` orbit angles aren't reset. The camera position snaps but the orbit angle stays the same, creating a mismatch between where the camera IS and where it SHOULD be relative to the player.

3. **`OverworldScreen.Update()` calls `_camera.Follow()` every frame** which updates `_followTarget`. On the frame after a warp, the camera lerps from its snapped position toward the new follow target with the orbit offset, causing the visual sweep.

4. **Frame ordering** — `HandleMapTransition` is called from an event handler during `Update()`, but the camera hasn't updated its matrices yet. The next frame draws with stale camera state before the new matrices are calculated.

**Recommended Strategies for Next Session:**

1. **Preserve player Yaw on warp** — Don't reset `Yaw = 0` in `Initialize()`. Add an overload: `Initialize(Vector3 pos, float yaw)` that preserves facing direction. This alone may fix the spin.

2. **Reset camera orbit to match player Yaw** — After warp, set `_camera.Yaw = _player.Yaw` so the camera is directly behind the player. Combined with `SnapToTarget()`, this should eliminate the orbit mismatch.

3. **Defer warp by one frame** — Instead of executing the warp immediately in the event handler, set a flag + store the warp. On the next frame's `Update()`, execute the warp BEFORE movement/camera. This ensures clean frame ordering.

4. **Full camera reset method** — Add `Camera3D.Reset(Vector3 target, float yaw)` that sets `_currentTarget = _followTarget = target; Yaw = yaw;` in one call. Use this on every warp instead of piecemeal Follow+Snap.

#### Bug 2: Warp Detection at Map Boundaries

**Symptom:** Warp tiles on the edge of a map (row 0 or column 0) may not trigger because the tile coordinate conversion can miss boundary values.

**Status:** Partially fixed with `Math.Floor` + `Math.Clamp`. Needs verification.

### Important

3. **Tile collision** — Player walks through all walls. No walkability check exists.
4. **PlayerSpawn positioning** — Player always starts at (0, 0.75, 0). Should scan for tile ID 116.
5. **Warp fade transition** — Map switches are instant cuts. Need fade-to-black effect.

---

## 3. Optimizations — Prime Suspects

1. **Coordinate conversion duplication** — Magic number `2f` (tile scale) appears in 4+ places. Add `MapDefinition.WorldToTile()` / `TileToWorld()` methods.

2. **MapRenderer3D per-frame geometry** — Builds vertex arrays every frame. Cache in `VertexBuffer` at `LoadMap()` time. ~100x fewer draw calls.

3. **`PlayerController.Initialize()` resets too much state** — Resets `Yaw`, `IsRunning`, `IsGrounded` on every warp. Should only reset position. Split into `SetPosition()` vs `FullReset()`.

4. **Warp cooldown is global** — Blocks ALL warp detection for 0.5s. Should be position-based: only block the departure tile.

---

## 4. Step-by-Step: Getting the App Fully Working

```powershell
# Prerequisites: .NET SDK 9.0.203+

# Clean start (delete old save)
del "D:\Projects\Starfield-2026\src\Starfield2026.3D\bin\Debug\net9.0\starfield2026.db"

# Build & run
cd D:\Projects\Starfield-2026
dotnet build src\Starfield2026.sln
dotnet run --project src\Starfield2026.3D
```

### Verify
1. ESC to Overworld → cyan wireframe grid, brown door cube visible at center
2. Walk into door → warps to HomeBaseCenter (**camera will spin — known bug**)
3. Walk to bottom door → warps to Hangar
4. Walk to top door in Hangar → warps back to Center
5. Close game → restart → spawns on last map

---

## 5. Known Issues & Strategies

See Section 2 for the two critical bugs and their strategies.

### Additional Issues

| Issue | Impact | Strategy |
|-------|--------|----------|
| No tile collision | Player walks through walls | Pre-movement tile check or grid-lock movement |
| WorldHalfSize = 500 on grid | Player can walk very far from map data | Implement proper tile collision instead |
| Blue zone under grid (FIXED) | TechFloor rendered as solid quads | Fixed with `skipTileId: 5` parameter |
| Invisible wall (FIXED) | WorldHalfSize clamped to 80 | Fixed by setting 500 for wireframe grids |

---

## 6. Architecture

### Wireframe Grid Rendering
```
OverworldGrid (MapDefinition, 80×80)
  ├─ Background tile: TechFloor (ID 5) — NOT rendered (skipped by MapRenderer3D)
  ├─ Visual floor: GridRenderer (cyan wireframe lines, infinite scroll)
  ├─ Placed tiles: Rendered by MapRenderer3D on top of wireframe
  │   ├─ Door (ID 32, brown, Height: 2f) — visible as cube
  │   ├─ NebulaZone (ID 72, green) — encounter zones
  │   └─ PlayerSpawn (ID 116, gold) — spawn marker
  └─ Warps: Defined in MapDefinition, detected by OverworldScreen.Update()
```

### Key Files for Camera Bug Fix

| File | What to Change |
|------|----------------|
| `Core/Controllers/PlayerController.cs:42-49` | `Initialize()` resets `Yaw = 0` — preserve it |
| `Core/Rendering/Camera3D.cs:10` | `Yaw` property — should sync with player on warp |
| `Core/Screens/OverworldScreen.cs:113-120` | `SetPlayerPosition()` — needs full camera reset |
| `3D/Starfield2026Game.cs:300-330` | `HandleMapTransition()` — consider deferred execution |

### Quick Wins

1. **Fix camera spin** — Add `PlayerController.SetPosition(Vector3 pos)` that only changes position, not yaw. ~5 min.
2. **Fix camera orbit** — Set `_camera.Yaw = _player.Yaw` in `SetPlayerPosition`. ~2 min.
3. **Tile collision** — Check `TileRegistry.GetTile(targetTile).Walkable` before movement. ~30 min.
4. **Warp fade** — Reuse `_transitionAlpha` fade-to-black for map warps. ~20 min.

---

## 7. Key Lessons

1. **Don't overwrite the visual identity.** The original cyan wireframe grid was the game's signature look. Replacing it with green grass tiles lost that identity. Always preserve the original visual when upgrading the data layer.

2. **Background tiles must be invisible on wireframe maps.** The `skipTileId` parameter on `MapRenderer3D.Draw()` is the clean solution — any map can specify which tile is its transparent background.

3. **Camera state has multiple components that must all reset atomically.** Snapping `_currentTarget` alone isn't enough — `Yaw`, `Pitch`, and the player's `Yaw` must all agree, or the camera orbits to the wrong position.

4. **`Initialize()` methods that reset too many fields are dangerous.** `PlayerController.Initialize()` resets position, yaw, running state, and grounded state. On a warp, only position should change. Overloaded methods with minimal reset are safer.

5. **`Math.Floor` is essential for tile coordinates.** C# `(int)` cast truncates toward zero, which gives wrong results for negative coordinates. `Math.Floor` always rounds toward negative infinity.
