# 10 — Lessons Learned & Handoff: Coins, Boost System & Driving Polish

**Date:** 2026-02-20  
**Session Focus:** Blue/Green coin types, boost system, camera follow fix, vehicle hover, ship bob, persistence  
**Status:** Build clean (0 errors, 0 warnings). Game functional with all four coin types, boost activation, and idle animations.

---

## 1. What We Accomplished

### New Systems Created

| System | File | Purpose |
|--------|------|---------|
| `BoostSystem` | `Core/Systems/BoostSystem.cs` | Manages boost charges (max 5), `AddBoost()`, `TryActivate()`, `Changed` event |

### Coin System Expansion — Blue & Green Coins

| Type | Color | Effect | Spawn Chance | Cap |
|------|-------|--------|-------------|-----|
| Gold | Gold | +10 ammo | ~76% | None |
| Red | Red | +10 ammo (2x damage) | ~8% | None |
| **Blue** | DodgerBlue | +1 boost charge | ~8% | 5 max |
| **Green** | LimeGreen | +25% health (if below 100%) | ~8% | 100% HP |

### Boost Mechanic

| Property | Value |
|----------|-------|
| Max charges | 5 |
| Activation | Press Shift (single press, not hold) |
| Duration | 10 seconds per charge |
| Speed | Auto-accelerates to 150 mph regardless of input |
| Behavior | No deceleration during boost, holds 150 mph for full duration |
| Works from idle | Yes — boost fires from standstill |
| Stacking | Pressing Shift during active boost resets the 10s timer |

### Camera & Animation Polish

| Change | Before | After |
|--------|--------|-------|
| Camera follow speed | 3f (lagged behind at speed, appeared to zoom out) | 12f (locked behind car at all speeds) |
| Speed-based FOV | +0.35 rad at max speed | +0.15 rad (subtle) |
| Idle vehicle animation | Random jitter rumble | Smooth sine-wave hover (Y bob + X sway) |
| Idle ship animation | None | Gentle sine-wave bob (fades at speed, idle-only) |
| Steering at rest | Blocked (required speed > 0.5) | Allowed at half turn rate |

### Persistence Updates

| Field | Added To | Purpose |
|-------|----------|---------|
| `BoostCount` | `PlayerProfile`, `GameDatabase`, `GameState` | Persist boost charges between sessions |
| `boost_count` | SQLite schema + migration | New column with `ALTER TABLE` fallback |

### Files Modified

| File | Changes |
|------|---------|
| `Core/Systems/BoostSystem.cs` | **[NEW]** Boost charge management with events |
| `Core/Systems/AmmoSystem.cs` | Added `Blue`/`Green` to `CoinType`, colors |
| `Core/Systems/CoinCollectibleSystem.cs` | 4-type tracking, weighted spawn, `PickCoinType()` |
| `Core/Managers/CoinCollector.cs` | Blue→boost, Green→heal routing |
| `Core/Controllers/VehicleController.cs` | Boost speed override, sine hover, steering at rest |
| `Core/Controllers/ShipController.cs` | `BobOffset`, `_elapsed` timer, idle bob |
| `Core/Screens/DrivingScreen.cs` | Boost activation (Shift press), camera tuning |
| `Core/Screens/SpaceFlightScreen.cs` | Boost activation, bob offset in draw |
| `Core/Camera/ChaseCamera.cs` | `FollowSpeed` 3→12, reduced FOV speed effect |
| `Core/UI/HUDRenderer.cs` | Boost count display in DodgerBlue |
| `Core/Save/PlayerProfile.cs` | `BoostCount` property |
| `Core/Save/GameDatabase.cs` | `boost_count` column, migration, save/load |
| `Core/Save/GameState.cs` | `BoostCount` tracking, `SetBoostCount()` |
| `3D/Starfield2026Game.cs` | Wires `BoostSystem` to all screens, collector, HUD, persistence |

---

## 2. What Work Remains

### Critical (Blocks Gameplay)

1. **Starfield backgrounds per screen** — Each screen needs a proper starry background with stars that streak as the player moves. Currently inconsistent between screens.
2. **Enemy System** — `EnemySystem` exists but isn't wired into gameplay. No enemies spawn or deal damage.
3. **Health damage sources** — `PlayerHealthSystem` tracks HP but nothing actually damages the player.

