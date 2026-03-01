# Starfield2026.MiniToolboxMCP

MCP (Model Context Protocol) server for the Starfield-2026 game project. Exposes game asset extraction tools, screenshots, and project utilities to AI assistants.

## Setup

```bash
cd src/Starfield2026.MiniToolboxMCP
npm install
```

## Running

```bash
npm run dev     # dev mode with watch
npm start       # production
```

## MCP Config

Add to your MCP config (e.g. `~/.gemini/antigravity/mcp_config.json`):

```json
{
    "minitoolbox": {
        "command": "cmd",
        "args": ["/c", "npx", "tsx", "D:/Projects/Starfield-2026/src/Starfield2026.MiniToolboxMCP/src/index.ts"]
    }
}
```

## Tools

### Project

#### `list_models`
List top-level folders under `Models/`. No parameters.

---

### GARC (3DS Pokemon Archives)

#### `garc_info`
Show summary info for a GARC archive file (entry count, type breakdown).

| Param | Required | Description |
|-------|----------|-------------|
| `input` | ✅ | Path to the GARC file |

#### `garc_list`
List entries in a GARC archive with detected types and model names.

| Param | Required | Description |
|-------|----------|-------------|
| `input` | ✅ | Path to the GARC file |
| `skip` | | Skip first N entries |
| `limit` | | Max entries to show |

#### `garc_extract`
Extract models, textures, and animations from a GARC archive.

| Param | Required | Description |
|-------|----------|-------------|
| `input` | ✅ | Path to the GARC file |
| `output` | ✅ | Output directory |
| `format` | | `dae` (default) or `obj` |
| `filter` | | Only extract entries matching this string |
| `limit` | | Max entries to extract |

---

### TRPAK (Switch Pokemon Archives)

#### `trpak_list`
List all `.trmdl` models in a TRPAK/TRPFS archive.

| Param | Required | Description |
|-------|----------|-------------|
| `arc` | ✅ | Archive directory containing `data.trpfd`/`data.trpfs` |

#### `trpak_extract`
Extract models from a TRPAK archive.

| Param | Required | Description |
|-------|----------|-------------|
| `arc` | ✅ | Archive directory |
| `output` | ✅ | Output directory |
| `model` | | Specific model path within archive |
| `all` | | Extract all models |
| `parallel` | | Max parallel jobs |

---

### GDB1 (Wii U Star Fox Resources)

#### `gdb1_list`
List all models in a GDB1 resource directory.

| Param | Required | Description |
|-------|----------|-------------|
| `input` | ✅ | Directory containing `.modelgdb`/`.texturegdb` files |

#### `gdb1_extract`
Extract models from a GDB1 resource directory.

| Param | Required | Description |
|-------|----------|-------------|
| `input` | ✅ | Resource directory |
| `output` | ✅ | Output directory |
| `model` | | Specific model ID |
| `all` | | Extract all models |
| `parallel` | | Max parallel jobs |

---

### Screenshots

Screenshots are saved to `Starfield2026.MiniToolboxMCP/screenshots/` with human-readable timestamps (e.g. `screenshot_2026-03-01_10-31-49.png`).

#### `screenshot`
Capture a screenshot of the active window. Uses PowerShell + System.Drawing to grab only the foreground window. Returns the image as base64 PNG.

| Param | Required | Description |
|-------|----------|-------------|
| `outputPath` | | Custom path to save the PNG (default: `screenshots/` dir) |

#### `purge_screenshots`
Delete all saved screenshots from the `screenshots/` directory. No parameters. Returns count of files deleted and MB freed.

---

## Architecture

```
Starfield2026.MiniToolboxMCP/
  src/
    index.ts              # Entry point — registers tools, connects stdio
    config.ts             # Paths + stderr redirect
    helpers/
      minitoolbox.ts      # runMiniToolbox() — shells out to dotnet CLI
      screenshot.ts       # captureActiveWindow() + purgeScreenshots()
    tools/
      models.ts           # list_models
      garc.ts             # garc_info, garc_list, garc_extract
      trpak.ts            # trpak_list, trpak_extract
      gdb1.ts             # gdb1_list, gdb1_extract
      screenshot.ts       # screenshot, purge_screenshots
  screenshots/            # Captured screenshots (gitignored)
  package.json
  tsconfig.json
```

The MCP server uses **stdio transport** and wraps the existing [MiniToolbox](../Starfield2026.MiniToolbox/README.md) .NET CLI via `child_process.execSync`. No .NET code is ported — the server shells out to `dotnet run --project MiniToolbox.App.csproj` with arguments.

## Dependencies

- `@modelcontextprotocol/sdk` — MCP protocol
- `tsx` — TypeScript execution
- `dotnet` CLI — runs MiniToolbox commands
- PowerShell + `System.Drawing` — screenshots (Windows)
