# 32 — Viewport Controls, Editor Tools & AutoRig Panel — Lessons Learned & Handoff

**Date:** 2026-02-25  
**Session Focus:** Transform BgEditor from viewer → functional 3D skeleton editor with full MCP control  
**Status:** 9 editor feature phases + 11 MCP tools implemented. UI bugs fixed. Manipulation tools wired but need restart/verification.

---

## 1. What We Accomplished

### Editor Feature Phases (9/9 ✅)

| Phase | Feature | Key Files |
|-------|---------|-----------|
| 1 | **Bone click-to-select** — Raycaster hit-testing on skeleton spheres with highlight feedback | `Viewport.tsx` |
| 2 | **TransformControls gizmo** — Three.js gizmo attachment, G/R/S keyboard shortcuts, OrbitControls conflict resolution | `Viewport.tsx` |
| 3 | **ViewHelper** — Orientation cube in bottom-right corner | `Viewport.tsx` |
| 4 | **Camera presets** — F/S/T/B/¾ buttons for front/side/top/back/three-quarter views | `AutoRigPanel.tsx` |
| 5 | **Bone inspector** — Real-time name, parent, collection, head/tail XYZ display | `BoneInspector.tsx` (NEW) |
| 6 | **Undo/redo stack** — Ctrl+Z/Y with per-bone before/after state tracking | `editorStore.ts` |
| 7 | **Collection visibility** — Eye/EyeOff toggles per bone group, dimming hidden collections | `AutoRigPanel.tsx` |
| 8 | **Grid/axes toggles** — Checkbox controls for viewport grid and axes helper | `AutoRigPanel.tsx`, `Viewport.tsx` |
| 9 | **Box select** — Shift+drag rectangle selection, multi-bone highlighting with primary/secondary distinction | `Viewport.tsx`, `editorStore.ts` |

### MCP Tools — Editor Control (7 tools ✅)

| Tool | Type | Purpose |
|------|------|---------|
| `select_bone` | Fire-and-forget | Select bone by name or deselect (null) |
| `set_transform_mode` | Fire-and-forget | Switch gizmo: translate/rotate/scale |
| `toggle_collection` | Fire-and-forget | Toggle bone collection visibility |
| `set_grid` | Fire-and-forget | Show/hide viewport grid |
| `set_axes` | Fire-and-forget | Show/hide axes helper |
| `get_bone_info` | Request-response | Query bone head/tail XYZ, parent |
| `get_editor_state` | Request-response | Full state: selection, mode, visibility, bone count |

### MCP Tools — Skeleton Manipulation (4 tools ✅ wired, needs verification)

| Tool | Type | Purpose |
|------|------|---------|
| `set_bone_position` | Fire-and-forget | Set absolute head/tail position |
| `translate_bone` | Fire-and-forget | Move bone + optional child cascade by delta |
| `set_bone_roll` | Fire-and-forget | Set bone roll angle |
| `add_bone` | Fire-and-forget | Append new bone to skeleton |

### UI Bug Fixes (2 ✅)
- **BoneInspector** — Transform buttons now always visible, disabled until bone selected
- **Bone section collapse** — Conditional `flex-1`/`shrink-0` so collapsing works properly

### Architecture Pattern (Established)
```
MCP Tool (mcp-server.ts)
  → POST /api/xxx (index.ts Fastify REST)
    → broadcast('event:type', data) (WebSocket)
      → useBackendEvents.ts handler
        → editorStore action (Zustand)
          → React UI rerenders (Viewport rebuilds skeleton group)
```

Request-response tools (get_bone_info, get_editor_state) use a `pendingEditorQueries` map, same pattern as screenshot capture:
```
MCP → POST /api/xxx → broadcast('editor:getXxx', { requestId }) → Frontend reads store
  → POST /api/editor-query/result → Backend resolves pending promise → MCP returns JSON
```

---

## 2. What Work Remains

### Critical
1. **Manipulation tool verification** — `translate_bone`, `set_bone_position`, `set_bone_roll`, `add_bone` are fully wired but need live MCP testing after a fresh backend restart
2. **TransformControls ↔ store sync** — When user drags the gizmo to move a bone, the updated position should write back to `editorStore.skeleton[]`. Currently the gizmo moves the Three.js mesh but the store data is stale
3. **Undo stack integration** — `pushUndo()` action exists but manipulation tools don't push undo entries yet. Need to wrap `setBonePosition`/`translateBone` with automatic undo recording
4. **Delete bone tool** — No `remove_bone` MCP tool exists yet

### Important
5. **Multi-select manipulation** — `translate_bone` only moves one bone + children. Need a `translate_selection` tool that moves all bones in `selectedBones`
6. **Bone rename** — No rename capability for bones
7. **Export skeleton** — No way to export the edited skeleton back to JSON/Blender format
8. **HMR skeleton persistence** — Skeleton state lost on HMR reload during development. Store persists but viewport doesn't re-render the skeleton group after hot reload

