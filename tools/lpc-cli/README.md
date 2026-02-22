# lpc-cli (Phase 4)

Slim Node + TypeScript CLI for LPC character spritesheet workflows.

## Install

```bash
npm install
```

## Build

```bash
npm run build
```

## End-to-end workflow

Default metadata source:
`D:/Projects/Universal-LPC-Spritesheet-Character-Generator/item-metadata.js`

1) List available options

```bash
npm run dev -- list items
npm run dev -- list body-types
```

2) Validate preset and sprite-path readiness

```bash
npm run dev -- validate --preset presets/sample.preset.json --assetsRoot D:/Projects/Universal-LPC-Spritesheet-Character-Generator/assets
```

Optional override during validation:

```bash
npm run dev -- validate --preset presets/sample.preset.json --assetsRoot D:/Projects/Universal-LPC-Spritesheet-Character-Generator/assets --bodyType male
```

Validation output is grouped into:
- schema
- metadata match
- path resolution readiness (including missing sprite files)

3) Generate outputs

```bash
npm run dev -- generate --preset presets/sample.preset.json --assetsRoot D:/Projects/Universal-LPC-Spritesheet-Character-Generator/assets --outDir ./output/sample
```

Single preset with split actions + bundle zip:

```bash
npm run dev -- generate --preset presets/sample.preset.json --assetsRoot D:/Projects/Universal-LPC-Spritesheet-Character-Generator/assets --outDir ./output/sample --splitActions --bundleZip
```

Single preset with body type override + explicit output base name:

```bash
npm run dev -- generate --preset presets/sample.custom.preset.json --assetsRoot D:/Projects/Universal-LPC-Spritesheet-Character-Generator/assets --outDir ./output/custom --bodyType male --outputName player-custom
```

Batch generation for every preset in a folder:

```bash
npm run dev -- generate-batch --presetDir presets/npcs --assetsRoot D:/Projects/Universal-LPC-Spritesheet-Character-Generator/assets --outDir ./output/npcs --splitActions --bundleZip
```

`generate-batch` processes preset files in deterministic filename order (`*.json`, case-insensitive extension match) and prints per-preset `[ok]`/`[failed]` lines followed by a summary.

`--outputName` controls deterministic file names. If omitted, files are named from the preset file name.

`--bundleZip` writes `<name>.bundle.zip` that contains deterministic internal paths:
- `<name>/<name>.spritesheet.png`
- `<name>/<name>.character.json`
- `<name>/<name>.credits.csv`
- `<name>/<name>.credits.txt`
- `<name>/actions/<action>.png` (when `--splitActions` is enabled)

## Output layout

`generate` writes four base artifacts into `--outDir`:
- `<name>.spritesheet.png`
- `<name>.character.json`
- `<name>.credits.csv`
- `<name>.credits.txt`

Optional artifacts:
- `<name>.actions/*.png` when `--splitActions` is enabled
- `<name>.bundle.zip` when `--bundleZip` is enabled

Before writing, lpc-cli removes those exact target files so regenerated output is clean and deterministic.

Example layout (`--outDir ./output/sample`, `--splitActions --bundleZip`):

```text
output/sample/
  sample.preset.spritesheet.png
  sample.preset.character.json
  sample.preset.credits.csv
  sample.preset.credits.txt
  sample.preset.bundle.zip
  sample.preset.actions/
    backslash.png
    combat_idle.png
    ...
```

Bundle zip internal layout:

```text
sample.preset/
  sample.preset.spritesheet.png
  sample.preset.character.json
  sample.preset.credits.csv
  sample.preset.credits.txt
  actions/
    backslash.png
    combat_idle.png
    ...
```

Batch layout example (`generate-batch --presetDir presets/npcs --outDir ./output/npcs`):

```text
output/npcs/
  npc_fisher.preset.spritesheet.png
  npc_fisher.preset.character.json
  npc_fisher.preset.credits.csv
  npc_fisher.preset.credits.txt
  npc_fisher.preset.actions/
  npc_fisher.preset.bundle.zip
  npc_guard_sword.preset.spritesheet.png
  ...
```

## Custom animation caveats

- Custom animations are appended below the standard LPC sheet rows.
- Custom layers depend on `custom_animation` metadata and matching custom animation definitions.
- Some custom animations extract and remap frames from standard rows; missing standard rows or source images will fail validation/readiness or generation.

## Troubleshooting

- `Assets root does not exist`:
  - Verify `--assetsRoot` points to the LPC generator `assets` directory.
- `duplicate_selection_group`:
  - Your preset selects multiple items from the same metadata `type_name` group.
- `variant_required`:
  - Select an explicit variant for items that expose multiple variants.
- `variant_not_supported`:
  - Remove `variant` from items that do not define variants.
- `Missing sprite files` or readiness path errors:
  - Check generated relative paths in output `character.json` and verify source PNGs exist under `assets/spritesheets`.

## Tests

```bash
npm run test
```
