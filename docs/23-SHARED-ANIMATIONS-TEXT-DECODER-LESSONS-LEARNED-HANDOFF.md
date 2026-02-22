# 23 - Shared Animation System & Game Text Decoder: Lessons Learned Handoff

**Date:** 2026-02-22
**Scope:** Shared animation retargeting system, bone mapping, procedural animation R&D, movement tuning, Sun/Moon game text XOR decryption, trainer class gender mapping
**Status:** Animation retargeting system built and compiles clean; text decoder functional (127 entries decoded); runtime testing of shared animations not yet verified

---

## 1. What We Accomplished

### Shared Animation Retargeting System (New Architecture)
- **Three-mode animation loading** via `AnimationLoadMode` enum:
  - `Own` — load only the model's own clips from its manifest
  - `FillMissing` — load own clips first, then fill gaps from a shared reference character (default)
  - `SharedOnly` — ignore own clips entirely, all animations come from shared reference
- Configurable `FillTags` set (defaults to `{"Jump", "Land"}`) controls which tags get filled
- `SharedAnimationFolder` points to `tr0001_00` (male hero) as the reference skeleton

### BoneMapping.cs (New File)
- Auto-detects skeleton family by checking for known bone names
- Returns `null` for Sun-Moon skeletons (no remap needed — same naming convention)
- Returns Sun-Moon → Scarlet name map (~30 entries) for Scarlet skeletons
- Covers: spine, arms, hands, fingers, hips, legs, feet, head, neck, jaw, eyes

### ColladaSkeletalLoader — Retargeted Loading
- New `LoadClipRetargeted(path, skeleton, boneNameMap, sourceName)` overload
- Translates animation channel target bone names through the mapping dictionary before resolving to target rig bone indices
- Falls back to original bone name if no mapping entry exists (graceful degradation)

### SkeletalAnimator — RebuildPose()
- New public method that recomputes `WorldPose` and `SkinPose` from `LocalPose`
- Enables external code to modify `LocalPose` entries and see results (used during procedural animation R&D)

### Jump Animation in OverworldCharacter
- `Update()` now checks `isGrounded` parameter and selects "Jump" tag when airborne
- Added `Jump` and `Land` patterns to `SplitModelAnimationSetLoader.TagPatterns`
- Jump/Land clips sourced from shared reference when model lacks its own

### Movement Tuning (PlayerController)
- Walk speed: 12 → **6**
- Run speed: 22 → **12**
- Jump force: 32 → **18**

### MiniToolbox: GARC Dump Mode
- Added `--dump` flag to `GarcCommand` for raw binary entry extraction
- Decompresses LZSS per entry, writes raw bytes to `entry_NNNNN.bin`
- Used to extract trainer class data (`a/1/0/1`) and game text (`a/0/3/2`)

### MiniToolbox: Text Decoder (New Command)
- `TextDecodeCommand.cs` registered as `minitoolbox text`
- Implements pk3DS-compatible XOR decryption: `KEY_BASE=0x7C89`, `KEY_ADVANCE=0x2983`
- Correctly parses section header (4-byte section length before line metadata)
- Line length is in **characters (ushorts)**, not bytes — key bug fix from initial implementation
- Supports `--entry N` for single-entry console output or batch decode to `.txt` files
- Successfully decoded all 127 English game text entries from Sun/Moon

### Trainer Class Gender Mapping (Research Complete)
- **Entry 111** in game text GARC `a/0/3/2` = trainer class names (224 entries)
- Field model ID mapping: `tr{N:0000}_00` → text index `N-1`
- Gender derivable from class names and pairing pattern:
  - Paired entries at consecutive indices (e.g., Youngster/Lass, Ace Trainer M/F)
  - Explicit names: "Punk Guy" = male, "Punk Girl" = female, "Beauty" = female, "Black Belt" = male
- 126 field models extracted across tr0001–tr1010 range
- Full decoded text dump at `Starfield2026.Tests/sun-moon-dump/spica-exported/game_text_decoded/`

---

## 2. What Work Remains

### Critical (Must Complete)
1. **Runtime test shared animation retargeting** — FillMissing mode with Jump/Land has never been run. Need to verify bone mapping produces correct poses.
2. **Set up SharedAnimations folder structure** — Currently pointing directly at `tr0001_00`. Should be `Models/SharedAnimations/humanoid/` with curated reference clips.
3. **Build gender lookup table** — Map each `tr{N}_00` to male/female using decoded trainer class names. Use this to select male vs female shared animation reference.
4. **Verify BoneMapping completeness** — The Sun-Moon → Scarlet map covers ~30 bones. Scarlet models may have bones not in the map that would be silently skipped.

