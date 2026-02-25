# 32 — Viewport Controls, AutoRig Panel & Bug Fixes — Lessons Learned & Handoff

**Date:** 2026-02-25  
**Session Focus:** Programmatic viewport controls, AutoRig panel minimization, skeleton rendering bug fix, `clearAll` reset  
**Status:** Viewport controls fully working (camera, lighting, persistence), AutoRig panel slimmed, all MCP tools verified

---

## 1. What We Accomplished

### Viewport Controls System (✅ Complete)
Added full programmatic control over the 3D viewport via MCP, with persistence across sessions.

| Feature | Implementation |
|---------|---------------|
| **Camera orbit** | Spherical coords (azimuth/elevation/distance) → Three.js cartesian |
| **Pan** | Target offset (panX/panY/panZ) shifts OrbitControls target |
| **Zoom** | Distance parameter controls camera-to-target distance |
| **FOV** | Perspective camera field-of-view in degrees |
| **Lighting** | Key light multiplier (0–2) scales all 3-point lights proportionally |
| **Ambient** | Hemisphere light multiplier (0–2) |
| **Background** | Hex color applied to WebGL clear color |
| **Partial updates** | `{ azimuth: 90 }` only changes rotation, all else preserved |
| **Persistence** | Debounced electron-store save on every change, hydrate on mount |
| **Bidirectional sync** | OrbitControls drag → store → persist; MCP update → store → Three.js |

**Key design:** `ViewportSettings` uses spherical coordinates because OrbitControls already works in spherical internally. Mapping azimuth/elevation/distance is more intuitive for MCP consumers than raw camera xyz. The `applyingRef` guard prevents infinite feedback loops between programmatic updates and OrbitControls change events.

**Files:**
- `editorStore.ts` — `ViewportSettings` type, `DEFAULT_VIEWPORT`, `updateViewport`/`resetViewport` actions
- `Viewport.tsx` — `sphericalToCartesian()`/`cartesianToSpherical()`, light refs, hydration effect, apply effect
- `index.ts` — `POST /api/viewport` endpoint
- `mcp-server.ts` — `set_viewport` tool (10 optional params)
- `useBackendEvents.ts` — `viewport:update` WS handler
- `electron/main.js` — viewport defaults in electron-store

### AutoRig Panel Minimization (✅ Complete)
- Removed MODEL section (Load Model, Load Animation, Unload Animation, Fit Rig to Model)
- Migrated those operations to the **Editor** menu in `Header.tsx` (disabled placeholders)
- Panel now has 3 sections: **RIG TEMPLATE**, **GAME**, **BONES**
- Removed unused imports (`Import`, `Play`, `Square` from lucide-react)

### Skeleton Rendering Bug Fix (✅ Complete)
- **Root cause:** `POST /api/generate-rig` endpoint was **missing** from `backend/src/index.ts`. MCP tool succeeded but no WS broadcast was ever sent.
- **Red herring:** Initially suspected WS pipeline issues, store action bugs, or race conditions. The real issue was that the REST endpoint simply didn't exist.
- **Fix:** Added the endpoint, which broadcasts `rig:generate` with template name.

### clearAll Reset Fix (✅ Complete)
- `clearAll` store action was not resetting `rigTemplate` or `gameType`, so dropdowns retained stale values after clearing.
- Added `rigTemplate: 'human' as RigTemplate` and `gameType: 'SUNMOON' as GameType` to the reset payload.
- Also removed duplicate `resetAnimations`/`clearAll` definitions that crept in from overlapping edit sessions.

---

## 2. What Work Remains

### Critical
1. **Enable Editor menu items** — "Load Model", "Load Animation", "Unload Animation", "Fit Rig to Model" are disabled placeholders. Need file picker integration + store actions.
2. **Error boundary around Viewport** — Three.js errors still crash the entire React tree. Wrap `<Viewport>` in an error boundary.
3. **Scene disposal pipeline** — No explicit GPU resource cleanup when switching models. Geometry/material/texture leaks accumulate.

### Important
4. **Bone highlighting on click** — Click a bone in the viewport or bone list to highlight it. Requires raycasting + hover/selection state.
5. **Per-collection visibility toggles** — Click bone collection names (Spine, Head, etc.) to toggle visibility.  
6. **Export rig** — Export the current skeleton as JSON, glTF, or FBX for use in external tools.
7. **Viewport state in URL** — Encode viewport settings in the URL hash so sharing a link preserves the camera angle.

