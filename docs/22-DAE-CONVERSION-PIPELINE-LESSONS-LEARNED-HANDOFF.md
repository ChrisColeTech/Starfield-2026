# 22 - DAE Conversion Pipeline: Lessons Learned Handoff

**Date:** 2026-02-22
**Scope:** MiniToolbox `--convert-dir` pipeline, LZA FlatBuffer parsing robustness, `.bin` TRMSH probing, batch processing
**Status:** Pipeline functional — 197 DAE models + 181 texture sets converted from first batch; ~300+ more expected from .bin probing; Pokemon conversion rate needs improvement

---

## 1. What We Accomplished

### `--convert-dir` Command (New Feature)
- Built `TrpakCommand.RunConvertDir()` that converts extracted loose Trinity files to DAE + PNG textures
- Pipeline: `TrinityModelDecoder` → `TrinityColladaExporter` → DAE, plus `BntxDecoder` → PNG
- Supports `--filter <regex>` for targeted conversion (e.g., `--filter "ik_pokemondata"` for Pokémon only)
- Supports `--skip N` and `--limit N` for batch processing past crashes
- FlatBuffer pre-validation via `ValidateFlatBuffer()` prevents native crashes from corrupted data

### TrinityModelDecoder Robustness (Major Improvement)
- **Directory-mode file discovery**: when TRMDL `PathName` references are invalid (LZA) or point to non-existent files, decoder discovers files by extension in the model directory
- **File-exists check on PathName**: `hasValidRefs` now verifies the referenced mesh file actually exists on disk, not just that the string is valid ASCII — forces directory mode when files are missing
- **Null-safe FlatBuffer parsing**: comprehensive null checks on `TRMSH.Meshes`, `vertexDeclaration`, `meshParts`, `TRMeshBuffers` bounds, `bufferFilePath`, `indexBuf`, and `boneWeight`
- **`.bin` file probing**: when no `.trmsh` files found, probes `.bin` files (100-5000 bytes) via FlatBuffer header validation + FlatSharp deserialization to find hidden TRMSH meshes
- **Try/catch on all file parsing**: wraps `ParseMesh`, `ParseMaterial`, `ParseArmature` calls in directory mode so bad files skip instead of aborting the entire model

### Conversion Results (First Full Run)
| Category | DAE Models | With Textures |
|----------|-----------|---------------|
| Pokémon | 29 | 27 |
| Characters/Trainers | 155 | 146 |
| Field/Environment | 13 | 8 |
| **Total** | **197** | **181** |

### Key Insight: gftool Code Is Identical
- Compared all relevant code between `D:\Projects\gftool\GFTool.Renderer\Scene\GraphicsObjects\Model.cs` and our `TrinityModelDecoder.cs`
- **FlatBuffer schemas are byte-for-byte identical**: `TRMDL.cs`, `TRMSH.cs`, `TRMBF.cs`, `TRSKL.cs`, `TRMTR.cs`
- **FlatBufferConverter is identical**: same `FlatBufferSerializer.Default.Parse<T>(data)` call
- **ParseMesh / ParseMeshBuffer / vertex readers are identical**: same format codes, same switch statements
- The failures are NOT from schema differences — they're from mismatched file detection and null fields in LZA data

---

## 2. What Work Remains

### Pokémon Conversion Rate (Currently 29/394 = 7%)
394 Pokémon packs have `.trmdl` + `.trmbf`. Only 29 converted because:
- 181 packs missing `.trmsh` (mesh files detected as `.bin` by `DetectFileExtension`)
- `.bin` probing added but needs testing — the `RunConvertDir` pre-filter was blocking packs without `.trmsh` (now fixed to only require `.trmdl + .trmbf`)
- Need to re-run conversion with the relaxed filter

### 1 Native Crash Pack
Pack 158 (`fieldmodelt2t2_gt2_g02_1t2_g02_1_anime_config.tracp`) causes a FlatSharp native access violation. Passes `.trmdl`/`.trmsh` header validation but `.trmbf` data crashes the deserializer. Worked around with `--skip 159`.

### `DetectFileExtension` Improvements in Scan-Extract
The root cause of 181 missing `.trmsh` files is that `DetectFileExtension()` can't distinguish TRMSH from other small FlatBuffer tables. A proper fix would add TRMSH-specific detection during scan-extract, eliminating the need for `.bin` probing at conversion time.

### GarcCommand Build Errors
`GarcCommand.cs` references missing `MiniToolbox.Garc` / `MiniToolbox.Spica` types (`OContainer`, `RenderBase`). Pre-existing issue — build sometimes fails on stale Spica DLL reference. Not related to conversion work.