### Important (Should Complete)
5. **Two shared animation references** — One male (tr0001_00), one female (tr0002_00) skeleton. Characters pick based on gender lookup.
6. **Test SharedOnly mode** — Verify all animations can come from shared reference for maximum storage savings.
7. **Add more tag patterns** — Current TagPatterns cover 13 tags. Field models have up to 127 animation slots (MapOverworldSlot). Many are unmapped.
8. **Validate text decoder on other languages** — Only tested English (`a/0/3/2`). Other language GARCs should use same XOR algorithm.

### Nice to Have
9. **Procedural animation system** — R&D proved bones CAN be moved programmatically (legs responded to rotation). Axis detection for arbitrary skeletons was the blocker. Could revisit with per-bone axis metadata.
10. **Animation blending/crossfade** — Currently snaps between clips. Smooth transitions would look much better.

---

## 3. Optimizations — Prime Suspects

### 3a. Bone Mapping Cache
**Current:** `BoneMapping.GetMapForRig()` is called per shared clip load. For SharedOnly mode with 20+ clips, the same detection + dictionary allocation happens repeatedly.
**Fix:** Cache the result per `SkeletonRig` instance (add a `Dictionary<SkeletonRig, Dictionary<string,string>?>` static cache).

### 3b. Shared Manifest Double-Parse
**Current:** `LoadSharedClips()` reads and deserializes the shared reference `manifest.json` every time a character is loaded.
**Fix:** Cache the parsed `ManifestData` for the shared folder. It never changes at runtime.

### 3c. Animation Clip Memory
**Current:** Each character loads its own copy of shared clips (Jump, Land). If 50 characters all fill Jump from the same source, that's 50 copies of the same keyframe data in memory.
**Fix:** Implement a `SharedClipCache` keyed by `(clipPath, sourceName)`. Return the same `SkeletalAnimationClip` instance for identical source files. Clips are immutable after load so sharing is safe.

### 3d. COLLADA Parsing Performance
**Current:** `ColladaSkeletalLoader` uses `XDocument` (full DOM parse) for every clip file. This is the slowest part of character loading.
**Fix:** For retargeted clips that are loaded frequently, consider pre-processing to a binary format (bone index + keyframes only, no XML overhead). Or use `XmlReader` streaming for clip-only loads where we don't need the full DOM.

---

## 4. Step-by-Step: Getting the App Fully Working

### Prerequisites
- .NET 9.0 SDK installed
- Solution: `D:\Projects\Starfield-2026\src\Starfield2026.sln`
- Assets: `D:\Projects\Starfield-2026\src\Starfield2026.Assets\Models\Characters\sun-moon\field\` (126 model folders with manifests + DAE + clips)

### Steps

1. **Build all projects**
   ```bash
   cd D:/Projects/Starfield-2026/src
   dotnet build Starfield2026.sln
   ```
   Expect 0 errors, warnings are OK (nullable, unused vars).

2. **Verify shared animation reference exists**
   ```bash
   ls Starfield2026.Assets/Models/Characters/sun-moon/field/tr0001_00/
   ```
   Must contain `manifest.json`, `model.dae`, and `animations/` or `clips/` folder with clip DAE files.

3. **Run the 3D Model Loader**
   ```bash
   dotnet run --project Starfield2026.3DModelLoader
   ```
   - Window opens with grid floor and character model
   - WASD to move, Shift to run, Space to jump
   - Tab to open character select overlay
   - Escape to quit

4. **Verify animation loading in logs**
   Check `modelloader.log` in the output directory for:
   ```
   [AnimSet] Loading animation set from: ... (mode=FillMissing)
   [AnimSet] Shared 'Jump': N tracks, remap=false
   ```
   If `remap=true` appears, bone mapping is active (Scarlet skeleton detected).

5. **Test jump animation**
   Press Space. Character should play Jump clip (from shared reference if model lacks its own). If character T-poses during jump, the shared clip failed to load or bone mapping is wrong.

6. **Test character switching**
   Press Tab, select different characters. Verify each loads without crash. Check log for errors.

7. **Run MiniToolbox text decoder** (separate tool)
   ```bash
   cd D:/Projects/Starfield-2026/src/Starfield2026.MiniToolbox
   dotnet run --project src/MiniToolbox.App -c Release -- text -i <path-to-dumped-entries> -e 111
   ```
   Should output trainer class names.

---

## 5. How to Start/Test

### 3D Model Loader (Main App)
```bash
cd D:/Projects/Starfield-2026/src
dotnet run --project Starfield2026.3DModelLoader
```
**Controls:**
| Key | Action |
|-----|--------|
| WASD | Move |
| Shift | Run |
| Space | Jump |
| Tab | Character select |
| Escape | Quit |
| Mouse | Camera rotation (hold right click) |

**Logs:** `modelloader.log` in executable directory. Contains detailed animation loading info, bone counts, clip durations, tag assignments.

### MiniToolbox (CLI Tool)
```bash
cd D:/Projects/Starfield-2026/src/Starfield2026.MiniToolbox
dotnet run --project src/MiniToolbox.App -c Release -- <command> [options]
```
**Commands:**
| Command | Purpose |
|---------|---------|
| `garc --dump -i <garc> -o <dir>` | Dump raw GARC entries |
| `text -i <dir> -o <dir>` | Decode Sun/Moon game text |
| `text -i <dir> -e <N>` | Decode single entry to console |
| `trpak` | Extract Scarlet/Violet archives |
| `gdb1` | Extract Star Fox archives |

### Quick Smoke Test
```bash
# Build everything
dotnet build Starfield2026.sln