### Nice to Have
8. **Camera presets** — Front/Side/Top/Back/¾ view buttons in the panel or via MCP tool.
9. **Smooth camera transitions** — Animate camera to new position instead of snapping (use `THREE.Vector3.lerp` in render loop).
10. **Grid toggle** — Show/hide the ground grid via viewport settings.

---

## 3. Optimizations — Prime Suspects

### Suspect 1: Viewport Effect Triggering on Every Object Change
**Problem:** The `[viewport]` useEffect dependency re-runs on every Zustand shallow-compare failure. Since `viewport` is a new object reference on every `updateViewport()` call (spread creates new object), the effect fires even when values haven't meaningfully changed.  
**Fix:** Use `zustand/shallow` equality or memoize the viewport selector. Or compare individual fields with `useRef` to skip no-op updates.

```typescript
// Before: fires every time store.viewport reference changes
const viewport = useEditorStore(s => s.viewport)

// After: only fires when actual values differ
const viewport = useEditorStore(s => s.viewport, shallow)
```

### Suspect 2: OrbitControls Sync Timer Leak
**Problem:** The debounced OrbitControls `change` listener creates a `setTimeout` on every mouse move during orbit. If the component unmounts mid-timer, the callback fires after cleanup.  
**Fix:** Clear `syncTimer` in the init effect's cleanup function. Store the timer ref outside the listener closure.

### Suspect 3: Animation Effect Stale Closures
**Problem:** The animation `useEffect` at `Viewport.tsx` depends only on `[activeClipIndex]` but reads `storeAnimations` and `animationPlaying` from the outer scope. These stale closures can cause incorrect behavior.  
**Fix:** Add `storeAnimations` and `animationPlaying` to the dependency array, or use refs for values that shouldn't trigger re-runs.

### Suspect 4: Electron-Store Write Frequency
**Problem:** Every OrbitControls `change` event (mouse move during orbit) triggers a debounced 150ms write to electron-store. On slow disks, this creates I/O pressure and can cause jank.  
**Fix:** Increase debounce to 500ms. Or only persist on `mouseup`/`touchend` instead of continuous orbit. Use `controls.addEventListener('end', ...)` instead of `'change'`.

---

## 4. Step-by-Step: Getting the App Fully Working

### Prerequisites
```
Node.js 22+
npm 10+
```

### Install
```powershell
cd D:\Projects\Starfield-2026\src\Starfield2026.BgEditor

# Install all workspaces
npm install
cd frontend && npm install && cd ..
cd backend && npm install && cd ..
cd electron && npm install && cd ..
```

### Start Dev Server
```powershell
cd D:\Projects\Starfield-2026\src\Starfield2026.BgEditor
npm run dev
```

This starts three processes via `concurrently`:
| Process | Port | Description |
|---------|------|-------------|
| **fe** | 5173 | Vite dev server (React frontend) |
| **be** | 3001 | Fastify backend (REST + WS) |
| **el** | — | Electron shell loading Vite URL |

### Verify Startup
1. Electron window opens → Editor page with 3D viewport + AutoRig panel
2. Console shows `Backend listening on http://localhost:3001`
3. Console shows `[WS] Client connected (1 total)` — frontend connected to backend WS
4. No red errors in terminal (Vite warnings about duplicate keys are safe to ignore if fixed)

### Common Startup Issues
| Symptom | Fix |
|---------|-----|
| Port 5173/3001 in use | `npx kill-port 5173 3001` then retry |
| Electron won't start | Check `wait-on http://localhost:5173` — Vite must be up first |
| WS disconnects repeatedly | Backend crashed — check `[be]` terminal logs for stack trace |
| Blank viewport | DevTools → Console → look for Three.js errors |

---

## 5. How to Start & Test the API

### Backend REST Endpoints

| Method | Endpoint | Purpose |
|--------|----------|---------|
| `GET` | `/ws` | WebSocket connection |
| `GET` | `/api/manifests?dir=...` | Scan folder for manifests |
| `GET` | `/api/manifests/read?dir=...` | Read + normalize manifest |
| `POST` | `/api/load-model` | Load model into frontend |
| `POST` | `/api/compare-models` | Side-by-side comparison |
| `POST` | `/api/generate-rig` | Generate skeleton from template |
| `POST` | `/api/clear-rig` | Clear skeleton |
| `POST` | `/api/viewport` | Update camera/lighting/bg (**NEW**) |
| `POST` | `/api/select-clip` | Select animation clip |
| `POST` | `/api/tag-clip` | Tag clip with semantic name |
| `POST` | `/api/auto-tag` | Auto-tag all clips |
| `POST` | `/api/save-manifest` | Save manifest to disk |
| `POST` | `/api/playback` | Play/pause animation |
| `POST` | `/api/navigate` | Navigate to page |