---

## 3. Optimizations — Prime Suspects

### Suspect 1: `.bin` File Probing Is Expensive
Each pack with no `.trmsh` probes every `.bin` file (100-5000 bytes range): reads file, validates FlatBuffer header, deserializes as TRMSH. Pokémon packs have 10-70 `.bin` files each × 394 packs = thousands of probe attempts.

**Fix:** Cache probe results in a `.trmsh_map.json` file per pack. On second run, skip probing and use cached `.bin` → TRMSH mappings. Alternatively, fix `DetectFileExtension` to properly detect TRMSH during scan-extract.

### Suspect 2: Sequential Pack Processing
`RunConvertDir` processes packs sequentially in a single thread. Pokémon packs with hundreds of files take several seconds each. 394 packs × ~3 sec = 20+ minutes.

**Fix:** Use `Parallel.ForEach` with a concurrency limit (e.g., 4 threads). Each pack is independent — file I/O is the bottleneck, not CPU. Need per-thread `Console.Write` locking for progress output.

### Suspect 3: Large `.trmbf` Buffer Files Read Into Memory
`ParseMesh` reads the entire `.trmbf` into memory via `FlatBufferConverter.DeserializeFrom<TRMBF>()`. Some buffer files are 400+ KB. FlatSharp materializes all vertex data arrays upfront even if only LOD0 is used.

**Fix:** Use FlatSharp's lazy deserialization (`--gen-type Lazy`) for TRMBF to only read the index/vertex buffers that are actually accessed. Alternatively, manually read only the needed buffer offsets using raw byte parsing.

### Suspect 4: Redundant FlatBuffer Deserialization
When `.bin` probing finds a valid TRMSH, it deserializes the file twice — once in the probe and once in `ParseMesh`. The probe result should be cached and passed directly.

**Fix:** Change `.bin` probing to return `(string file, TRMSH data)` tuples. Modify `ParseMesh` to accept an optional pre-deserialized TRMSH.

---

## 4. Step-by-Step Approach to Get App Fully Working

### Step 1: Run the Extraction (Already Set Up)
```bash
cd D:\Projects\Starfield-2026\src\Starfield2026.MiniToolbox
dotnet run --project src/MiniToolbox.App -c Release -- trpak \
  --convert-dir "D:\Projects\Starfield-2026\src\Starfield2026.Tests\pokemon-lza-dump\scan_extract" \
  -o "D:\Projects\Starfield-2026\src\Starfield2026.Tests\pokemon-lza-dump\extracted" \
  --filter "ik_pokemondata"
```

### Step 2: Convert Characters/Trainers
```bash
dotnet run --project src/MiniToolbox.App -c Release -- trpak \
  --convert-dir "..\Starfield2026.Tests\pokemon-lza-dump\scan_extract" \
  -o "..\Starfield2026.Tests\pokemon-lza-dump\extracted" \
  --filter "ik_chara"
```

### Step 3: Verify Output
```powershell
$out = "D:\Projects\Starfield-2026\src\Starfield2026.Tests\pokemon-lza-dump\extracted"
$dae = Get-ChildItem $out -Directory | Where-Object { Test-Path (Join-Path $_.FullName "model.dae") }
Write-Host "DAE models: $($dae.Count)"
```

### Step 4: Skip Past Crashes (If Needed)
If the process crashes on a specific pack, note the pack number from the log and re-run with `--skip`:
```bash
dotnet run --project src/MiniToolbox.App -c Release -- trpak \
  --convert-dir "scan_extract" -o "extracted" --skip 159
```

### Step 5: Load in 3DModelLoader
DAE files are at `extracted/<packName>/model.dae` with textures at `extracted/<packName>/textures/*.png`.

---

## 5. How to Start/Test the API

### Prerequisites
- .NET 8.0+ SDK (net8.0-windows target)
- LZA archive already scan-extracted to `pokemon-lza-dump/scan_extract/`
- Oodle DLL for decompression (loaded at runtime by `OodleDecompressor`)

### Build
```bash
cd D:\Projects\Starfield-2026\src\Starfield2026.MiniToolbox
dotnet build src/MiniToolbox.App -c Release
```
**Note:** If build fails on `GarcCommand.cs`, this is a pre-existing Spica reference issue. The TRPAK/conversion code compiles fine — rebuild Spica first: `dotnet build src/MiniToolbox.Spica -c Release`.