# Run model loader — should open window, render character on grid
dotnet run --project Starfield2026.3DModelLoader

# Decode game text (from previously dumped GARC)
cd Starfield2026.MiniToolbox
dotnet run --project src/MiniToolbox.App -c Release -- text -i ../../Starfield2026.Tests/sun-moon-dump/spica-exported/game_text_english -e 22
# Expected: "Pokémon Sun", "Pokémon Moon", "Presented by GAME FREAK"
```

---

## 6. Known Issues & Strategies

### Issue 1: Shared Animation Retargeting Untested at Runtime
The `LoadClipRetargeted` path compiles but has never rendered on screen. Bone name mismatches could cause missing tracks (bones stuck in bind pose) or wrong bones moving.

**Strategy A — Diagnostic overlay:** Add a debug mode to `OverworldCharacter.Draw()` that renders bone positions as colored dots. Compare shared-clip pose vs own-clip pose visually. Bones that don't move under shared clip indicate mapping gaps.

**Strategy B — Exhaustive bone name audit:** Dump all bone names from 5–10 representative Scarlet models. Diff against the BoneMapping dictionary. Add missing entries. Some Scarlet models may have extra finger/face bones not in Sun-Moon skeletons — these should map to `null` (skip) rather than causing errors.

**Strategy C — Fallback to bind pose:** When a shared clip has no track for a bone (after remapping), ensure `SkeletalAnimator.Sample()` uses the bind pose for that bone rather than zeroing it out. This is the current behavior but should be explicitly verified.

### Issue 2: Procedural Animation Axis Detection Failed
Attempted auto-detecting the "pitch" axis for procedural leg rotation. The bind pose orientation varies between models (some face +Z, some -Z, different up axes). Simple dot-product heuristics picked wrong axes causing legs to drift sideways.

**Strategy A — Per-bone axis annotation:** Store pitch/yaw/roll axis info in the manifest per bone role. This is manual but guarantees correctness.

**Strategy B — Animation-driven detection:** Load a known "walk" clip, find the thigh bone's dominant rotation axis across all keyframes. That axis is the pitch axis. Use it for procedural overlays.

**Strategy C — Abandon procedural, use retargeted clips:** The bone mapping retargeting approach is more robust. Procedural animation is only worth revisiting for effects that can't be pre-authored (physics-driven, IK, ragdoll).

### Issue 3: Text Decoder Variable Handling is Minimal
Variables (`0x0010` marker) are decoded as `[VAR]` without resolving the actual variable type (Pokémon name, item name, player name, etc.). pk3DS uses a `GameConfig` with variable code tables.

**Strategy A — Port pk3DS variable tables:** Copy the variable name lookup from pk3DS's `GameConfig`. Map codes like `0xFF00` → "TRPokemon", `0x0100` → "PLAYER", etc. Output `[VAR PLAYER]` instead of `[VAR]`.

**Strategy B — Raw hex output:** For entries we just need to read (trainer class names), variables don't matter — the class names are plain text. Only invest in variable resolution if we need to parse dialogue.

### Issue 4: Character Select Overlay Font Rendering
The `PixelFont` bitmap renderer works but is basic. With 126+ characters, the list is hard to navigate. Category filtering (by trainer class) would help.

**Strategy A — Category tabs:** Group characters by trainer class category (Trainer, Swimmer, etc.) using the decoded class names. Tab/arrow keys switch categories.

**Strategy B — Search filter:** Type-to-filter on the character name. Requires text input handling in the overlay.

**Strategy C — Thumbnail preview:** Pre-render a small thumbnail of each character model. Display alongside the name in the select overlay. Expensive but very usable.

---

## 7. Architecture & New Features

### Three-Mode Animation System (New Architecture)
```
SplitModelAnimationSetLoader.Load(characterFolder)
├── Mode: Own
│   └── Load clips from character's own manifest only
├── Mode: FillMissing (default)
│   ├── Load own clips
│   └── For each tag in FillTags not found:
│       ├── BoneMapping.GetMapForRig(skeleton) → detect family
│       ├── Read shared reference manifest
│       └── LoadClipRetargeted() or LoadClip() → fill gap
└── Mode: SharedOnly
    ├── Skip own clips entirely
    └── Load ALL tagged clips from shared reference
