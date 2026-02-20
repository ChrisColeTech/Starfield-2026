# Starfield-2026 Handoff & Lessons Learned

## 1. What We Accomplished

### Overworld Screen
- Player movement with walk (12u/s) and run (22u/s) speeds
- Run toggle (Shift key) - toggles run mode on/off
- Jump system (Space or Alt) - works with diagonal movement
- Jetpack/hover system:
  - Press jump in air to activate hover (costs 1 blue coin/boost)
  - Hover lasts 10 seconds
  - Press jump again while hovering to rise (if boosts available) or descend
  - Hold jump while hovering to descend
  - Bob animation while hovering
- Camera zooms out when running AND moving, zooms back in when idle or run disabled
- Coins spawn only when player moving (speed > 2f), clamped to map bounds

### Unified Game State
- Single BoostSystem shared across all screens (overworld, driving, space)
- Boosts persist via GameState -> GameDatabase
- Health unified across all screens
- Ammo unified across screens
- Coins collected persist

### HUD
- Health bar (top-right)
- Coins display (top-right)
- Boosts display (top-left, blue, when > 0)
- Speedometer (bottom-left, driving/space screens)

### Input System
- Jump: Space, Alt, or C (C added as fallback for keyboard ghosting)
- Run toggle: Shift press toggles
- Camera controls: Q/E (yaw), R/F (pitch), Z/X or mouse scroll (zoom)

### UI and Transitions
- ScreenManager extended to support same-screen transitions via `midFadeAction` lambda callbacks.
- Added crossfade alpha routing from `ScreenManager` straight into `HUDRenderer` for seamless RPG-style screen blending.
- Ensured UI elements (like Overworld Boost=0) persist correctly on screen even when empty.

### Battle Screen Port
- Reconstructed `KermFont` to load binary font atlases and draw them as virtual-resolution MonoGame quads.
- Recovered the `gamedata.db` SQLite database and embedded it into the build output path.
- Integrated Assimp-net to load Collada (`.dae`) trainer models and background stages, hooking them to `BattleScreen3D`.
- Unified the `PokemonGreen` input pipelines (`MenuBox.Update`) into the centralized `InputSnapshot` and `BattleMenuBox` components to allow the user to properly escape the battle.

---

## 2. What Work Remains

### ⚠️ Overworld Files DELETED — Need Full Rebuild
- `OrbitCamera.cs`, `PlayerController.cs`, and `OverworldScreen.cs` were deleted after multiple failed rebuild attempts
- All three files need to be recreated from scratch

### CRITICAL: Coordinate System Reference (from working DrivingScreen)
The following is verified working in DrivingScreen + ChaseCamera + VehicleController:
- **Forward direction**: `(sin(Yaw), 0, cos(Yaw))` — at Yaw=0, forward = +Z
- **W key** → MoveZ = +1, **S** → MoveZ = -1, **A** → MoveX = -1, **D** → MoveX = +1
- **ChaseCamera behind-player offset**: `(-sin(yaw)*dist, height, -cos(yaw)*dist)` — note the NEGATIVE signs
- **ChaseCamera look-ahead**: `(+sin(yaw)*lookAhead, 1, +cos(yaw)*lookAhead)` — POSITIVE signs
- At Yaw=0: camera at (0, h, -dist) = behind in -Z, looking at (0, 1, +ahead) = +Z = forward
- The previous OrbitCamera used `+sin/+cos` for its offset (opposite of ChaseCamera), placing the camera IN FRONT of the player at Yaw=0 instead of behind. This caused all direction inversions.

### Camera Requirements for Overworld
- **Camera-relative movement**: Press W = walk in the direction the camera is looking (like Mario 64, Zelda)
- Character smoothly turns to face movement direction
- Camera follows player position with smooth lag
- Camera does NOT need to auto-rotate behind player (player uses Q/E to rotate camera)
- Camera zooms out when running, zooms back in when idle

### Overworld Polish
- Encounter system not fully tested
- Map transitions need testing
- Coin collection feedback (sound/effects)

### Driving Screen
- Verify all systems work after boost unification

### Space Flight Screen  
- Verify all systems work after boost unification

### General
- Add more maps to MapCatalog
- Enemy encounters
- Boss battles
- Sound effects
- Music

---

## 3. Optimizations - Prime Suspects

### A. PlayerController Boost Sync
**File:** `src/Starfield2026.Core/Controllers/PlayerController.cs`
**Issue:** Boosts synced before/after Update() in OverworldScreen - potential race condition
**Fix:** Have PlayerController accept BoostSystem reference directly, or use event-based sync

### B. OrbitCamera Follow Smoothness
**File:** `src/Starfield2026.Core/Camera/OrbitCamera.cs`
**Issue:** Lerp-based follow may feel floaty or snappy depending on FollowSpeed
**Fix:** Consider exponential decay for smoother feel: `pos = Vector3.Lerp(pos, target, 1f - exp(-speed * dt))`

### C. Coin Spawn Performance
**File:** `src/Starfield2026.Core/Systems/CoinCollectibleSystem.cs`
**Issue:** List iteration every frame for collision/updates
**Fix:** Spatial partitioning if many coins, or limit max active coins

### D. InputManager Direct Keyboard Access
**File:** `src/Starfield2026.Core/Controllers/PlayerController.cs` (line 74)
**Issue:** PlayerController calls Keyboard.GetState() directly, bypassing InputManager
**Fix:** Remove direct keyboard access, rely solely on InputSnapshot for consistency