### Nice to Have
9. **Bone constraint visualization** — Show IK chains, symmetry pairs
10. **Bone length lock** — Option to keep bone length fixed when moving head (tail follows)
11. **Mirror mode** — Move bone on left side, automatically mirror to right side

---

## 3. Optimizations — Prime Suspects

### Suspect 1: Skeleton Re-render on Every Store Change
**Problem:** The Viewport `useEffect` that builds the skeleton group depends on `skeleton` (the entire array). Any bone position change recreates ALL sphere meshes and line segments for 159+ bones.  
**Impact:** Noticeable jitter when dragging bones or rapid translateBone calls.  
**Fix:** Instead of rebuilding the entire group, update individual mesh positions. Track bones by name via a `Map<string, THREE.Mesh>` and only reposition changed bones.

### Suspect 2: Box Select Projection Performance
**Problem:** On `pointerup` with shift, every bone sphere's world position is projected to screen space via `Vector3.project()`. For 159 bones this runs ~159 matrix multiplications.  
**Impact:** Slight lag on large skeletons when completing a box select.  
**Fix:** Only project visible bones (skip `hiddenCollections`). Cache screen-space positions and invalidate when camera moves.

### Suspect 3: WS Round-trip Latency for Read Tools
**Problem:** `get_bone_info` and `get_editor_state` do a full MCP → REST → WS → frontend → REST → MCP round-trip. Each call takes ~50-200ms.  
**Impact:** Slow when the AI wants to query multiple bones sequentially.  
**Fix:** Add a `get_all_bones` tool that returns the entire skeleton in one call, avoiding per-bone round-trips. Or cache skeleton data in the backend after rig generation.

### Suspect 4: editorStore Monolith
**Problem:** Single Zustand store holds ALL state: model viewer, animation editor, rig editor, scan browser, viewport settings. Every action triggers all subscribers.  
**Impact:** Unrelated components re-render when editor state changes (e.g., animation panel re-renders when a bone is selected).  
**Fix:** Split into focused stores (`useRigStore`, `useViewportStore`, `useAnimationStore`) or use Zustand slices with selectors. Low priority but improves maintainability.

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

This starts 3 concurrent processes:
| Process | Port | Description |
|---------|------|-------------|
| **fe** (Vite) | 5173 | React frontend with HMR |
| **be** (Fastify) | 3001 | REST API + WebSocket hub |
| **el** (Electron) | — | Desktop shell loading Vite URL |

### Verify Startup
1. Electron window opens with BgEditor UI
2. Console shows `[WS] Connected to backend`
3. Navigate to Editor page (default `/`)
4. AutoRig panel visible on right side with Rig Template dropdown, camera presets, grid/axes checkboxes

### Generate & Test Rig
1. Select "Human" template → click "Generate Game Rig"
2. Skeleton appears in viewport (159 bones)
3. Click any bone sphere → bone highlights, BoneInspector shows details
4. Press G/R/S → gizmo changes mode
5. Shift+drag → box select multiple bones

---

## 5. How to Start & Test the API

### MCP Server
The MCP server runs as a **separate stdio process** configured in `.gemini/settings.json`. It is NOT started by `npm run dev`. AI assistants (Gemini/Claude) connect to it automatically.

### REST API Testing (PowerShell)

**Editor Control Tools:**
```powershell
# Select a bone
Invoke-RestMethod -Method POST -Uri http://localhost:3001/api/select-bone `
  -ContentType "application/json" -Body '{"name":"spine"}'

# Set transform mode
Invoke-RestMethod -Method POST -Uri http://localhost:3001/api/transform-mode `
  -ContentType "application/json" -Body '{"mode":"rotate"}'

# Get editor state (request-response)
Invoke-RestMethod -Method POST -Uri http://localhost:3001/api/editor-state

# Get bone info
Invoke-RestMethod -Method POST -Uri http://localhost:3001/api/bone-info `
  -ContentType "application/json" -Body '{"name":"spine"}'
```

**Skeleton Manipulation Tools:**
```powershell
# Translate bone + children by delta
Invoke-RestMethod -Method POST -Uri http://localhost:3001/api/translate-bone `
  -ContentType "application/json" -Body '{"name":"spine","delta":[0,0.5,0],"children":true}'

# Set absolute bone position
Invoke-RestMethod -Method POST -Uri http://localhost:3001/api/set-bone-position `
  -ContentType "application/json" -Body '{"name":"hand.L","head":[1,2,0]}'

# Add a new bone
Invoke-RestMethod -Method POST -Uri http://localhost:3001/api/add-bone `
  -ContentType "application/json" -Body '{"bone":{"name":"custom_bone","head":[0,0,0],"tail":[0,0.2,0],"parent":"spine"}}'