```

**Configuration (static properties on SplitModelAnimationSetLoader):**
```csharp
SharedAnimationFolder = "path/to/tr0001_00";  // reference character
LoadMode = AnimationLoadMode.FillMissing;       // default mode
FillTags = new() { "Jump", "Land" };            // which gaps to fill
```

### Text Decryption Pipeline (New Feature)
```
GARC archive (a/0/3/2)
  → garc --dump → entry_NNNNN.bin (raw encrypted)
    → text -i → .txt (decrypted, one line per text string)
```
XOR cipher: per-line key starts at `0x7C89`, advances by `+0x2983` per line, rotates `key = (key << 3 | key >> 13)` per word within a line.

### Quick Wins

1. **Instant: Add more FillTags** — Just add `"Walk"`, `"Run"`, `"Idle"` to the `FillTags` set. Characters missing walk/run animations will inherit from the shared reference. Zero code change, just config.

2. **30 min: Shared clip cache** — Prevent duplicate clip loads across characters. Cache `SkeletalAnimationClip` instances by file path. Reduces memory and load time proportional to character count.

3. **1 hour: Gender-aware shared reference** — Build a static `Dictionary<int, bool>` mapping trainer class index → isMale. Select `tr0001_00` (male) or `tr0002_00` (female) as shared reference based on the character being loaded. The trainer class name data is already decoded.

4. **2 hours: Binary clip format** — Serialize loaded clips to a simple binary format (bone count, frame count, keyframe arrays). Skip XML parsing on subsequent loads. Could cut character load time by 80%+ for shared clips.

---

## 8. File Reference

### New Files This Session
| File | Lines | Purpose |
|------|-------|---------|
| `3DModelLoader/Skeletal/BoneMapping.cs` | ~80 | Bone name remapping (Sun-Moon ↔ Scarlet) |
| `MiniToolbox/Commands/TextDecodeCommand.cs` | ~160 | Sun/Moon game text XOR decryption |

### Modified Files This Session
| File | Change |
|------|--------|
| `3DModelLoader/Skeletal/SplitModelAnimationSet.cs` | Added AnimationLoadMode enum, FillMissing/SharedOnly modes, LoadSharedClips(), FillTags |
| `3DModelLoader/Skeletal/ColladaSkeletalLoader.cs` | Added LoadClipRetargeted() overload |
| `3DModelLoader/Skeletal/SkeletalAnimator.cs` | Added RebuildPose() method |
| `3DModelLoader/Skeletal/OverworldCharacter.cs` | Added jump animation selection in Update() |
| `3DModelLoader/Controllers/PlayerController.cs` | Reduced walk=6, run=12, jump=18 |
| `3DModelLoader/ModelLoaderGame.cs` | Set SharedAnimationFolder to tr0001_00 |
| `MiniToolbox/Commands/GarcCommand.cs` | Added --dump mode for raw entry extraction |
| `MiniToolbox/Program.cs` | Registered `text` command |

### Data Outputs
| Path | Content |
|------|---------|
| `Tests/sun-moon-dump/spica-exported/game_text_english/` | 127 raw encrypted game text entries |
| `Tests/sun-moon-dump/spica-exported/game_text_decoded/` | 127 decoded .txt files |
| `Tests/sun-moon-dump/spica-exported/game_text_decoded/entry_00111.txt` | Trainer class names (224 entries) |

### Project Stats
- **3DModelLoader total:** ~4,229 lines across all .cs files
- **Character models available:** 126 Sun-Moon field trainers
- **Game text entries decoded:** 127 (all English)