### New Commands (This Session)
| Command | Description |
|---------|-------------|
| `trpak --convert-dir <dir> -o <dir>` | Convert extracted loose files to DAE + PNG |
| `trpak --convert-dir <dir> --filter <regex>` | Convert only packs matching regex |
| `trpak --convert-dir <dir> --skip N` | Skip first N packs (resume after crash) |
| `trpak --convert-dir <dir> --limit N` | Process at most N packs |

### Quick Test
```bash
dotnet run --project src/MiniToolbox.App -c Release -- trpak \
  --convert-dir "..\Starfield2026.Tests\pokemon-lza-dump\scan_extract" \
  -o "..\Starfield2026.Tests\pokemon-lza-dump\test_output" \
  --filter "ik_pokemondatapm0006" --limit 5
```
Should produce Charizard DAE with ~21 textures.

---

## 6. Issues & Strategies

### Issue 1: `.bin` Files Are Untyped — TRMSH Detection Fails
**Symptom:** 181 Pokémon packs have mesh data in `.bin` files instead of `.trmsh`. `DetectFileExtension()` in scan-extract can't distinguish TRMSH FlatBuffers from other small FlatBuffers (TRSKL, TRMTR, animation configs).
**Root cause:** All Trinity FlatBuffers share the same generic header structure. TRMSH has no magic bytes — differentiation requires schema-aware deserialization.

### Issue 2: Native FlatSharp Crashes on Bad Data
**Symptom:** Process exits with `-1073740791` (STATUS_STACK_BUFFER_OVERRUN) on certain `.trmbf` files.
**Root cause:** FlatSharp's generated deserializer reads past buffer bounds when vtable indicates fields that don't exist in the actual data. This happens inside native/JIT code and can't be caught by managed try/catch.

### Issue 3: TRMDL PathNames Are Valid ASCII But Files Don't Exist
**Symptom:** `hasValidRefs` was true (PathNames looked like real filenames) but referenced files weren't in the extracted directory. Decoder tried normal-path mode and threw `FileNotFoundException`.
**Root cause:** LZA's TRMDL PathNames reference the original romfs:// paths, but extracted files use sanitized directory names.

### Issue 4: Multiple `.trmbf` Files Per Pack — Wrong Buffer Paired
**Symptom:** Decoder picks first `.trmbf` by alphabet. Many packs have 4-170+ `.trmbf` files of which only 1-2 match the TRMSH meshes.
**Root cause:** `ParseMesh` fallback uses `Directory.GetFiles("*.trmbf")[0]` when TRMSH's `bufferFilePath` is invalid.

### Strategy 1: Schema-Aware `DetectFileExtension`
Add TRMSH-specific detection: deserialize as TRMSH in `DetectFileExtension` (with a 5KB size cap), check if `Meshes != null && Meshes.Length > 0`. This fixes the root cause during scan-extract so `.bin` probing at conversion time becomes unnecessary.

### Strategy 2: TRMBF Size Matching
When multiple `.trmbf` files exist, match by size: the TRMSH's `bufferFilePath` (when valid) hints at the expected buffer filename. Even when invalid, the buffer count should match `TRMSH.Meshes.Length` — pick the `.trmbf` whose `TRMeshBuffers.Length >= TRMSH.Meshes.Length`.

### Strategy 3: Two-Pass Conversion
First pass: probe all `.bin` files across all packs (fast — just header check + small deserialize). Build a `pack → List<TRMSH files>` index. Second pass: convert using the cached index. This avoids redundant probing and enables parallel conversion.

### Strategy 4: Process Isolation for Crash Safety
Run each pack conversion in a subprocess (`dotnet run` with `--filter "exact_pack_name"`). If the subprocess crashes, the main process logs it and continues. Eliminates `--skip` workarounds entirely. Cost: subprocess startup (~1 sec) × pack count.

---

## 7. Architecture & New Features

### New: `--convert-dir` Pipeline Architecture

```
TrpakCommand.RunConvertDir()
├── Scan input directory for subdirectories
├── Pre-filter: require .trmdl + .trmbf (relaxed from .trmdl+.trmsh+.trmbf)
├── Apply --filter regex, --skip, --limit
├── For each pack:
│   ├── ValidateFlatBuffer() on .trmdl/.trmsh files
│   ├── TrinityModelDecoder(trmdlFile)
│   │   ├── FlatBufferConverter.DeserializeFrom<TRMDL>()
│   │   ├── hasValidRefs = PathName valid AND File.Exists()
│   │   ├── Directory mode (LZA):
│   │   │   ├── Discover .trmsh files
│   │   │   ├── Fallback: probe .bin files as TRMSH
│   │   │   │   ├── Size filter: 100-5000 bytes
│   │   │   │   ├── FlatBuffer header validation
│   │   │   │   └── FlatSharp probe deserialize
│   │   │   ├── ParseMesh() with null-safe buffers
│   │   │   ├── ParseMaterial() with try/catch
│   │   │   └── ParseArmature() with try/catch
│   │   └── CreateExportData()
│   ├── TrinityColladaExporter.Export() → model.dae
│   └── BntxDecoder.Decode() → textures/*.png
└── Print summary: converted, failed, skipped
```