### MCP Tools (Full Inventory)

| Tool | Description |
|------|-------------|
| `load_model` | Load manifest/dae into viewer |
| `load_path` | Scan folder, load all manifests |
| `compare_models` | Side-by-side model comparison |
| `clear_all` | Reset all frontend state |
| `generate_rig` | Generate Rigify skeleton |
| `clear_rig` | Clear current skeleton |
| `navigate_page` | Navigate to editor/animations/tools/extraction |
| `select_model` | Select model by index |
| `select_clip` | Select animation clip by index |
| `tag_clip` | Tag clip with semantic name |
| `auto_tag` | Auto-tag all clips |
| `save_manifest` | Save manifest to disk |
| `set_playback` | Play/pause animation |
| `set_viewport` | Update camera/lighting/background (**NEW**) |
| `save_screenshot` | Capture 3D viewport as PNG |
| `save_ui_screenshot` | Capture full app window as PNG |
| `render_model` | Headless multi-angle render |

### Testing Viewport Controls
```powershell
# Via MCP tool (from AI assistant)
set_viewport({ azimuth: 90, distance: 4 })            # Side view, zoomed
set_viewport({ bgColor: "#2a1a3a", lightIntensity: 0.3 })  # Purple, dim

# Via REST
$body = '{"azimuth":90,"distance":4}' 
Invoke-RestMethod -Method POST -Uri http://localhost:3001/api/viewport -ContentType "application/json" -Body $body
```

---

## 6. Known Issues & Error-Solving Strategies

### Issue 1: Vite HMR Breaks WS Connection During Edits
**Symptom:** MCP tools return "success" but nothing happens in the UI. After editing a frontend file, the old WS client disconnects and the new one reconnects, but events sent during the reconnection window are lost.  
**Strategies:**
1. **WS reconnect with replay** — Buffer the last N events in the backend. On WS reconnect, send them to the new client. Guarantees no lost events.
2. **Frontend health check** — Add a periodic `ping` from MCP tool before sending real commands. If the ping fails, wait and retry.
3. **Debounce HMR** — Configure Vite's `server.hmr` to batch updates and reduce reconnection frequency.

### Issue 2: Viewport Apply Effect Creates Feedback Loop Risk
**Symptom:** Potential infinite loop: OrbitControls `change` → `updateViewport()` → store change → `[viewport]` useEffect → camera update → OrbitControls `change` → ...  
**Strategies:**
1. **`applyingRef` guard** (✅ implemented) — Set a ref before programmatic updates, skip sync during guard.
2. **Separate user vs programmatic state** — Use two stores: `viewportTarget` (set by MCP/API) and `viewportCurrent` (set by OrbitControls). Only `viewportTarget` triggers the apply effect.
3. **Event source tagging** — Attach `{ source: 'mcp' | 'user' }` to viewport updates. Only sync back to store for `source: 'user'`.

### Issue 3: Electron-Store Persistence Race on Crash
**Symptom:** If the app crashes during a debounced write, the viewport state may be lost or partially written.  
**Strategies:**
1. **Write-ahead journal** — Write to a temp file first, then atomic rename. Prevents partial writes.
2. **Immediate write on significant changes** — Skip debounce for large delta changes (e.g., bgColor, fov) that are clearly intentional.
3. **Fallback to defaults** — If `storeGet('viewport')` returns malformed data, fall back to `DEFAULT_VIEWPORT` instead of crashing.

### Issue 4: GPU Memory Leaks on Model/Rig Switching
**Symptom:** Memory usage grows over time when generating/clearing rigs or loading/unloading models.  
**Strategies:**
1. **Explicit dispose pipeline** — On rig clear: `skeletonGroup.traverse(child => { child.geometry?.dispose(); child.material?.dispose() })`.
2. **WeakRef tracking** — Use `FinalizationRegistry` to log when Three.js objects are GC'd. Detect leaks.
3. **Periodic forceGC** — In dev mode, call `renderer.info.memory` to log texture/geometry counts and alert when thresholds are exceeded.

---

## 7. Architecture Updates

