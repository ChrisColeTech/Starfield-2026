# 06 — Lessons Learned & Handoff: Ammo System, HUD, Health & Architecture

**Date:** 2026-02-20  
**Session Focus:** HUD simplification, PixelFont, health bar, ammo rebalancing, starfield polish, nullable cleanup  
**Status:** Core systems complete. Build clean (0 warnings, 0 errors). Game functional with HUD, health bar, and ammo constraints.

---

## 1. What We Accomplished

### New Systems Created

| System | File | Purpose |
|--------|------|---------|
| `AmmoSystem` | `Core/Systems/AmmoSystem.cs` | Centralized ammo tracking, coin-to-ammo conversion, projectile type selection |
| `AmmoConfig` | `Core/Systems/AmmoSystem.cs` | Static configuration for coin values, projectile damage, colors |
| `PlayerHealthSystem` | `Core/Systems/PlayerHealthSystem.cs` | Player HP tracking, damage, healing, death events |
| `PixelFont` | `Core/Rendering/PixelFont.cs` | 5×7 bitmap font renderer for readable HUD text |
| `EnemySystem` | `Core/Systems/EnemySystem.cs` | AI-driven enemies with pursuit behaviors, enemy projectiles (partial) |

### Controllers Extracted (SRP Refactoring)

| Controller | Extracted From | Lines Reduced |
|------------|----------------|---------------|
| `PlayerController` | `OverworldScreen.cs` | -78 lines |
| `VehicleController` | `DrivingScreen.cs` | -114 lines |
| `ShipController` | `SpaceFlightScreen.cs` | -126 lines |

### Systems Extracted

| System | Extracted From | Purpose |
|--------|----------------|---------|
| `ProjectileSystem` | `SpaceFlightScreen` + `DrivingScreen` | Shared projectile spawning, collision, rendering |
| `BossSystem` | `SpaceFlightScreen` | Boss spawning, damage, flashing, rendering |

### Coin & Projectile Types

| Type | Color | Ammo Granted | Damage | Cost/Shot | Projectile Size | Spawn Chance | Coin Value |
|------|-------|-------------|--------|-----------|-----------------|-------------|------------|
| Gold Coin | Gold | 10 | 1x | 1 | 0.4 (small) | 90% | 1 coin |
| Red Coin | Red | 10 | 2x | 2 | 0.8 (big) | 10% | 3 coins |

### Persistence Updates

| Field | Added To | Purpose |
|-------|----------|---------|
| `GoldAmmo` | `PlayerProfile`, `GameDatabase` | Persist gold projectile count |
| `RedAmmo` | `PlayerProfile`, `GameDatabase` | Persist red projectile count |

### Before/After Screen Complexity

| Screen | Before | After | Reduction |
|--------|--------|-------|-----------|
| SpaceFlightScreen | 331 lines, 3 classes | 208 lines, 1 class | -37% |
| DrivingScreen | 270 lines, 2 classes | 159 lines, 1 class | -41% |
| OverworldScreen | 249 lines | 188 lines | -24% |

---

## 2. What Work Remains

### Critical (Blocks Gameplay)

1. **Battle Screen** — Not implemented. Turn-based creature combat system needed.
2. **Enemy System Integration** — `EnemySystem` created but not wired into `SpaceFlightScreen` properly.
3. **Database Migration** — Old databases crash on load due to missing columns. Need proper migration or delete-old-db strategy.
4. ~~**SpriteFont** — HUD still uses pixel blocks instead of real font glyphs.~~ ✅ Solved with `PixelFont.cs`.

### Important (Core Experience)

5. **Enemy AI Behaviors** — Three behaviors coded (pursue, strafe, orbit) but enemies don't spawn via key presses (1-9).
6. **Enemy Projectiles** — Red enemy projectiles implemented but player collision damage not applied.
7. ~~**Player Health** — No health system.~~ ✅ `PlayerHealthSystem` created and health bar added to HUD.
8. **Wire Health to Enemies** — Health system exists but no damage source wired yet (enemy projectiles, collisions).
9. **Boss Improvements** — Boss spawns but has no movement patterns, just rotates in place.

### Nice to Have

