# 19 - LZA Extraction Pipeline: Lessons Learned Handoff

**Date:** 2026-02-22
**Scope:** MiniToolbox TRPAK `--scan-extract` pipeline, `--convert-dir` DAE converter, LZA (Legends: Arceus) model extraction, hash list generation
**Status:** Full pipeline working — 1,725 packs extracted, 197 DAE models + 181 texture sets converted (29 Pokémon, 155 characters, 13 field models)

---

## 1. What We Accomplished

### Scan-Extract Pipeline (Complete, Verified)
- Built `--scan-extract` command in `TrpakCommand.RunScanExtract()` that iterates all 9,892 packs in the 3GB TRPFS archive, extracts files by content detection, and writes model packs to disk
- **Memory-bounded operation**: only one pack in memory at a time with 10-pack LRU cache eviction via `TrpfsLoader.ClearPackCache()`
- **O(1) hash lookups**: replaced `Array.IndexOf` (O(n) on 178K entries) with `Dictionary<ulong, int>` indexes (`_fdHashIndex`, `_fsHashIndex`, `_fdUnusedHashIndex`)
- **Results**: 1,725 model packs extracted, 110K+ files written, 0 errors, 0 empty directories

### TRMDL-Referenced File Naming (Complete)
- Files within each model pack are named using TRMDL internal references: `mesh.PathName`, `skeleton.PathName`, `material` paths
- `IsValidPathRef()` validates that PathName contains only valid ASCII characters — LZA's TRMDL PathNames are binary data (decoded as `♀` U+2640) instead of readable strings
- When PathName is invalid (502/1,725 models), falls back to pack-derived naming: `model.trmdl`, `mesh_00.trmsh`, `skeleton_00.trskl`, `material_00.trmtr`
- `.trmbf` mesh buffer files detected by content (large binary, follows a `.trmsh` entry)
- `.bntx` texture files get generic `texture_00.bntx` naming (TRMDL doesn't reference textures by path)

### Hash List Auto-Generation (Complete, Running)
- `--scan-extract` now generates `hashes_inside_fd.txt` alongside extracted files
- Format: `0xHASH16 packDir/fileName` — directly compatible with `TrpakHashCache.LoadHashList()`
- Maps actual FD file hashes from our archive to pack-derived paths, enabling `--list` and `-m` path-based extraction after the initial scan
- Sample entry: `0xF51E0DC014E51F0B fieldenvenvik_t1_depth_1_1.trlgt/ik_t1_depth.trmdl`

### Hash File Loading Improvements (Complete)
- Hash file search now checks **arc directory first**, then `AppContext.BaseDirectory`
- Switched from `File.ReadAllLines()` to `File.ReadLines()` for lazy loading (21MB community file with 238K lines)
- Console output auto-flushed via `Console.SetOut(new StreamWriter(Console.OpenStandardOutput()) { AutoFlush = true })`

### Community Hash List Investigation (Completed — Rejected)
- Downloaded `hashes_inside_fd.txt` (21.5 MB, 238K lines) and `hashes_inside_trpak.txt` (22.4 MB, 252K lines) from pkZukan/PokeDocs
- Both files contain the same hash→path mappings; TRPAK file additionally groups entries by pack name (`@arc\packname.trpak` headers)
- **Version mismatch**: only 1 out of 178,109 FD hashes matched — community files were built from a different archive version
- Decision: generate our own hash list from scan-extract instead

### Progress Monitoring (Complete)
- Per-pack error logging to `_scan_log.txt` with auto-flush
- Progress reported every 500 packs: `Pack N/9892 | M models | F files | P packs`
- `scripts/check_progress.ps1` helper script for monitoring scan output

### DAE Conversion Pipeline — `--convert-dir` (Complete)
- Added `--convert-dir <dir>` command that converts extracted loose Trinity files to DAE + PNG textures
- Iterates each subdirectory with `.trmdl` + `.trmsh` + `.trmbf`, runs `TrinityModelDecoder` → `TrinityColladaExporter` → DAE, plus `BntxDecoder` → PNG
- **Directory-mode decoder**: `TrinityModelDecoder` constructor detects invalid LZA `PathName` references via `IsValidPathRef()` and falls back to extension-based file discovery in the model directory
- **Null-safe FlatBuffer parsing**: comprehensive null checks on `TRMSH.Meshes`, `vertexDeclaration`, `meshParts`, `TRMeshBuffers` bounds, and `bufferFilePath`
- **FlatBuffer pre-validation**: `ValidateFlatBuffer()` checks root offset, vtable offset, and vtable size before FlatSharp deserialization to prevent native crashes
- **Batch processing**: `--skip N` and `--limit N` args for processing in ranges (works around 1 corrupted pack that crashes FlatSharp native code)
- **Results**: 811 convertible packs → **197 DAE models** + **181 texture sets** (29 Pokémon, 155 characters, 13 field/environment)

---

## 2. What Work Remains

### Load DAE Models in 3DModelLoader
The 197 converted DAE models are at `pokemon-lza-dump/dae_output/`. Each subdirectory has `model.dae` and a `textures/` folder with PNGs. The 3DModelLoader needs to load these for rendering/preview.

### Improve Conversion Rate (Currently 197/811 = 24%)
468 packs "failed" because they had the required file extensions but no actual vertex mesh data after parsing. Many are animation configs (`.tracr`, `.tracp`), light rigs (`.trlgt`), or morph data (`.trslp`). A pre-filter on directory name (exclude `.tranm`, `.tracr`, `.trlgt` suffixes) would eliminate these false positives.

### Handle the 1 Crashing Pack
Pack 158 (`fieldmodelt2t2_gt2_g02_1t2_g02_1_anime_config.tracp`) causes a native FlatSharp access violation. It passes `.trmdl`/`.trmsh` validation but the `.trmbf` data crashes the native deserializer. Currently worked around with `--skip 159`.

### Trainer Model Filtering
Trainer models are under `ik_chara/model_cc_ir/tr####_##_*` (29 packs extracted). Use `--filter "ik_chara.*tr\d"` for targeted conversion.

---

## 3. Optimizations — Prime Suspects

### Suspect 1: Full-Archive Scan Every Run
`--scan-extract` iterates all 9,892 packs even when seeking one model. With the generated hash list in place, `--list` and `-m` can look up a model by path in O(1) without scanning. **The scan only needs to run once** — subsequent extractions should use the hash list.

**Fix:** After the initial scan, copy `hashes_inside_fd.txt` to the `arc/` directory. All future commands will load it automatically and skip scanning.

### Suspect 2: Eager Pack Loading in `TrpfsLoader` Constructor
The constructor reads the TRPFD file descriptor (7MB FlatBuffer) and the TRPFS filesystem section (158KB). Both are synchronous and block startup. The 238K community hash file takes additional time to parse (hex parsing per line).

**Fix:** Lazy-load the filesystem section — only deserialize when `FindFilesByExtension` or `ExtractFile` is first called. For the hash file, consider a binary cache format (like `GFPAKHashCache.bin`) that loads in O(1) via memory-mapped I/O.

### Suspect 3: No EncryptionType Caching
`ExtractFile()` deserializes the entire pack FlatBuffer to check `entry.EncryptionType` and decompress if needed. The same pack may be deserialized multiple times for different files within it. The LRU cache helps, but with a 10-pack limit and 178K files, cache misses are common.

**Fix:** Build a file→pack×entry index during the initial hash list scan. Store it as a separate binary index alongside `hashes_inside_fd.txt`. This avoids re-deserializing packs during extraction.

### Suspect 4: `Console.SetOut(AutoFlush)` Wrapper
The `StreamWriter(Console.OpenStandardOutput()) { AutoFlush = true }` replacement for `Console.Out` adds overhead to every `Console.Write` call — a write and flush syscall per line.

**Fix:** Use explicit `Console.Out.Flush()` only at progress checkpoints (every 500 packs) instead of wrapping the entire stdout. This keeps most writes buffered while ensuring progress is visible.

---

## 4. Step-by-Step Approach to Get App Fully Working

### Phase 1: Complete Scan-Extract + Hash List
```bash
cd D:\Projects\Starfield-2026\src\Starfield2026.MiniToolbox
dotnet run --project src/MiniToolbox.App -c Release -- trpak \
  --arc "D:\Projects\Starfield-2026\src\Starfield2026.Tests\pokemon-lza-dump\arc" \
  --scan-extract \
  -o "D:\Projects\Starfield-2026\src\Starfield2026.Tests\pokemon-lza-dump\scan_extract"
```
Wait for completion (~15 min). Verify `scan_extract/hashes_inside_fd.txt` exists and `_scan_log.txt` shows 0 errors.

### Phase 2: Install Generated Hash List
```bash
copy scan_extract\hashes_inside_fd.txt arc\hashes_inside_fd.txt
```

### Phase 3: Verify Hash List Works
```bash
dotnet run --project src/MiniToolbox.App -c Release -- trpak \
  --arc "..\Starfield2026.Tests\pokemon-lza-dump\arc" --list
```
Should show 1,725+ models with paths from the generated hash list.

### Phase 4: Implement Directory Mode Decoder
Modify `TrinityModelDecoder` to discover files by extension within the TRMDL's directory when PathName references are invalid. This enables DAE conversion of the extracted loose files.

### Phase 5: Extract + Convert a Single Trainer
```bash
dotnet run --project src/MiniToolbox.App -c Release -- trpak \
  --arc "..\Starfield2026.Tests\pokemon-lza-dump\arc" \
  -m "pack_name/model.trmdl" \
  -o trainer_test
```
Verify `trainer_test/` contains `model.dae`, textures, and animation clips.

### Phase 6: Batch Extract Trainers
Add `--filter` flag for `--all` mode to extract only packs matching a pattern (e.g., `tr\d{4}`).

---

## 5. How to Start/Test the API

### Prerequisites
- .NET 8.0+ SDK (net8.0-windows target)
- LZA archive dump at `Starfield2026.Tests/pokemon-lza-dump/arc/` containing `data.trpfd` (7MB) and `data.trpfs` (3GB)
- Oodle DLL for decompression (loaded at runtime by `OodleDecompressor`)

### Build
```bash
cd D:\Projects\Starfield-2026\src\Starfield2026.MiniToolbox
dotnet build src/MiniToolbox.App -c Release
```
**Important:** The project targets `net8.0-windows`. Running the DLL directly with `dotnet MiniToolbox.App.dll` picks up `net8.0` (stale), not `net8.0-windows`. Always use `dotnet run --project src/MiniToolbox.App -c Release`.

### Available Commands
| Command | Description |
|---------|-------------|
| `trpak --arc <dir> --list` | List all models found via hash cache |
| `trpak --arc <dir> --list-packs` | Dump all 9,892 pack names |
| `trpak --arc <dir> --scan` | Scan for models without extracting |
| `trpak --arc <dir> --scan-extract -o <dir>` | Extract all model packs to disk + generate hash list |
| `trpak --arc <dir> -m <path> -o <dir>` | Extract single model by path |
| `trpak --arc <dir> --all -o <dir>` | Batch extract all models (parallel) |
| `trpak --arc <dir> --generate-hashes` | Generate hash list from hash cache templates |
| `trpak --convert-dir <dir> -o <dir>` | **NEW** Convert extracted loose files to DAE + PNG |
| `trpak --convert-dir <dir> --filter <regex>` | Convert only packs matching regex |
| `trpak --convert-dir <dir> --skip N` | Skip first N packs (resume after crash) |
| `trpak --convert-dir <dir> --limit N` | Process at most N packs |

### Output Files
| File | Purpose |
|------|---------|
| `scan_extract/hashes_inside_fd.txt` | Generated hash list — copy to `arc/` for future use |
| `scan_extract/_scan_log.txt` | Extraction log with per-pack progress and errors |
| `scan_extract/<packName>/model.trmdl` | Extracted model files per pack |
| `scan_extract/<packName>/*.trmsh` | Mesh files (named from TRMDL refs or generic) |
| `scan_extract/<packName>/*.trskl` | Skeleton files |
| `scan_extract/<packName>/*.trmtr` | Material files |
| `scan_extract/<packName>/*.bntx` | BNTX texture files |

---

## 6. Issues & Strategies

### Issue 1: TRMDL PathNames Are Binary Garbage in LZA
**Symptom:** `Path.GetFileName(mesh.PathName)` returns `♀` (U+2640) or other binary data instead of a filename.
**Root cause:** LZA's TRMDL FlatBuffers store PathName fields as raw bytes that decode to non-ASCII characters. This is different from SV archives where PathNames are valid `romfs://` paths.
**Fix applied:** `IsValidPathRef()` validates PathName has only printable ASCII characters (0x20-0x7E, plus `/` and `\`). Falls back to pack-derived or generic naming when invalid.

### Issue 2: Community Hash List Version Mismatch
**Symptom:** Loading the pkZukan community `hashes_inside_fd.txt` resolves only 1 of 178,109 FD hashes.
**Root cause:** The community hash file was built from a different archive version. Our archive's FD contains completely different file hashes.
**Fix applied:** Generate our own hash list from scan-extract, mapping actual FD hashes to pack-derived paths.

### Issue 3: Stale DLL Target Confusion
**Symptom:** Code changes don't take effect when running `dotnet bin/Release/net8.0/MiniToolbox.App.dll`.
**Root cause:** The project targets `net8.0-windows` but `bin/Release/` contains both `net8.0/` and `net8.0-windows/` folders. Only the `-windows` target gets rebuilt. Running the `net8.0` DLL picks up old code.
**Fix:** Always use `dotnet run --project src/MiniToolbox.App -c Release` which picks the correct target.

### Issue 4: Console Output Buffering
**Symptom:** `command_status` shows "No output" for minutes even though the process is running.
**Root cause:** `Console.Write` with `\r` (progress bar style) doesn't flush when output is captured. Also, `Console.WriteLine` is buffered when stdout is redirected.
**Fix applied:** `Console.SetOut(new StreamWriter(Console.OpenStandardOutput()) { AutoFlush = true })` at startup. For monitoring, use `Start-Process -RedirectStandardOutput` with file-level capture instead of inline terminal capture.

### Strategy 1: Binary Hash Cache for Fast Startup
Instead of parsing 238K hex lines on every run, serialize the hash cache as a binary file (`hashes.bin`) using `TrpakHashCache.LoadBinaryCache()`. This exists already — just needs a `--save-cache` flag to materialize it after the first scan-extract.

### Strategy 2: Pack-Level Index File
Build a secondary index mapping each FD file hash to its pack index, entry index, and file type — without needing to deserialize the pack data. This enables O(1) file extraction without FlatBuffer deserialization. Store as a sorted binary file for binary-search lookups.

### Strategy 3: Incremental Scan-Extract
Instead of re-scanning all 9,892 packs every time, check if the output directory already has a pack folder. Skip packs whose folders exist and contain the expected number of files (from the hash list). This reduces a 15-min scan to seconds for incremental updates.

### Strategy 4: Content-Based DAE Conversion (Bypass Hash List)
Instead of routing through `TrinityModelDecoder` with path resolution, build a standalone converter that takes a directory of loose files and converts by detected type: `.trmdl` → model root, `.trmsh` → meshes, `.trskl` → skeleton, `.trmtr` → materials, `.bntx` → textures. No hash list needed — just glob by extension in the directory.

### Strategy 5: File-Based Logging (Replace Console.WriteLine)
`Console.WriteLine` output is unreliable when stdout is captured — buffering, `\r` carriage returns, and redirect quirks have caused repeated debugging blind spots. The scan-extract already uses `_scan_log.txt` with `AutoFlush = true` which works perfectly. Extend this pattern to the entire `Run()` method:
```csharp
using var log = new StreamWriter("trpak_run.log", false) { AutoFlush = true };
void Log(string msg) { Console.WriteLine(msg); log.WriteLine(msg); }
```
Replace all `Console.WriteLine(...)` in `TrpakCommand` with `Log(...)`. Console output becomes best-effort (works interactively), while the log file is always reliable for diagnostics. The `Console.SetOut(AutoFlush)` hack added during this session can then be removed.

---

## 7. Architecture & New Features

### New: Scan-Extract Pipeline

```
TrpakCommand.RunScanExtract()
├── Build pack index: fileHash → packIdx (O(1) dictionary)
├── Open StreamWriter for _scan_log.txt (auto-flush)
├── Open StreamWriter for hashes_inside_fd.txt (auto-flush)
├── For each pack (0..9892):
│   ├── Skip if no file hashes for this pack
│   ├── Extract all files: loader.ExtractFile(hash) → bytes
│   ├── TRMDL detection: FlatBufferConverter.DeserializeFrom<TRMDL>()
│   │   └── Check: mdl.Meshes?.Length > 0 || mdl.Skeleton != null
│   ├── Name resolution: IsValidPathRef() → TRMDL refs or fallback
│   │   ├── .trmdl → modelBaseName.trmdl (from first mesh ref)
│   │   ├── .trmsh → mesh PathName or mesh_00.trmsh
│   │   ├── .trskl → skeleton PathName or skeleton_00.trskl
│   │   ├── .trmtr → material path or material_00.trmtr
│   │   ├── .bntx → texture_00.bntx (always generic)
│   │   └── .trmbf → detected by size heuristic
│   ├── Write files to packDir/
│   ├── Write hash entries: 0xHASH safeName/fileName
│   └── ClearPackCache() — LRU eviction
└── Print summary: models, files, errors
```

### Modified: TrpfsLoader O(1) Lookups

```
TrpfsLoader (modified)
├── _fdHashIndex: Dictionary<ulong, int>    FD file hash → array index
├── _fsHashIndex: Dictionary<ulong, int>    FS file hash → array index
├── _fdUnusedHashIndex: Dictionary<ulong, int>?   Unused hashes
├── _packCache: Dictionary<ulong, PackedArchive>   10-pack LRU
├── ClearPackCache()                        NEW — evict all cached packs
├── FindFilesByExtension()                  Uses hash cache string filter
└── ExtractFile()                           O(1) hash → pack → entry → bytes
```

### Modified: Hash Loading

```
TrpakCommand.Run() — Hash file search order:
  1. arc/hashes_inside_fd.txt         (archive directory)
  2. AppContext.BaseDirectory/hashes_inside_fd.txt  (exe directory)
  → Uses File.ReadLines() for lazy loading
  → Loads into TrpakHashCache via LoadHashList()
```

### Quick Win 1: Copy Generated Hash List (1 min)
After scan-extract completes, copy `scan_extract/hashes_inside_fd.txt` to `arc/hashes_inside_fd.txt`. All future `--list` and `-m` commands instantly work with path-based lookups.

### Quick Win 2: Trainer Filter Flag (20 min)
Add `--filter <regex>` to `--scan-extract` that filters packs by name before extraction. Example: `--filter "tr\d{4}"` extracts only trainer model packs.

### Quick Win 3: Binary Hash Cache (15 min)
Add `--save-cache` flag to `--scan-extract` that writes a binary cache alongside the text hash list. Future runs load `hashes.bin` (O(1) read) instead of parsing 238K lines.

### Quick Win 4: Directory-Mode DAE Export (30 min)
Add a `--loose` flag to `TrinityModelDecoder` that discovers files by extension within the model's directory instead of using TRMDL PathName references. This is the critical quick win that unblocks DAE conversion of all 1,725 extracted models.

---

## 8. Key Files Reference

| File | Purpose | Lines |
|------|---------|-------|
| `MiniToolbox.App/Commands/TrpakCommand.cs` | CLI command: `--scan-extract`, `--list`, `-m`, hash loading | 725 |
| `MiniToolbox.Trpak/Archive/TrpfsLoader.cs` | Archive loader with O(1) hash lookups and LRU cache | 279 |
| `MiniToolbox.Trpak/Archive/TrpakTypes.cs` | `TrpakHashCache`: text and binary hash file loading | 153 |
| `MiniToolbox.Trpak/TrpakFileGroupExtractor.cs` | Pipeline extractor: `EnumerateJobs()`, `ProcessJobAsync()` | 399 |
| `MiniToolbox.Trpak/Decoders/TrinityModelDecoder.cs` | TRMDL→DAE: mesh/skeleton/material resolution | — |
| `MiniToolbox.Core/Utils/FlatBufferConverter.cs` | FlatBuffer deserialization wrapper | — |
| `MiniToolbox.Hashes/FnvHash.cs` | FNV-1a 64-bit hash implementation | — |
| `scripts/check_progress.ps1` | Scan progress monitor (PowerShell) | ~20 |
| `scripts/run_extraction.ps1` | Extraction runner with file-level output capture | ~35 |

### Test Data
| Path | Description | Size |
|------|-------------|------|
| `Starfield2026.Tests/pokemon-lza-dump/arc/data.trpfd` | File descriptor (FlatBuffer) | 7 MB |
| `Starfield2026.Tests/pokemon-lza-dump/arc/data.trpfs` | Pack storage (all game data) | 3 GB |
| `Starfield2026.Tests/pokemon-lza-dump/hashes_inside_fd_community.txt` | pkZukan community hash list (wrong version) | 21.5 MB |
| `Starfield2026.Tests/pokemon-lza-dump/hashes_inside_trpak_community.txt` | pkZukan community TRPAK hash list (wrong version) | 22.4 MB |
| `Starfield2026.Tests/pokemon-lza-dump/scan_extract/` | Extracted model packs + generated hash list | ~5.6 GB |

---

## 9. LZA Archive Architecture Reference

- **TRPFD** (`data.trpfd`): File descriptor FlatBuffer containing `FileHashes[]` (178,109 FNV-1a 64-bit hashes), `FileInfo[]` (pack index per file), `PackNames[]` (9,892 pack names), `PackInfo[]` (pack sizes)
- **TRPFS** (`data.trpfs`): Pack storage — 3GB binary with a `FileSystem` FlatBuffer at offset stored in the 16-byte header. Contains `FileHashes[]` mirroring the TRPFD plus file offsets within packs
- **TRPAK packs**: Individual pack archives containing FlatBuffer `PackedArchive` with `FileEntry[]` — each entry has a hash, file buffer, encryption type, and decompressed size
- **Oodle compression**: Files with `EncryptionType != -1` are Oodle-compressed; decompressed via native `OodleDecompressor`
- **TRMDL**: Model root FlatBuffer with `Meshes[]` (PathName + LOD info), `Skeleton` (PathName), `Materials[]` (path strings)
- **File extensions**: `.trmdl` (model root), `.trmsh` (mesh geometry), `.trmbf` (mesh buffer), `.trskl` (skeleton), `.trmtr` (material), `.bntx` (texture), `.tranm`/`.tracm`/`.traef` (animation)
- **Hash format**: FNV-1a 64-bit, lowercase path with forward slashes. Community files use `0x` prefix hex. Our FD stores raw uint64
- **Path convention**: `romfs://` prefix stripped. Paths like `ik_chara/model_cc_ir/tr0002_00_rival_m/tr0002_00.trmdl`