---

## 4. Step-by-Step Approach to Get App Fully Working

1. **Build and run** - `dotnet build src/Starfield2026.sln && dotnet run --project src/Starfield2026.3D`

2. **Test overworld camera**
   - Walk around, verify camera follows smoothly
   - Toggle run (Shift), verify zoom out/in
   - Rotate camera (Q/E), verify smooth rotation
   - Adjust pitch (R/F), verify clamps work

3. **Test player movement**
   - Walk in all directions
   - Run toggle
   - Jump (Space/Alt/C) - especially diagonal (W+A+Space)
   - Hover/jetpack in air

4. **Test boost system**
   - Collect blue coins in overworld
   - Verify HUD shows boost count
   - Use hover, verify boost decreases
   - Switch screens, verify boost persists

5. **Test screen transitions**
   - Overworld -> Driving (Enter key)
   - Driving -> Space (trigger)
   - Space -> Overworld (land)

6. **Fix any issues found**

---

## 5. How to Start/Test

```bash
# Navigate to project
cd D:\Projects\Starfield-2026

# Build
dotnet build src/Starfield2026.sln

# Run
dotnet run --project src/Starfield2026.3D

# Or run from Visual Studio / VS Code
```

### Controls
| Action | Keys |
|--------|------|
| Move | W/A/S/D or Arrow keys |
| Run toggle | Shift (press to toggle) |
| Jump | Space, Alt, or C |
| Camera rotate | Q/E |
| Camera pitch | R/F |
| Camera zoom | Z/X or Mouse scroll |
| Launch to driving | Enter |
| Pause | P |
| Exit | Escape |

---

## 6. Issues & Strategies

### Issue 1: Jump not working when traveling diagonal
**Root Cause:** Keyboard ghosting (hardware limitation)
**Strategies:**
- Already fixed by adding C and Alt as jump alternatives
- Consider allowing key rebinding in settings

### Issue 2: Camera not zooming back in when idle
**Root Cause:** Condition was `IsRunning && IsMoving` instead of `IsRunning`
**Strategies:**
- Fixed: now checks both conditions
- Test edge cases: run toggle while stationary, run toggle while moving

### Issue 3: Boosts not persisting across screens
**Root Cause:** Overworld used PlayerController.BoostCount, other screens used BoostSystem
**Strategies:**
- Fixed: all screens now share single BoostSystem instance
- PlayerController syncs from BoostSystem before update, writes back after

### Issue 4: OrbitCamera rewritten but may have issues
**Root Cause:** Multiple failed attempts to tune parameters led to confusion
**Strategies:**
- Fresh rewrite completed
- If still broken: replace with simpler FollowCamera approach
- Consider removing orbit mechanics entirely, use fixed follow distance

---

## 7. New Architecture & Features

### New Features This Session
1. **Jetpack/Hover System** - Press jump in air to hover, costs boosts, 10s duration
2. **Run Toggle** - Shift toggles run mode (no longer hold)
3. **Unified Boost System** - Single source of truth across all screens
4. **Diagonal Jump Fix** - Alt and C keys added as jump alternatives

### Quick Wins
1. **Camera presets** - Add method to OrbitCamera for different view modes (close/far/combat)
2. **Boost pickup sound** - Add sound effect when collecting blue coins
3. **Footstep sounds** - Add audio feedback for walking/running
4. **Dust particles** - Visual effect when landing from jump

### Architecture Notes
```
Starfield2026.3D (Game)
    ├── Starfield2026Game.cs (main loop, manages screens)
    └── Screens
        ├── OverworldScreen (exploration, PlayerController)
        ├── DrivingScreen (vehicle, VehicleController)
        └── SpaceFlightScreen (on-rails, ShipController)

Starfield2026.Core (Library)
    ├── Camera/
    │   ├── OrbitCamera.cs (overworld - needs work)
    │   ├── ChaseCamera.cs (driving - working)
    │   └── FollowCamera.cs (space - working)
    ├── Controllers/
    │   ├── PlayerController.cs (overworld movement, jump, hover)
    │   ├── VehicleController.cs (driving physics)
    │   └── ShipController.cs (space flight)
    ├── Systems/
    │   ├── BoostSystem.cs (SHARED across all screens)
    │   ├── AmmoSystem.cs (SHARED)
    │   └── CoinCollectibleSystem.cs
    ├── Save/
    │   ├── GameState.cs (runtime state)
    │   └── GameDatabase.cs (SQLite persistence)
    └── UI/
        └── HUDRenderer.cs (displays health, coins, boosts)
```

---

## Lessons Learned

1. **Listen carefully to instructions** - When user says "nuke it", they mean rewrite from scratch, not tweak parameters

2. **Avoid parameter creep** - Too many small changes to camera values led to confusion. Better to reset and start fresh

3. **Unify systems early** - Having separate boost counts per screen was a design flaw. Should have used single shared system from start

4. **Input abstraction** - Direct keyboard access in PlayerController bypasses InputManager, causing inconsistency. All input should go through InputSnapshot

5. **Test edge cases** - Diagonal movement + jump, run toggle while stationary, screen transitions with active boosts

---

*Generated: 2026-02-20*
*Project: https://github.com/ChrisColeTech/Starfield-2026*