9. **More Coin Types** — Architecture supports Green/Blue coins but not implemented.
10. **Projectile Visuals** — Gold/Red projectiles are just colored cubes. Could have trails or different shapes.
11. **Ammo Pickups** — Direct ammo pickups (not from coins) would add variety.
12. **Weapon Modifiers** — Spread shot, rapid fire, homing projectiles.

---

## 3. Optimizations — Prime Suspects

### Suspect 1: CoinRenderer Rebuilds Vertices Every Frame

**What happens:** `CoinRenderer.Draw()` loops through all vertices and updates their colors every frame, then calls `DrawUserIndexedPrimitives`.

**Root cause:** Coin colors are dynamic (gold vs red), so vertex colors must change per-coin. But updating all vertices for every coin is O(coins × vertices) per frame.

**Fix strategy:**
1. Create two pre-built coin meshes (gold and red) at initialization
2. Draw gold coins with gold mesh, red coins with red mesh
3. Eliminates per-frame vertex updates entirely
4. Alternative: Use a shader uniform for color instead of per-vertex colors

### Suspect 2: ProjectileSystem Uses List<struct> with Copy Semantics

**What happens:** `ProjectileInstance` is a struct stored in `List<ProjectileInstance>`. Every access copies the struct. The update loop does `var p = _projectiles[i]; p.Position += ...; _projectiles[i] = p;`.

**Root cause:** Structs are value types. List indexer returns a copy. Must write back after modification.

