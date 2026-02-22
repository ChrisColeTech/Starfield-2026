# 18 - Window State & DPI Persistence: Lessons Learned Handoff

**Date:** 2026-02-22
**Scope:** Window size/position persistence, DPI scaling, MonoGame DesktopGL SDL2 window management
**Status:** DPI-scaled window sizing working and sharp; persistence re-added with deferred restore; not yet committed

---

## 1. What We Accomplished

### DPI-Aware Window Sizing (Complete, Verified)
- Identified that `app.manifest` declaring `permonitorv2` DPI awareness causes MonoGame DesktopGL (SDL2) to interpret `PreferredBackBufferWidth/Height` as **physical pixels**, producing a half-sized window on 200% displays (800 → 400px logical)
- Added `GetDpiForSystem()` P/Invoke to query the Windows DPI scale factor at startup
- Back buffer is now set to `logicalSize * dpiScale`, producing a correctly-sized window with sharp rendering

### Window State Persistence (Complete, Build Verified)
- `WindowStateHelper.cs` saves/restores window position and size across sessions via `window.json`
- All sizes stored in **logical pixels** (divided by DPI on save, multiplied on restore) so the config is DPI-independent
- Position restore deferred to first `Update()` frame because SDL2's window isn't fully ready during `Initialize()`
- `ClientSizeChanged` subscription deferred until after restore to prevent SDL's initialization events from overwriting saved state
- Save triggers: on every resize (`ClientSizeChanged`) and on exit (`Exiting` event)

### Key Discovery: Win32 APIs Don't Work with MonoGame DesktopGL
- The original `WindowStateHelper` used `GetWindowRect`, `SetWindowPos`, `IsZoomed`, and `MonitorFromPoint` — all Win32 HWND APIs
- MonoGame DesktopGL uses SDL2 under the hood; `Window.Handle` is not a real Win32 HWND
- These APIs returned garbage data (`X:0, Y:0`, `Width:3772`) or silently failed
- **Solution:** Use MonoGame's own `Window.ClientBounds` and `Window.Position` instead

---

## 2. What Work Remains