### Modified: TrinityModelDecoder Constructor Flow

```
TrinityModelDecoder(modelFile)
├── Deserialize TRMDL
├── Check hasValidRefs:
│   ├── Meshes != null && Length > 0
│   ├── IsValidPathRef(PathName) — ASCII chars only
│   └── File.Exists(resolved mesh path)        ← NEW
├── If valid refs → normal SV path
└── If invalid refs → directory mode:
    ├── Find *.trmsh files
    ├── If none found → probe *.bin as TRMSH    ← NEW
    │   ├── Skip files > 5KB or < 100 bytes
    │   ├── Validate FlatBuffer header
    │   └── Deserialize & check Meshes != null
    ├── ParseMesh() with null-safe buffer access ← NEW
    │   ├── Null checks: Meshes, meshParts, vertexDeclaration
    │   ├── Bounds check: buffers[i] vs buffers.Length
    │   └── Skip empty IndexBuffer/VertexBuffer
    └── Try/catch on all file parsing           ← NEW
```

### Quick Win 1: Fix `DetectFileExtension` for TRMSH (30 min)
Add TRMSH deserialization probe to `DetectFileExtension()` in `TrpakCommand.cs`. Files under 5KB that deserialize as TRMSH with `Meshes.Length > 0` get `.trmsh` extension. Re-run `--scan-extract` to fix all 181 mislabeled packs permanently.

### Quick Win 2: Re-run Pokemon Conversion (5 min)
The latest code has the relaxed pre-filter (`.trmdl + .trmbf` only) and `.bin` probing. Just re-run:
```bash
dotnet run --project src/MiniToolbox.App -c Release -- trpak \
  --convert-dir "scan_extract" -o "extracted" --filter "ik_pokemondata"
```

### Quick Win 3: Parallel Conversion (20 min)
Replace the `for` loop in `RunConvertDir` with `Parallel.ForEach` and `MaxDegreeOfParallelism = 4`. Use `Interlocked.Increment` for counters. 4× speedup for large batch runs.

### Quick Win 4: Skip Non-Model Packs by Extension (10 min)
Many "failed" packs are animation configs (`.tracr`, `.tracp`), light rigs (`.trlgt`), or morph data (`.trslp`). Add a pre-filter in `RunConvertDir`: skip directories whose name ends with `.tranm`, `.tracr`, `.trlgt`, `.traef`, `.tracp` extensions.

---

## 8. Key Files Reference

| File | Purpose | Key Changes |
|------|---------|-------------|
| `MiniToolbox.App/Commands/TrpakCommand.cs` | CLI: `--convert-dir`, `--skip`, `--limit`, `ValidateFlatBuffer()` | New `RunConvertDir()`, relaxed pre-filter |
| `MiniToolbox.Trpak/Decoders/TrinityModelDecoder.cs` | Model decoder with null-safe parsing + `.bin` probing | File-exists check, `.bin` TRMSH probe, try/catch guards |
| `MiniToolbox.Trpak/Flatbuffers/TR/Model/TRMSH.cs` | TRMSH FlatBuffer schema (identical to gftool) | Unchanged |
| `MiniToolbox.Trpak/Flatbuffers/TR/Model/TRMBF.cs` | TRMBF FlatBuffer schema (identical to gftool) | Unchanged |
| `MiniToolbox.Trpak/Exporters/TrinityColladaExporter.cs` | DAE COLLADA exporter | Unchanged |
| `MiniToolbox.Core/Texture/BntxDecoder.cs` | BNTX → PNG texture decoder | Unchanged |

### Test Data
| Path | Description |
|------|-------------|
| `pokemon-lza-dump/scan_extract/` | 1,725 extracted model packs (input for --convert-dir) |
| `pokemon-lza-dump/dae_output/` | First full conversion run: 197 DAE models |
| `pokemon-lza-dump/extracted/` | Target output for Pokemon + character extraction |
| `scripts/test_dae.ps1` | Batch conversion test script |
| `scripts/fix_trmsh.ps1` | .bin → .trmsh rename/undo utility |
| `scripts/check_progress.ps1` | File analysis / progress monitor |