### Important (Core Experience)

4. **Green coin visual feedback** — No flash or sound when health is restored.
5. **Boost visual feedback** — No particle trail or color change during active boost.
6. **Boost HUD timer** — Show remaining boost duration (seconds countdown) alongside charge count.
7. **Boss movement patterns** — Boss rotates in place but doesn't attack or move.

### Nice to Have

8. **Boost exhaust trail** — Particle emitter behind vehicle/ship during boost.
9. **Coin magnet** — Blue coins pull toward player when nearby.
10. **Boost speedlines** — Screen-space streaks during boost for visual impact.

---

## 3. Optimizations — Prime Suspects

### Suspect 1: Camera Follow Lerp at High Frequencies

**What happens:** `ChaseCamera.Update()` uses `Vector3.Lerp(_currentPosition, targetPosition, FollowSpeed * dt)` with `FollowSpeed = 12`. At 60fps, `12 * 0.016 = 0.192` (19% per frame). At 120fps, `12 * 0.008 = 0.096` (9.6% per frame). Camera feels different at different framerates.

**Fix strategy:**
1. Use frame-independent smoothing: `Lerp(current, target, 1 - pow(0.01, dt))` instead of `speed * dt`
2. Or use a spring system with velocity/damping that's physics-based
3. Or clamp `dt` to prevent large jumps during frame spikes

### Suspect 2: BoostSystem.Changed Event Triggers Save on Every Coin

**What happens:** Every blue coin collection calls `AddBoost()` → fires `Changed` event → `GameState.SetBoostCount()` → `Save()` → full SQLite profile write.

**Fix strategy:**
1. Batch saves — debounce with a 1-second timer, save only once per batch
2. Dirty flag — mark `_dirty = true` on change, save on next autosave tick
3. Save only on screen transition or game close, not per-coin

### Suspect 3: Sine Calculations Every Frame for Idle Animations

**What happens:** `Math.Sin(_elapsed * 2.0)` and similar called every frame even when the result is applied to `RumbleOffset` / `BobOffset`. At 60fps, that's 120-240 trig calls per second across both controllers.

**Fix strategy:**
1. Not actually a problem — `Math.Sin` is hardware-accelerated, nanosecond-level
2. But if profiling shows issues: use a lookup table or cubic approximation
3. Or only update every 2nd frame (30Hz is sufficient for idle bob)

### Suspect 4: CoinCollectibleSystem Spawns All Types with Float Comparison

**What happens:** `PickCoinType()` uses cascading `if (roll < threshold)` comparisons. With 4 coin types, this is 3 comparisons per spawn.

**Fix strategy:**
1. Not a real performance issue (spawns happen every 2-4 seconds)
2. But for extensibility: use a weighted random table (`CoinType[]` with pre-computed weights)
3. This would also make spawn rates data-driven instead of hardcoded

---

## 4. Step-by-Step: Getting the App Fully Working

### Prerequisites
```
.NET SDK 9.0.203+
```

### First-Time Setup

Delete old database if upgrading from a previous version (schema changed):
```powershell
del "D:\Projects\Starfield-2026\src\Starfield2026.3D\bin\Debug\net9.0\starfield2026.db"
```

### Build & Run
```powershell
cd D:\Projects\Starfield-2026
dotnet build src\Starfield2026.sln
dotnet run --project src\Starfield2026.3D
```

### Verify All New Features

1. **Space Flight** — Game opens here. Fly through coins, collect blue (DodgerBlue) and green (LimeGreen) alongside gold/red.
2. **Blue coins** — Collect blue coins. HUD shows "Boosts: N" in blue below ammo count. Max 5.
3. **Boost activation** — Press Shift. Vehicle/ship auto-accelerates to 150 mph for 10 seconds. Works from idle.
4. **Boost from idle** — Stop completely, press Shift. Should launch forward.
5. **Green coins** — Take damage first (not yet wired — set health low in code to test). Green coins heal 25% of max HP.
6. **Idle hover** — Stop vehicle on driving screen. Cube gently bobs up/down in a sine wave.
7. **Ship bob** — Stop ship on space flight. Cube bobs gently.
8. **Steering at rest** — On driving screen, press A/D while stopped. Vehicle turns in place.
9. **Persistence** — Collect boosts, close game, restart. Boost count preserved.

