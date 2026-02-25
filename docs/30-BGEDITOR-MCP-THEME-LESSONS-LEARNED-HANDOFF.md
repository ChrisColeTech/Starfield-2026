# 30 — BgEditor MCP Tools & Theme System — Lessons Learned & Handoff

**Date:** 2026-02-25  
**Session Focus:** Semantic theme refactor (V6 Style Guide) + MCP tool fixes/additions  
**Status:** Theme migration complete, MCP `load_path` fixed, `compare_models` added, Viewport crash fixed

---

## 1. What We Accomplished

### Semantic Theme System (✅ Complete)
Migrated all 17 frontend `.tsx` files from hardcoded hex colors and custom `--color-*` CSS variables to a semantic HSL-based theme system.

| Layer | What Changed |
|-------|-------------|
| `index.css` | Rewrote from scratch — HSL vars (Zinc gray + Indigo accent) mapped to Tailwind `@theme` tokens |
| Components (8) | `ToastContainer`, `Header`, `Sidebar`, `Viewport`, `DropZone`, `TexturePanel`, `ColorControls`, `AnimationPanel` |
| Pages (4) | `EditorPage`, `AnimationsPage`, `ExtractionPage`, `ToolsPage` |
| Other (5) | `ExportPanel`, `InfoBar`, `HeaderMenuBar` + remaining files |

**Theme classes now available:** `bg-background`, `bg-card`, `text-foreground`, `text-muted-foreground`, `border-border`, `bg-primary`, `text-primary-foreground`, `text-destructive`, `bg-muted`, `hover:bg-muted`, `accent-primary`, etc.

### MCP Tool: `load_path` (✅ Fixed)
- **Was broken:** Tried to find `manifest.json` at folder root (fails for folders with subfolders)
- **Fix:** Uses `collectManifests()` for recursive scanning, sends full manifest list in WS payload
- **Result:** Scans folder → all manifests sent to UI → first model auto-loads

### MCP Tool: `compare_models` (✅ New)
- Accepts `pathA` and `pathB` (manifest.json or model folder paths)
- Backend reads and normalizes both manifests via `readAndNormalizeManifest()`
- Sends both manifests to UI via `model:compare` WS event
- Frontend populates model browser, auto-loads first

### Backend Refactoring (✅ Complete)
| Change | File |
|--------|------|
| Extracted `normalizeManifest()` helper | `index.ts` |
| Extracted `readAndNormalizeManifest()` helper | `index.ts` |
| Exported `collectManifests()` | `manifests.ts` |
| Added `/api/compare-models` endpoint | `index.ts` |
| Fixed folder handler in `/api/load-model` | `index.ts` |

### Frontend Store (✅ Complete)
- Added `loadManifestList(manifests, dir?)` action — populates browser + auto-loads first
- Updated `useBackendEvents.ts` — handles `model:compare` and improved `model:load` folder handling

### Bug Fix: Viewport Crash (✅ Fixed)
- **Symptom:** `Cannot read properties of undefined (reading 'uuid')` at `Viewport.tsx:232`
- **Root cause:** `mixer.clipAction(clip)` called with `clip = undefined` when `activeClipIndex` was set before animations loaded
- **Fix:** Added `if (!clip) return` guard

### Other
- Fixed Sidebar active page indicator (`border-none` was overriding `border-l-2`)
- DevTools re-enabled in Electron `main.js`

---

## 2. What Work Remains

### Critical (MCP Tooling)
1. **Additive model loading** — `load_model`/`compare_models` currently wipes the existing model list and reloads. Should **append** to the existing set instead, preserving what's already loaded.
2. **`clear_all` MCP tool** — Reset the UI store: clear scene, manifests, animations, scan state. No UI equivalent exists yet.
3. **`save_screenshot` MCP tool** — Save a PNG of the currently rendered 3D model viewport (WebGL canvas capture → backend save).
4. **`save_ui_screenshot` MCP tool** — Full app screenshot (Electron `capturePage()` → backend save). Enables visual validation during development.

### Important (Stability)
5. **WS payload size** — `load_path` on large folders sends hundreds of normalized manifests over WS. Could crash the frontend or cause OOM. Consider pagination or lazy loading.
6. **Error boundaries** — The Viewport crash revealed that React doesn't recover from Three.js errors. An error boundary around `<Viewport>` would prevent full-page crashes.
7. **Git push kills Electron** — Every `git push` terminates the Electron process. The dev workflow loses state frequently.

### Nice to Have
8. **Dark/light mode toggle** — CSS vars are set up for `.dark` class but no toggle exists in the UI.
9. **Theme preview page** — A debug page showing all theme tokens with swatches, useful for design iteration.

---

## 3. Optimizations — Prime Suspects

### Suspect 1: Manifest Normalization Duplication
**Problem:** `normalizeManifest()` in `index.ts` duplicates logic from `manifests.ts` route handler. The manifest route's `GET /api/manifests/read` does its own normalization with a slightly different structure.
**Fix:** Extract a single `normalizeManifest()` into a shared util. Both `index.ts` and `manifests.ts` should import from the same source.