### New: Viewport Control Pipeline
```
MCP set_viewport({ azimuth: 90 })
  → POST /api/viewport  (Fastify)
    → broadcast('viewport:update', { settings })  (WS)
      → useBackendEvents: case 'viewport:update'
        → store.updateViewport({ azimuth: 90 })  (Zustand partial merge)
          → Viewport.tsx [viewport] useEffect
            → sphericalToCartesian() → camera.position.set()
            → controls.target.set() + controls.update()
            → light intensities + renderer.setClearColor()
            → electronAPI.storeSet('viewport', vp)  (persist)
```

### Bidirectional Sync (User Orbit)
```
User drags viewport (mouse/touch)
  → OrbitControls 'change' event
    → debounce 150ms
      → cartesianToSpherical(camera, target)
        → store.updateViewport(spherical)
          → electronAPI.storeSet('viewport', vp)  (persist)
```

### Hydration on Launch
```
App mount → storeGet('viewport') 
  → updateViewport(saved)
    → [viewport] useEffect applies to Three.js
```

### ViewportSettings Type
```typescript
interface ViewportSettings {
  azimuth: number       // 0 = front, 90 = right, 180 = back
  elevation: number     // 0 = level, 90 = top-down, -30 = below
  distance: number      // zoom distance from target
  panX: number          // target X offset
  panY: number          // target Y offset  
  panZ: number          // target Z offset
  fov: number           // camera field of view
  lightIntensity: number   // key/fill/rim multiplier (0–2)
  ambientIntensity: number // hemisphere light (0–2)
  bgColor: string       // hex color
}

const DEFAULT_VIEWPORT: ViewportSettings = {
  azimuth: 35, elevation: 25, distance: 8,
  panX: 0, panY: 1, panZ: 0,
  fov: 45, lightIntensity: 1.0, ambientIntensity: 0.8,
  bgColor: '#1e1e1e',
}
```

---

## 8. Quick Wins

1. **Camera preset buttons** (~15 min) — Add Front/Side/Top/Back buttons to AutoRig panel that call `updateViewport({ azimuth: 0/90/top/180 })`. Instant UX improvement for model inspection.

2. **Smooth camera transitions** (~30 min) — Instead of snapping, lerp from current position to target over 500ms using `requestAnimationFrame`. Makes viewport changes feel professional.

3. **Grid visibility toggle** (~10 min) — Add `showGrid: boolean` to `ViewportSettings`. Hide/show the `GridHelper` in the apply effect. Useful for clean screenshots.

4. **Viewport reset MCP tool** (~5 min) — Already have `resetViewport()` in the store. Just add a `reset_viewport` MCP tool that calls it. One-liner.

---

## 9. Key Lessons

1. **Missing REST endpoints are invisible failures.** The `generate_rig` MCP tool returned 200 from the MCP layer (because the POST was "successful" from node-fetch's perspective — it got a response) but the backend was returning 404. The MCP tool didn't propagate the error properly. Always check the HTTP status code in MCP tool handlers.

2. **Vite HMR breaks WebSocket connections.** Every file edit triggers an HMR update that drops and reconnects the WS client. Events sent during the ~100ms reconnection window are lost. This makes debugging via "edit → test → observe" unreliable. The workaround is to wait 2–3 seconds after a file edit before testing MCP tools.

3. **Spherical coordinates are the right abstraction for camera control.** Raw cartesian (x,y,z) position is confusing for consumers — they have to mentally map coordinates. Spherical (azimuth, elevation, distance) maps directly to "rotate left/right", "tilt up/down", "zoom in/out". OrbitControls already uses spherical internally, making the conversion trivial.

4. **Partial updates via shallow merge prevent state corruption.** `{ ...state.viewport, ...patch }` ensures that changing one field doesn't wipe others. This is critical for MCP tools where the caller shouldn't need to know the current state to make a change.

5. **Bidirectional sync needs a feedback guard.** Without `applyingRef`, the cycle is: programmatic update → OrbitControls change event → store update → useEffect → programmatic update → ∞. The `requestAnimationFrame` release is important — it ensures the guard spans the full update cycle including Three.js's internal event dispatching.

6. **Duplicate Zustand properties are silent.** JavaScript objects allow duplicate keys — the last one wins. This means two `clearAll:` definitions in a Zustand store don't error, they just silently use the last one. Vite/esbuild warns but doesn't error. Always search for duplicates after multi-edit sessions.