```

**Verify via screenshot:**
```powershell
# Take viewport screenshot (via MCP)
# save_screenshot → saves PNG to backend/outputs/
```

### Full API Endpoint Reference

| Method | Endpoint | Purpose |
|--------|----------|---------|
| `POST` | `/api/select-bone` | Select/deselect bone |
| `POST` | `/api/transform-mode` | Set gizmo mode |
| `POST` | `/api/toggle-collection` | Toggle bone group visibility |
| `POST` | `/api/set-grid` | Show/hide grid |
| `POST` | `/api/set-axes` | Show/hide axes |
| `POST` | `/api/bone-info` | Get bone details (round-trip) |
| `POST` | `/api/editor-state` | Get full state (round-trip) |
| `POST` | `/api/editor-query/result` | Frontend callback for round-trips |
| `POST` | `/api/set-bone-position` | Set bone head/tail |
| `POST` | `/api/translate-bone` | Move bone by delta |
| `POST` | `/api/set-bone-roll` | Set bone roll |
| `POST` | `/api/add-bone` | Add new bone |
| `POST` | `/api/generate-rig` | Generate rig from template |
| `POST` | `/api/clear-rig` | Clear skeleton |

---

## 6. Known Issues & Strategies

### Issue 1: HMR Loses Skeleton State
**Symptom:** After a hot reload, "Bones (0)" shows in panel and skeleton disappears from viewport, even though `editorStore.skeleton` still has data.  
**Root cause:** Viewport's Three.js scene is recreated on HMR, but the `useEffect` that builds the skeleton group doesn't re-trigger because the `skeleton` ref hasn't changed.  
**Strategies:**
1. **Force re-render on mount** — Add a `mountId` counter to the store, increment on HMR. Include in Viewport's skeleton `useEffect` deps
2. **Persist skeleton to sessionStorage** — On store update, save skeleton to sessionStorage. On mount, check and restore
3. **Accept it** — This only affects development. Production builds don't have HMR. Document as known dev-time artifact

### Issue 2: TransformControls Don't Write Back to Store
**Symptom:** Dragging the gizmo visually moves the bone mesh, but `get_bone_info` returns the original position.  
**Root cause:** TransformControls modify the Three.js mesh transform, but nothing propagates that change back to `editorStore.skeleton[]`.  
**Strategies:**
1. **`change` event listener** — Listen for TransformControls `objectChange` event, read the mesh's world position, and call `setBonePosition()` on the store
2. **`mouseUp` sync** — Only sync position when the user releases the gizmo (less frequent updates, better performance)
3. **Viewport-to-store bridge** — Create a utility that watches TransformControls state and batches store updates

### Issue 3: Undo Stack Not Connected to Manipulation
**Symptom:** Ctrl+Z doesn't undo bone moves made via MCP tools.  
**Root cause:** `pushUndo()` exists but manipulation actions don't call it.  
**Strategies:**
1. **Wrap actions** — Modify `setBonePosition`/`translateBone` to automatically `pushUndo()` before making changes
2. **MCP-level undo** — Have the MCP tool read the "before" state, make the change, and push undo in a single atomic operation
3. **Command pattern** — Replace direct store mutations with a command queue that automatically records undo entries

### Issue 4: Request-Response Tools Timeout on Slow Frontend
**Symptom:** `get_bone_info` or `get_editor_state` returns "Timeout" if the frontend takes >5s to respond.  
**Root cause:** The `pendingEditorQueries` timeout is 5000ms, which can be tight if the frontend is busy rendering.  
**Strategies:**
1. **Increase timeout** — 10s default with configurable override
2. **Retry with backoff** — MCP tool retries once on timeout
3. **Cache in backend** — After `generate_rig`, cache skeleton data in the backend. Serve `bone-info` and `editor-state` directly without WS round-trip for cached data

---

## 7. New Architecture & Quick Wins

### Architecture Changes Made This Session

**Before:** BgEditor was a read-only viewer with model loading, animation playback, and screenshot tools.

**After:** BgEditor is now a functional 3D skeleton editor with:
- Interactive bone selection (click, shift+drag box select)
- Transform gizmo with keyboard shortcuts
- MCP-driven programmatic control (11 new tools)
- Skeleton manipulation (add/move/rotate bones)
- Property inspection panel

```
NEW: Editor Page Architecture
┌─────────────────────────────────────┐
│ Viewport.tsx                        │
│ ├── Three.js Renderer               │
│ ├── OrbitControls                    │
│ ├── TransformControls (gizmo)       │
│ ├── ViewHelper (orientation cube)   │
│ ├── Raycaster (bone click)          │
│ ├── Box-select (shift+drag)         │
│ ├── Grid + Axes (toggleable)        │
│ └── SkeletonRenderer (spheres+lines)│
├─────────────────────────────────────┤
│ AutoRigPanel.tsx                    │
│ ├── Rig Template selector           │
│ ├── Camera preset buttons (F/S/T/B/¾)│
│ ├── Grid/Axes checkboxes            │
│ ├── BoneCollectionsList (with eye)  │
│ └── BoneInspector.tsx               │
│     ├── Transform mode buttons (G/R/S)│
│     └── Bone details (name/parent/XYZ)│
└─────────────────────────────────────┘
```

### Quick Wins

| Win | Effort | Impact | Description |
|-----|--------|--------|-------------|
| **Industry-standard infinite grid** | 30 min | High | Replace basic `GridHelper` with infinite grid shader (see below) |
| `get_all_bones` tool | 15 min | High | Return entire skeleton as JSON — eliminates per-bone round-trips |
| TransformControls writeback | 30 min | Critical | Sync gizmo changes to store so `get_bone_info` returns actual positions |
| `remove_bone` tool | 15 min | Medium | Filter bone out of skeleton array — completes CRUD |
| Undo auto-recording | 20 min | High | Wrap manipulation actions with automatic `pushUndo()` |
| `export_skeleton` tool | 30 min | High | Export edited skeleton to JSON file — enables Blender import |
| `mirror_bone` tool | 20 min | Medium | Mirror .L bone to .R or vice versa — common rig editing operation |

### Quick Win: Industry-Standard Infinite Grid

**Current:** Basic `THREE.GridHelper(10, 10)` — fixed-size flat grid that ends abruptly, doesn't subdivide on zoom, no axis coloring. Looks like a prototype.

**Target:** The infinite grid seen in Blender, Unity, and Unreal — fading to horizon, multi-scale subdivision, colored XZ axes (red/blue).

**Implementation approach:**
1. Use a custom `ShaderMaterial` on a large plane with the [Three.js InfiniteGridHelper pattern](https://github.com/Fyrestar/THREE.InfiniteGridHelper)
2. The fragment shader draws grid lines procedurally based on world-space coordinates using `fwidth()` for anti-aliasing
3. Two grid scales: major (1m) and minor (0.1m) with the minor fading in as camera gets closer
4. Alpha fades to 0 at distance — grid appears infinite but doesn't clutter the far field
5. X-axis line colored red, Z-axis line colored blue (industry convention)
6. Grid plane sits at Y=0, automatically visible from any camera angle

**Key shader technique:**
```glsl
// In fragment shader — procedural grid with LOD
vec2 coord = worldPos.xz * scale;
vec2 grid = abs(fract(coord - 0.5) - 0.5) / fwidth(coord);
float line = min(grid.x, grid.y);
float alpha = 1.0 - min(line, 1.0);
alpha *= max(0.0, 1.0 - length(worldPos.xz) / fadeDistance);  // distance fade
```

**Why this matters:** The grid is the most visually prominent element in the viewport. An industry-standard grid instantly makes the tool feel professional and gives users spatial reference for bone positions and scale.

---

## 8. Key Lessons

1. **The 3-layer MCP pipeline is rock-solid.** Every new tool follows the exact same pattern: `server.tool()` → `POST /api/xxx` → `broadcast()` → `handleEvent()` → store action. Adding a new tool is purely mechanical — 4 touch points, all predictable.

2. **Immutable store updates trigger viewport re-renders automatically.** Returning a new `skeleton` array from Zustand causes React to re-render, which rebuilds the Three.js scene. No manual invalidation needed. This makes manipulation tools trivial to implement.

3. **Request-response WS patterns work but add complexity.** The `pendingEditorQueries` map + timeout + callback endpoint is 3x the code of a fire-and-forget tool. For frequently-queried data, caching in the backend would be simpler and faster.

4. **HMR and Three.js don't play well together.** Three.js scene state (renderer, controls, skeleton group) is inherently imperative and lives outside React's reconciliation. HMR replaces the React component but the Three.js context is lost. Accept this as a dev-time artifact.

5. **Flex layout gotcha: `flex-1` always takes space even when content is hidden.** A collapsed section with `flex-1` still occupies vertical space, preventing content below from moving up. Fix: conditionally apply `flex-1` only when the section is expanded, use `shrink-0` when collapsed.

6. **Disabled buttons should be visible, not hidden.** Hiding UI elements until a prerequisite is met (like selecting a bone) confuses users. Better pattern: show the buttons but disable them with reduced opacity and no-cursor, providing visual affordance of what's possible.

7. **`z.tuple([z.number(), z.number(), z.number()])` in Zod for MCP schemas.** This is the correct way to define fixed-length arrays like `[x, y, z]` position vectors in MCP tool parameters. Using `z.array(z.number())` doesn't convey the fixed length to AI assistants.