### Suspect 2: WS Payload Bloat
**Problem:** `collectManifests()` returns raw manifest data including all clips, models, and metadata. For a folder with 100+ character models, the WS JSON payload can be megabytes.
**Fix:** Send lightweight summaries (name, dir, clipCount, modelFile) in the WS payload. Load full manifest data on-demand when a model is selected.

### Suspect 3: Scene Teardown Leaks
**Problem:** When loading a new model, the old Three.js scene is replaced in the store but may not be properly disposed. GPU resources (geometries, materials, textures) can leak.
**Fix:** In `loadFolder()` and `loadModelOnly()`, explicitly call `scene.traverse(child => { child.geometry?.dispose(); child.material?.dispose() })` on the old scene before replacing it.

### Suspect 4: Animation Effect Re-runs
**Problem:** The `useEffect` at `Viewport.tsx:220` depends only on `[activeClipIndex]` but reads `storeAnimations` and `animationPlaying` from the outer scope. These stale closures can cause incorrect behavior.
**Fix:** Add `storeAnimations` and `animationPlaying` to the dependency array, or use refs for values that shouldn't trigger re-runs.

---

## 4. Step-by-Step: Getting the App Fully Working

### Prerequisites
```
Node.js 22+
npm 10+
```

### Install & Build
```powershell
cd D:\Projects\Starfield-2026\src\Starfield2026.BgEditor

# Install all workspaces
npm install
cd frontend && npm install && cd ..
cd backend && npm install && cd ..
cd electron && npm install && cd ..
```

### Start the Dev Server
```powershell
cd D:\Projects\Starfield-2026\src\Starfield2026.BgEditor
npm run dev
```
This starts three processes via `concurrently`:
- **fe** — Vite dev server on `http://localhost:5173`
- **be** — Backend API on `http://localhost:3001`
- **el** — Electron shell loading the Vite URL

### Verify
1. Electron window opens with the BgEditor UI
2. Navigate pages: Editor (cube icon), Animations (film icon), Extraction (box icon), Tools (wrench icon)
3. Active page indicator shows indigo left border on sidebar
4. DevTools should open automatically (can be disabled in `electron/main.js`)

### Running MCP Server Separately
The MCP server (`mcp-server.ts`) runs as a separate stdio process for AI tool integration. It is NOT started by `npm run dev`. AI coding assistants connect to it via the `.gemini/settings.json` MCP configuration.

---

## 5. How to Start & Test the API

### Backend API Endpoints

| Method | Endpoint | Purpose |
|--------|----------|---------|
| `GET` | `/ws` | WebSocket connection for real-time events |
| `GET` | `/api/manifests?dir=...` | Scan folder for manifests |
| `GET` | `/api/manifests/read?dir=...` | Read + normalize a single manifest |
| `POST` | `/api/load-model` | Load model(s) into frontend via WS |
| `POST` | `/api/compare-models` | Compare two models in frontend |
| `POST` | `/api/manifests/generate` | Generate manifests from model folders |
| `GET` | `/serve/<token>/<file>` | Serve model/texture files from disk |
| `POST` | `/api/save-render` | Save rendered images to disk |
| `POST` | `/api/render-angles` | Server-side multi-angle render |

### Testing `load_path`
```powershell
# Via MCP tool
# load_path with path: "D:/Projects/Starfield-2026/src/Starfield2026.Assets/Models/Characters/PZLA/man"

# Via REST
$body = @{path="D:/path/to/models"; type="folder"} | ConvertTo-Json
Invoke-RestMethod -Method POST -Uri http://localhost:3001/api/load-model -ContentType "application/json" -Body $body
```

### Testing `compare_models`
```powershell
$body = @{
  pathA="D:/path/to/model_a/manifest.json"
  pathB="D:/path/to/model_b/manifest.json"
} | ConvertTo-Json
Invoke-RestMethod -Method POST -Uri http://localhost:3001/api/compare-models -ContentType "application/json" -Body $body
```

---

## 6. Known Issues & Strategies

### Issue 1: `load_model` / `compare_models` Wipes Existing State
**Current:** Calling load resets the entire manifest list and replaces the scene.
**Strategies:**
1. **Additive `loadManifestList`** — Change the store action to `set(prev => ({ manifests: [...prev.manifests, ...newManifests] }))` instead of replacing.
2. **Dedup by `dir`** — Before appending, filter out manifests with matching `dir` to avoid duplicates.
3. **"Replace" vs "Append" mode** — Add a `mode` param to the WS event: `{ mode: 'replace' | 'append' }`. Default to `append` for `load_model`, `replace` for `clear_all`.