---

## 5. How to Start & Test

### Starting the Application
```powershell
cd D:\Projects\Starfield-2026
dotnet run --project src\Starfield2026.3D
```

### Updated Key Bindings

| Key | Space | Overworld | Driving |
|-----|-------|-----------|---------|
| W/S | Move up/down | Forward/Back | Accelerate/Brake |
| A/D | Move left/right | Turn | Steer (works at rest) |
| Shift | **Boost** (press) | Run toggle | **Boost** (press) |
| Space | Fire | Jump | Fire |
| Z | Toggle ammo | Toggle ammo | Toggle ammo |
| ESC | → Overworld | → Driving | → Space |

### Manual Test Checklist

- [ ] Game launches without crash
- [ ] All four coin colors spawn (Gold, Red, Blue, Green)
- [ ] Blue coin increments boost count in HUD (max 5)
- [ ] Shift activates boost — 150 mph for 10 seconds
- [ ] Boost works from standstill
- [ ] Boost doesn't chain (holding Shift doesn't auto-reactivate)
- [ ] Vehicle hovers smoothly when idle (no random jitter)
- [ ] Ship bobs gently when idle
- [ ] Camera stays locked behind car at all speeds (no zoom-out)
- [ ] Steering works at rest (A/D while stopped)
- [ ] Close and restart — boost count preserved
- [ ] HUD shows "Boosts: N" in blue

---

## 6. Known Issues & Strategies

### Issue 1: Camera Zoom-Out at Speed (RESOLVED)

**Symptom:** Camera fell behind the car at high speed, appearing to zoom out.

**Root Cause:** `ChaseCamera.FollowSpeed` was 3f — only 5% position catchup per frame. At 150 mph the car outran the camera.

**Resolution:** Increased `FollowSpeed` to 12f (19% per frame). Camera now tracks tightly at all speeds.

**Lesson:** Camera "zoom" issues aren't always about distance or FOV — follow lag at high velocity creates the same visual effect.

### Issue 2: Boost Chaining via Held Shift (RESOLVED)

**Symptom:** Single boost appeared to last 30+ seconds.

**Root Cause:** `input.RunHeld` (Shift held) re-triggered boost activation every frame after the previous boost expired, silently consuming multiple charges.

**Resolution:** Changed to `input.IsKeyJustPressed(Keys.LeftShift)` — each boost requires a fresh press.

**Lesson:** For consumable actions, always use "just pressed" not "is held."

### Issue 3: Build Failures from Locked DLLs (RECURRING)

**Symptom:** `dotnet build` fails with "file is locked by Starfield2026.3D (PID)."

**Root Cause:** Game process still running when rebuilding. `dotnet run` uses the cached old DLLs.

**Strategies:**
1. **Always close game before building** — Check Task Manager if unsure
2. **Use `dotnet build --no-incremental`** after closing to force full rebuild
3. **Add a pre-build script** that kills the process: `taskkill /IM Starfield2026.3D.exe /F`
4. **Consider hot-reload** — MonoGame doesn't support it natively, but config values could be loaded from JSON

### Issue 4: Boost Doesn't Decelerate After Expiry

**Symptom:** After 10-second boost ends, vehicle stays at 150 mph until player manually brakes.

**Root Cause:** `HandleSpeed` returns early during boost. When boost expires, speed is still 150 but `_maxSpeed` is 100. The normal deceleration logic only activates if no input is pressed.

**Strategies:**
1. **On boost end, set speed to maxSpeed** — `Speed = Math.Min(Speed, _maxSpeed)` in `HandleBoost` when timer hits 0
2. **Gradual slowdown** — Lerp speed from boost to normal over 2 seconds
3. **Keep current behavior** — Player controls deceleration (feels powerful)

---

## 7. Architecture & New Features

### Updated Architecture