**Fix strategy:**
1. Change to `class` instead of `struct` — eliminates copy overhead
2. Or use array with manual count tracking: `ProjectileInstance[] _projectiles` + `int _count`
3. Or accept the copy cost (it's small for ~50 projectiles) but document it

### Suspect 3: Starfield Update Creates Garbage

**What happens:** `StarfieldRenderer.Update()` modifies `_stars[i].Position` in a loop. Since `Star` is a struct in an array, this is actually efficient. But if converted to List, would have same issue as projectiles.

**Root cause:** Currently using array, so this is fine. But worth auditing.

**Fix strategy:** Keep using arrays for high-frequency particle systems. Never use `List<struct>` for particles.

### Suspect 4: No Object Pooling for Projectiles/Enemies

**What happens:** New `ProjectileInstance` structs are allocated on every fire. Enemies are `new Enemy()` on spawn. GC collects them when removed.

**Root cause:** No pooling system. Each spawn allocates, each despawn collects.

**Fix strategy:**
1. Create `ObjectPool<T>` class with `Acquire()` and `Release()` methods
2. Pre-allocate 100 projectiles at startup
3. Reuse dead projectiles instead of allocating new ones
4. Same for enemies (smaller pool, ~20 enemies)

---

## 4. Step-by-Step: Getting the App Fully Working

### Prerequisites
```
.NET SDK 9.0.203+
```

### First-Time Setup (Important!)

The database schema changed. Old databases will crash. Delete the old DB:

```powershell
del "D:\Projects\Starfield-2026\src\Starfield2026.3D\bin\Debug\net9.0\starfield2026.db"
```

### Build & Run
```powershell
cd D:\Projects\Starfield-2026
dotnet build src\Starfield2026.sln
dotnet run --project src\Starfield2026.3D
```

### Verify All Systems

1. **Space Flight** — Game opens here. WASD moves, Z toggles ammo type (color changes in HUD), SPACE fires (costs ammo: gold=1, red=2). Red projectiles are 2x bigger.
2. **Overworld** — ESC to enter. WASD walks, SHIFT toggles run, SPACE jumps. Coins spawn while moving. "Coins: #" shown upper-right below health bar.
3. **Driving** — ESC again. W/S speed, A/D steer, SHIFT turbo, SPACE fires (costs ammo).
4. **Ammo persists** — Close and restart, ammo count should be preserved.
5. **Coin collection converts to ammo** — Each coin gives 10 ammo regardless of type.
6. **Health bar** — Always visible upper-right. Green → Yellow → Red based on HP%.
7. **Stars** — Render as dots when stopped, streak when moving.

### Key Bindings Reference

| Key | Space | Overworld | Driving |
|-----|-------|-----------|---------|
| W | Move up | Forward | Accelerate |
| S | Move down | Backward | Brake |
| A | Move left | Turn left | Steer left |
| D | Move right | Turn right | Steer right |
| Q/E | — | Camera orbit | — |
| Shift | — | Run toggle | Turbo (hold) |
| Space | Fire projectile | Jump | Fire projectile |
| Z | Toggle ammo type | Toggle ammo type | Toggle ammo type |
| Ctrl | Brake/slow | — | — |
| Alt | Accelerate | — | — |
| 1-9 | Spawn enemies (not wired) | — | — |
| ESC | → Overworld | → Driving | → Space |

---

## 5. How to Start & Test

### Starting the Application
```powershell
cd D:\Projects\Starfield-2026
dotnet run --project src\Starfield2026.3D
```

### Manual Test Checklist

- [ ] Game launches without crash
- [ ] Space flight screen shows ship (cube), starfield, grids, coins
- [ ] WASD moves the ship in X/Y
- [ ] Ctrl/Alt change speed (brake/accelerate)
- [ ] Z toggles between GOLD and RED ammo (HUD color changes)
- [ ] SPACE fires projectiles (gold=small, red=big)
- [ ] Collecting coins adds 10 ammo per coin
- [ ] Red coins are rarer (~10% spawn rate)
- [ ] Cannot fire when ammo is 0
- [ ] Red ammo costs 2 per shot, gold costs 1
- [ ] Ammo counter decreases 1:1 with visible projectiles (gold)
- [ ] Health bar visible upper-right on all screens
- [ ] ESC transitions to Overworld with fade
- [ ] Overworld: WASD walks, SHIFT runs, SPACE jumps
- [ ] ESC transitions to Driving
- [ ] Driving: W/S speed, A/D steer, SHIFT turbo
- [ ] Close and restart — ammo preserved

### Testing Persistence

```powershell
# 1. Start game, collect coins, note ammo count
# 2. Close game
# 3. Check DB file exists
dir "D:\Projects\Starfield-2026\src\Starfield2026.3D\bin\Debug\net9.0\starfield2026.db"

# 4. Restart game — same ammo count
```

---

## 6. Known Issues & Strategies

### Issue 1: Old Database Crashes on Load

**Symptom:** `SQLite Error 1: 'no such column: gold_ammo'`

**Root Cause:** Database schema was extended with `gold_ammo` and `red_ammo` columns. Existing DBs don't have them.

**Strategies:**
1. **Delete old DB** (quick fix) — `del starfield2026.db` — loses old save data
2. **Proper migration** — Check if columns exist, `ALTER TABLE ADD COLUMN` if not
3. **Version-based migration** — Add `schema_version` column, run migrations on load
4. **Recreate on mismatch** — If columns missing, drop table and recreate

### Issue 2: Enemies Not Spawning

**Symptom:** Pressing 1-9 does nothing

**Root Cause:** `EnemySystem` created but not wired into `SpaceFlightScreen`. The screen has `_enemies` field but the key-handling and integration was interrupted.

**Strategies:**
1. **Wire in Update** — Add key handling for 1-9 in `SpaceFlightScreen.Update()`
2. **Add to Draw** — Call `_enemies.Draw(device, view, proj)` in `SpaceFlightScreen.Draw()`
3. **Wire collision** — Check `_enemies.CheckPlayerCollisions()` for damage
4. **Test incrementally** — Start with just spawning/drawing, add AI and combat later

### Issue 3: No Player Health / Damage

**Symptom:** Enemy projectiles hit player but nothing happens

**Root Cause:** No health system exists. `CheckPlayerCollisions()` returns hits but they're ignored.

**Strategies:**
1. **Add HealthSystem** — Create `HealthSystem` class with current/max HP, damage, death event
2. **Wire into screens** — Each screen has `HealthSystem _health`
3. **Visual feedback** — Flash screen red on damage, show health bar in HUD
4. **Respawn on death** — Reset position and health, lose some coins

### Issue 4: Coin Spawning Too Sparse / Too Dense

**Symptom:** Coins feel rare or overwhelming depending on screen

**Root Cause:** `SpawnInterval` values now tuned: Space=3s, Overworld=4s, Driving=2s. Red coin chance reduced to 10%.

**Strategies:**
1. **Dynamic spawning** — Spawn more coins at high speed, fewer when stopped
2. **Pattern spawning** — Spawn in groups/lines instead of random single coins
3. **Difficulty scaling** — Spawn more as player gets better (track score/time)

---

## 7. Architecture & New Features

### Current Architecture (Post-Refactoring)

```
Starfield2026.3D (Desktop App)
├── Program.cs                    → Entry point
├── Starfield2026Game.cs            → Screen router, HUD, transitions, AmmoSystem/Health owner
│
Starfield2026.Core (Library)
├── Controllers/
│   ├── PlayerController.cs       → Overworld movement, jump, run
│   ├── VehicleController.cs      → Driving physics, steering, turbo
│   └── ShipController.cs         → Space flight movement, gear system
├── Input/
│   ├── InputManager.cs           → Polls hardware → InputSnapshot
│   └── InputSnapshot.cs          → Immutable per-frame input state
├── Rendering/
│   ├── Camera3D.cs               → Third-person orbit camera (smooth follow)
│   ├── CoinRenderer.cs           → 3D coin mesh (dynamic color)
│   ├── CubeRenderer.cs           → Per-face colored 3D cube
│   ├── GridRenderer.cs           → Configurable grid plane
│   ├── PixelFont.cs              → 5×7 bitmap glyph renderer (A-Z, 0-9, symbols)
│   └── StarfieldRenderer.cs      → Dots when still, streaked lines when moving
├── Screens/
│   ├── IGameScreen.cs            → Interface: Initialize, Update, Draw, OnEnter/OnExit
│   ├── SpaceFlightScreen.cs      → Space flight mode (uses ShipController)
│   ├── OverworldScreen.cs        → 3D exploration (uses PlayerController)
│   └── DrivingScreen.cs          → Driving mode (uses VehicleController)
├── Systems/
│   ├── AmmoSystem.cs             → Ammo tracking, coin conversion, type selection
│   ├── CoinCollectibleSystem.cs  → Coin spawning, animation, collection
│   ├── BossSystem.cs             → Boss spawning, damage, rendering
│   ├── EnemySystem.cs            → Enemy AI, spawning, combat (partial)
│   ├── PlayerHealthSystem.cs     → HP tracking, damage, heal, death events
│   └── ProjectileSystem.cs       → Projectile spawning, collision, rendering
└── Save/
    ├── GameDatabase.cs           → SQLite persistence (ammo + coins + screen)
    └── PlayerProfile.cs          → Saveable player data
```

### Design Patterns Used

| Pattern | Implementation | Benefit |
|---------|----------------|---------|
| **Controller** | `PlayerController`, `VehicleController`, `ShipController` | Separates input → state logic from screen |
| **System** | `AmmoSystem`, `ProjectileSystem`, `CoinCollectibleSystem` | Reusable game logic across screens |
| **Config** | `AmmoConfig` static class | Easy tuning without code changes |
| **Dependency Injection** | `Ammo = _ammoSystem` property on screens | Shared state without globals |

### Guide: Adding a New Coin Type

1. **Add to `CoinType` enum** in `AmmoSystem.cs`:
   ```csharp
   public enum CoinType { Gold, Red, Green, Blue }
   ```

2. **Add config values** in `AmmoConfig`:
   ```csharp
   public static readonly Color GreenColor = Color.Lime;
   public static int GreenCoinAmmo = 100;
   public static int GreenDamageMultiplier = 3;
   ```

3. **Update `CoinCollectibleSystem.SpawnRandomAhead()`**:
   ```csharp
   var type = _random.NextDouble() switch
   {
       < 0.1 => CoinType.Green,  // 10% green
       < 0.3 => CoinType.Red,    // 20% red
       _ => CoinType.Gold        // 70% gold
   };
   ```

4. **Update `AmmoSystem.AddAmmoFromCoin()`**:
   ```csharp
   case CoinType.Green:
       GreenAmmo += AmmoConfig.GreenCoinAmmo;
       break;
   ```

### Guide: Adding a New Projectile Type

1. **Add to `ProjectileType` enum**
2. **Add config values** (color, damage, maybe speed)
3. **Update `ProjectileSystem.Spawn()`** to accept the type
4. **Update `Draw()`** to use correct color
5. **Update `AmmoSystem`** to track the new ammo type

### Quick Wins

1. **Wire EnemySystem to keys 1-9** — Add key handling in `SpaceFlightScreen.Update()`, draw in `Draw()`. ~30 min.

2. **Wire health to enemy damage** — Call `_playerHealth.TakeDamage()` when enemy projectiles hit player. ~20 min.

3. **Death/respawn logic** — On `OnDeath` event, reset position and health, lose some coins. ~30 min.

4. **Persist health to DB** — Add `CurrentHealth` to `PlayerProfile` and `GameDatabase`. ~15 min.

5. **Boss movement patterns** — Give the boss strafe/dive behaviors instead of static rotation. ~45 min.

---

## 8. Key Lessons for Next Session

1. **Architect for extensibility from the start.** The `AmmoConfig` static class with `GetProjectileColor()`, `GetDamageMultiplier()`, etc. makes adding new types trivial.

2. **Controllers belong in their own folder.** Extracting `PlayerController`, `VehicleController`, and `ShipController` reduced screen complexity by ~30%.

3. **Structs in Lists require write-back.** `List<T>` where `T : struct` has subtle copy semantics. Use `class`, arrays, or always write back.

4. **Database migrations need planning.** Adding columns breaks existing saves. Next time: add `schema_version` from day one.

5. **Never consume resources before confirming the action succeeds.** Ammo was consumed every frame while Space was held, but `TryFire()` only spawns when cooldown expires. Fix: consume AFTER confirming the projectile spawned.

6. **Pixel-block text is unreadable.** Drawing identical rectangles for each character provides zero information. A 5×7 bitmap font with actual glyph shapes is trivially small but makes the HUD functional.

7. **Speed-dependent rendering matters.** Stars as static streaks look like worms when the ship is stopped. Switching between dot rendering (still) and streak rendering (moving) gives proper visual feedback.

8. **Balance tuning is iterative.** Initial coin values (50 ammo per coin) felt absurd. Reducing to 10 per coin and adjusting spawn intervals required multiple playtest cycles.

---

## 9. Commit Message Template

```
feat(ammo): Add gold/red coin and projectile system

- Create AmmoSystem with coin-to-ammo conversion
- Add CoinType (Gold=50 ammo, Red=25 ammo, 2x damage)
- Add ProjectileType with damage multipliers
- Extract PlayerController, VehicleController, ShipController
- Update PlayerProfile/GameDatabase for ammo persistence
- Update CoinRenderer for dynamic colors
- Update ProjectileSystem for typed projectiles
- Wire Z key to toggle ammo type
- Refactor screens to use Ammo property injection

Breaking: Delete old starfield2026.db before running
```

---

## 10. File Summary

| File | Purpose | Status |
|------|---------|--------|
| `Core/Systems/AmmoSystem.cs` | Ammo tracking, type selection | ✅ Updated (10 ammo/coin, 2x red cost) |
| `Core/Systems/PlayerHealthSystem.cs` | Player HP, damage, death | ✅ New |
| `Core/Systems/EnemySystem.cs` | Enemy AI and combat | ⚠️ Partial |
| `Core/Systems/ProjectileSystem.cs` | Projectile spawning, red 2x size | ✅ Updated |
| `Core/Systems/CoinCollectibleSystem.cs` | Coin types, 10% red chance | ✅ Updated |
| `Core/Rendering/PixelFont.cs` | 5×7 bitmap font for HUD | ✅ New |
| `Core/Rendering/CoinRenderer.cs` | Dynamic colors | ✅ Updated |
| `Core/Rendering/StarfieldRenderer.cs` | Dots when still, streaks when moving | ✅ Updated |
| `Core/Controllers/PlayerController.cs` | Overworld movement | ✅ Existing |
| `Core/Controllers/VehicleController.cs` | Driving physics | ✅ Existing |
| `Core/Controllers/ShipController.cs` | Space flight | ✅ Existing |
| `Core/Screens/*.cs` | Ammo consumption fix, spawn tuning | ✅ Updated |
| `Core/Save/PlayerProfile.cs` | Ammo fields | ✅ Existing |
| `Core/Save/GameDatabase.cs` | Ammo persistence | ✅ Existing |
| `3D/Starfield2026Game.cs` | HUD, health bar, PixelFont | ✅ Updated |