### Issue 2: No Way to Clear State
**Current:** No MCP tool or UI button to reset the editor.
**Strategies:**
1. **`clear_all` MCP tool** → `POST /api/clear` → WS broadcast `model:clear` → store's `resetAll()` + clear scene.
2. **UI reset button** — Add a "Clear All" button to the header or sidebar that calls the same store reset.
3. **Keybinding** — `Ctrl+Shift+R` to reset (avoid `Ctrl+R` which reloads the page).

### Issue 3: No Screenshot Capability for MCP
**Current:** No way for AI agents to see what the app looks like.
**Strategies:**
1. **`save_screenshot`** — Frontend captures WebGL canvas via `renderer.domElement.toDataURL('image/png')`, sends base64 to backend `/api/save-render`, backend writes to disk.
2. **`save_ui_screenshot`** — Electron's `mainWindow.capturePage()` returns a NativeImage. Expose via IPC + backend endpoint. Returns full-app screenshot including sidebar, header, etc.
3. **Auto-capture on model load** — After `loadFolder` completes, automatically capture a thumbnail for the manifest browser.

### Issue 4: Viewport Animation Race Condition
**Current:** Fixed with null guard, but root cause (effect deps) remains.
**Strategies:**
1. **Proper effect dependencies** — Add `storeAnimations` to the `useEffect` deps array so it re-runs when animations actually load.
2. **Ref-based animation state** — Store `storeAnimations` in a ref and only trigger animation changes from explicit user actions (clip selection), not from store subscription effects.
3. **Animation loading callback** — Have `loadFolder`/`selectClip` return a Promise, and only set `activeClipIndex` after the animation data is confirmed loaded.

---

## 7. Architecture & Quick Wins

### Current Architecture
```
BgEditor/
├── frontend/                    (React + Vite + Tailwind)
│   └── src/
│       ├── store/editorStore.ts   ← Zustand store (scene, manifests, animations)
│       ├── hooks/useBackendEvents.ts ← WS event dispatcher
│       ├── components/            ← Viewport, Sidebar, Panels
│       └── pages/                 ← Editor, Animations, Extraction, Tools
├── backend/                     (Fastify + Three.js headless)
│   └── src/
│       ├── index.ts               ← API routes, WS broadcast, model loading
│       ├── mcp-server.ts          ← MCP stdio server (AI tool bridge)
│       ├── cli-render.ts          ← Headless 3D rendering
│       └── routes/manifests.ts    ← Manifest CRUD + scanning
└── electron/                    (Electron shell)
    └── main.js                    ← Window management, IPC
```

### Data Flow
```
MCP Tool → POST /api/... → Backend reads manifests from disk
                         → Normalizes via normalizeManifest()
                         → Broadcasts WS event with complete data
                         → Frontend handleEvent() → store action → UI update
```

### Quick Wins
1. **Additive model loading** (~30 min) — Change `loadManifestList` to append instead of replace. Biggest UX improvement for the MCP workflow.
2. **`clear_all` tool** (~15 min) — New endpoint + WS event + store reset. Minimal code, high utility.
3. **`save_screenshot`** (~30 min) — Canvas `toDataURL()` → backend save. Enables visual validation in AI workflows.
4. **`save_ui_screenshot`** (~20 min) — Electron `capturePage()` via IPC. Full app visibility for development.

### Architectural Improvements
1. **Shared normalization util** — Extract `normalizeManifest` from `index.ts` into a shared module. Both `index.ts` and `manifests.ts` routes import from the same source. Eliminates duplication.
2. **Scene disposal pipeline** — Add explicit Three.js resource cleanup when switching models. Currently relies on garbage collection which doesn't free GPU buffers.
3. **Error boundary around Viewport** — Wrap `<Viewport>` in a React error boundary that shows a friendly error message instead of crashing the entire app.
4. **WS event types** — Define a TypeScript enum/union for WS event types (`model:load`, `model:compare`, `model:clear`, `render:progress`, etc.) shared between backend and frontend.

---

## 8. Key Lessons

1. **Don't add abstractions prematurely.** The original `load_path` flow was: MCP → REST pre-scan → REST load → WS broadcast. It should have been: MCP → REST load → backend scans internally → WS broadcast. The extra REST call added complexity and a point of failure.

2. **WS payloads should be self-contained.** The frontend shouldn't need to make additional REST calls after receiving a WS event. The event should contain everything needed to update the UI immediately.

3. **Guard against race conditions in React effects.** The Viewport crash was caused by an effect that read store state (`storeAnimations`) that could change between when the effect was scheduled and when it ran. Always validate assumptions inside effects.

4. **`border-none` vs `border-0` in Tailwind.** `border-none` sets `border-style: none`, which overrides `border-l-2` even when placed after it. Use `border-y-0 border-r-0` to zero specific sides while keeping the left border.

5. **Git push on Windows kills Electron.** The `git push` command causes `concurrently` to restart processes, which terminates Electron. This is a dev workflow friction point — consider running the backend + frontend without Electron during active development, or using a separate terminal for git operations.