```
Starfield2026.Core (Library)
├── Controllers/
│   ├── VehicleController.cs    → + ActivateBoost(), HandleBoost(), sine hover, steering at rest
│   └── ShipController.cs       → + ActivateBoost(), HandleBoost(), BobOffset
├── Systems/
│   ├── BoostSystem.cs          → [NEW] Boost charge management (max 5)
│   ├── AmmoSystem.cs           → + Blue/Green CoinType, colors
│   └── CoinCollectibleSystem.cs → + 4-type tracking, weighted spawning
├── Managers/
│   └── CoinCollector.cs        → + Blue→boost, Green→heal routing
├── Camera/
│   └── ChaseCamera.cs          → FollowSpeed 3→12, reduced FOV effect
├── UI/
│   └── HUDRenderer.cs          → + Boost count display
└── Save/
    ├── GameState.cs             → + BoostCount, SetBoostCount()
    ├── PlayerProfile.cs         → + BoostCount
    └── GameDatabase.cs          → + boost_count column with migration
```

### Data Flow: Blue Coin → Boost Activation

```
Blue Coin Spawned (CoinCollectibleSystem.PickCoinType, 8% chance)
    → Collected (CoinCollectibleSystem.Update, distance check)
    → Counter incremented (_newlyBlueCollected++)
    → Polled by CoinCollector.CollectFromScreen()
    → BoostSystem.AddBoost() called (cap at 5)
    → Changed event → GameState.SetBoostCount() → Save()
    → HUD reads BoostSystem.BoostCount → draws "Boosts: N"
    
Player presses Shift (IsKeyJustPressed)
    → BoostSystem.TryActivate() → decrements count → Changed event → Save
    → VehicleController.ActivateBoost(10f) → HasBoost=true, _boostTimer=10
    → HandleSpeed: auto-accelerate to 150mph, ignore input
    → HandleBoost: timer counts down → HasBoost=false after 10s
```

### Quick Wins

1. **Boost end slowdown** — In `HandleBoost`, when timer hits 0, set `Speed = Math.Min(Speed, _maxSpeed)`. ~5 min.

2. **Boost visual flash** — Change vehicle color to blue during `HasBoost` in `DrivingScreen.Draw()`. ~5 min.

3. **Star backgrounds per screen** — Each screen should share a consistent `StarfieldRenderer` with speed-dependent streaking. ~30 min.

4. **Coin collection particles** — Spawn 5-10 small colored cubes that fly outward on coin pickup. ~45 min.

---

## 8. Key Lessons for Next Session

1. **Camera follow lag ≠ zoom.** A slow `Lerp` follow speed causes the camera to fall behind at high velocity, visually identical to a zoom-out. Always test camera at max game speed.

2. **"Held" vs "JustPressed" matters for consumables.** Using `RunHeld` for boost activation caused silent charge consumption every frame after boost expiry. Consumable actions must use edge-detection (just pressed), not level-detection (is held).

3. **Locked DLLs block deployment silently.** `dotnet build` succeeds with warnings when DLLs can't be copied. The old binary runs instead. Always verify the game is closed before building.

4. **Idle animations sell the fantasy.** Random jitter rumble felt broken. A smooth sine-wave hover with different frequencies for X and Y axes feels like a hovercraft. Two lines of trig replaced 10 lines of random noise.

5. **Boost should feel autonomous.** A boost that requires holding W to work isn't a boost — it's a speed limit increase. True boost means "press button, go fast, hands-free."

6. **Persistence requires end-to-end wiring.** Adding `BoostCount` required changes in 5 files: `BoostSystem` → `GameState` → `PlayerProfile` → `GameDatabase` schema → `Starfield2026Game` initialization. Missing any link breaks the chain silently.

---

## 9. Commit Message Template

```
feat(coins): Add blue/green coins, boost system, and driving polish

- Add Blue coins (boost charges, max 5) and Green coins (25% heal)
- Create BoostSystem with charge management and persistence
- Implement boost activation (Shift press) — 10s at 150mph, works from idle
- Fix camera zoom-out at speed (FollowSpeed 3→12)
- Replace random vehicle rumble with smooth sine hover animation
- Add gentle idle bob to spaceship
- Allow steering at rest on driving screen
- Add boost_count to database schema with migration
- Display boost count on HUD in DodgerBlue
- Use IsKeyJustPressed to prevent boost chaining

Breaking: Delete old starfield2026.db before running (new column)
```