### Window State — Untested at Runtime
- The persistence code compiles cleanly but the full save → close → relaunch → restore cycle has **not been runtime-verified** yet
- Position restore (`Window.Position = ...`) during the first `Update()` frame may produce a visible "jump" — needs checking
- Maximize state was removed from the config (the original used `IsZoomed` which doesn't work with SDL2) — could be re-added by checking `Window.ClientBounds` against screen bounds

### Edge Cases
- Multi-monitor: saved position on a disconnected monitor could place the window off-screen on next launch
- DPI changes between sessions (e.g., moving laptop to external monitor) — stored logical pixels should handle this, but untested
- `ClientSizeChanged` fires on every resize drag, writing `window.json` rapidly — could add debouncing

---

## 3. Optimizations — Prime Suspects

### Suspect 1: Rapid `window.json` Writes During Drag Resize
`OnClientSizeChanged` calls `WindowStateHelper.Save()` on every resize event, which fires continuously during a drag. This writes JSON to disk dozens of times per second.

**Fix:** Debounce the save — set a `_saveTimer` that resets on each resize event and only writes after 500ms of inactivity. Save immediately on `Exiting` regardless.

### Suspect 2: DPI Scale Queried Multiple Times
`GetDpiForSystem()` is called in the constructor, in `Save()` (every resize), and in `Restore()`. The Win32 P/Invoke overhead is minimal, but it's unnecessary repeated work since system DPI doesn't change during a session.

**Fix:** Cache the DPI scale once on first call: `private static float? _cachedScale;`

### Suspect 3: `ClientSizeChanged` Handler Calls `ApplyChanges()`
The handler sets `PreferredBackBufferWidth/Height` and calls `ApplyChanges()` on every resize. MonoGame DesktopGL already resizes the back buffer internally when the window is resized by the user — the explicit `ApplyChanges()` may be redundant and could cause a feedback loop.

**Fix:** Test removing the `ApplyChanges()` call from `OnClientSizeChanged()`. If the viewport auto-resizes without it, the handler only needs to save state.

### Suspect 4: No Monitor Bounds Validation on Position Restore
`Window.Position` is set blindly from saved config. If the monitor layout changed (external display disconnected), the window could be positioned off-screen with no recovery path.

**Fix:** After setting `Window.Position`, check `Window.ClientBounds` against `GraphicsAdapter.DefaultAdapter.CurrentDisplayMode` to verify the window is visible. If not, skip position restore.

---

## 4. Step-by-Step Approach to Get App Fully Working

### Phase 1: Verify Build
```bash
cd D:\Projects\Starfield-2026\src\Starfield2026.3DModelLoader
dotnet build
```
Should produce 0 errors, 0 warnings.

### Phase 2: Fresh Launch (No Config)
1. Delete `bin/Debug/net9.0/window.json` if it exists
2. `dotnet run`
3. **Verify:** Window opens at ~800×600 logical pixels, rendering is sharp (not blurry)
4. **Verify:** A `window.json` file is created in `bin/Debug/net9.0/` after resize or close

### Phase 3: Resize + Relaunch
1. Drag the window to a specific position and resize to a non-default size
2. Close with Escape
3. Check `window.json` — should contain logical pixel values and screen coordinates
4. `dotnet run` again
5. **Verify:** Window opens at the saved size and position

### Phase 4: Stress Tests
1. Maximize the window → close → relaunch (should open large, position reset)
2. Move window to a second monitor → close → relaunch (should restore to same monitor)
3. Delete `window.json` → relaunch (should fall back to 800×600 defaults)

---

## 5. How to Start/Test the App

### Build & Run
```bash
cd D:\Projects\Starfield-2026\src\Starfield2026.3DModelLoader
dotnet build
dotnet run
```

### Controls
| Key | Action |
|-----|--------|
| W/S | Move forward/backward |
| A/D | Turn left/right |
| Tab | Character select overlay |
| Esc | Quit (saves window state) |

### Debug Files
| File | Purpose |
|------|---------|
| `bin/Debug/net9.0/window.json` | Persisted window position/size (logical pixels) |
| `bin/Debug/net9.0/modelloader.log` | Full startup and character loading trace |
| `bin/Debug/net9.0/modelloader.db` | SQLite character registry |

---

## 6. Issues & New Strategies

### Issue 1: MonoGame DesktopGL Ignores `PreferredBackBuffer` on DPI Displays
**Symptom:** Window opens at half the expected size (800→400 on 200% DPI).
**Root cause:** With `permonitorv2` in `app.manifest`, SDL2 creates the window in physical pixels. The preferred buffer size of 800 is treated as 800 physical pixels = 400 logical pixels.
**Fix applied:** Multiply the desired logical size by `GetDpiForSystem() / 96f` before setting `PreferredBackBufferWidth/Height`.

### Issue 2: Win32 Window APIs Return Garbage for SDL2 Windows
**Symptom:** `GetWindowRect` returns `(0,0)`, `IsZoomed` returns false, `SetWindowPos` silently fails.
**Root cause:** MonoGame DesktopGL's `Window.Handle` is an SDL window pointer, not a Win32 HWND. All user32.dll APIs that operate on HWNDs fail silently or return zeroed data.
**Fix applied:** Replaced all Win32 interop with MonoGame's `Window.ClientBounds` and `Window.Position`.

### Issue 3: SDL Deferred Events Overwrite Restored Size
**Symptom:** `Restore()` sets correct size via `ApplyChanges()`, but the window reverts to default immediately after.
**Root cause:** During `base.Initialize()`, SDL creates the window at default size and queues a `ClientSizeChanged` event. This event fires after `Restore()` completes, overwriting the restored size.
**Fix applied:** Subscribe to `ClientSizeChanged` only after restore, inside the first `Update()` frame.

### Strategy 1: SDL2 Direct API via Reflection
If `Window.Position` or `ApplyChanges()` fails in edge cases, MonoGame DesktopGL's `GameWindow` has an internal `SdlGameWindow` that wraps `SDL.SDL_SetWindowSize()` and `SDL.SDL_SetWindowPosition()`. These can be accessed via reflection as a last resort:
```csharp
var sdlWindow = typeof(GameWindow).GetField("_handle", BindingFlags.NonPublic | BindingFlags.Instance);
```

### Strategy 2: Skip DPI Manifest Entirely
Remove `app.manifest` so the app is DPI-unaware. Windows handles all scaling via bitmap upscaling. The window appears at the correct logical size with zero DPI code, but rendering is slightly blurry on high-DPI displays. This is acceptable for a dev tool — many game editors use this approach.

### Strategy 3: Render Target for Resolution Independence
Render the entire scene to a `RenderTarget2D` at a fixed resolution (e.g., 1920×1080), then blit to the window. This decouples rendering resolution from window size entirely, eliminating all DPI issues. The back buffer matches the window, but the scene always renders crisp at the target resolution.

### Strategy 4: Use MonoGame's `Window.IsBorderless` + Manual Maximize
Instead of relying on Win32 `IsZoomed()`, track maximize state manually: when the user presses a hotkey or the window size matches the display mode, toggle `Window.IsBorderless` and resize to fill the screen. This gives full control over the maximize lifecycle without OS API dependencies.

---

## 7. Architecture & New Features

### New Feature: DPI-Aware Window State Persistence

```
WindowStateHelper (static)
├── GetDpiScale()          user32.dll GetDpiForSystem → float
├── Load(path)             JSON → WindowConfig (logical pixels)
├── Save(path, window)     ClientBounds → logical pixels → JSON
└── Restore(window, gfx)   logical pixels × DPI → ApplyChanges + Position

ModelLoaderGame Integration
├── Constructor             Load config → scale by DPI → set PreferredBackBuffer
├── First Update()          Restore position → subscribe ClientSizeChanged
├── OnClientSizeChanged     Sync back buffer + Save
└── Game_Exiting            Final Save
```

### Data Flow
```
Save:  Window.ClientBounds (physical px) ÷ DPI → logical px → window.json
Load:  window.json → logical px × DPI → PreferredBackBuffer (physical px)
```

### Quick Win 1: DPI Cache (5 min)
Cache `GetDpiForSystem()` result in a static field. Called 3+ times per session, all returning the same value.

### Quick Win 2: Save Debouncing (10 min)
Add a `DateTime _lastSave` field and skip writes if < 500ms since last save. Always save on `Exiting`.

### Quick Win 3: Off-Screen Guard (15 min)
After `Window.Position = ...`, verify the window center is within `GraphicsAdapter.DefaultAdapter.CurrentDisplayMode` bounds. Reset to centered if off-screen.

### Quick Win 4: Maximize State (20 min)
Add `bool Maximized` to `WindowConfig`. On save, set to true if `Window.ClientBounds` width/height matches the display mode. On restore, if maximized, set `Window.IsBorderless = true` and resize to fill.

---

## 8. Key Files Reference

| File | Purpose | Lines |
|------|---------|-------|
| `3DModelLoader/UI/WindowStateHelper.cs` | DPI-aware save/restore with logical pixel storage | 93 |
| `3DModelLoader/ModelLoaderGame.cs` | Game loop with deferred restore and DPI-scaled constructor | 266 |
| `3DModelLoader/app.manifest` | Per-monitor DPI awareness declaration | 10 |
| `3DModelLoader/bin/Debug/net9.0/window.json` | Runtime config (logical pixels + screen position) | 1 |

---

## 9. MonoGame DesktopGL DPI Reference

- **DPI awareness levels:** unaware (Windows bitmap-scales), system (single scale at startup), per-monitor v2 (live scale per monitor)
- **SDL2 behavior:** With per-monitor DPI, SDL2 sizes windows in physical pixels. `Window.ClientBounds` reports physical pixels. `Window.Position` operates in screen coordinates (physical on DPI-aware apps)
- **MonoGame's `PreferredBackBufferWidth/Height`:** Sets both the render target resolution AND the SDL window size. On DPI-aware apps, this is in physical pixels
- **Logical → Physical conversion:** `physical = logical × (GetDpiForSystem() / 96)`
- **`GetDpiForSystem()` returns:** 96 at 100%, 120 at 125%, 144 at 150%, 192 at 200%
- **Mixed DPI setups:** `GetDpiForSystem()` returns the primary monitor's DPI. For true per-monitor support, use `GetDpiForWindow(hwnd)` — but this requires the actual Win32 HWND, which SDL2 exposes via `SDL_GetWindowWMInfo()`
