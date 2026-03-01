/**
 * Shared paths and stderr redirect for MCP server.
 */
import path from "path";

/* ------------------------------------------------------------------ */
/*  Redirect console to stderr so stdout stays clean for JSON-RPC     */
/* ------------------------------------------------------------------ */
const _origStdoutWrite = process.stdout.write.bind(process.stdout);
export const _stderr = process.stderr.write.bind(process.stderr);
process.stdout.write = function write(chunk: any, ...args: any[]) {
    if (typeof chunk === "string" && chunk.startsWith("{")) {
        return _origStdoutWrite(chunk, ...args as []);
    }
    return _stderr(chunk, ...args as []);
} as any;
console.log = (...a: any[]) => _stderr(a.map(String).join(" ") + "\n");
console.warn = (...a: any[]) => _stderr(a.map(String).join(" ") + "\n");
console.error = (...a: any[]) => _stderr(a.map(String).join(" ") + "\n");

/* ------------------------------------------------------------------ */
/*  Paths                                                              */
/* ------------------------------------------------------------------ */
export const PROJECT_ROOT = path.resolve(import.meta.dirname, "../..");
export const ASSETS_ROOT = path.join(PROJECT_ROOT, "Starfield2026.Assets");
export const MODELS_ROOT = path.join(ASSETS_ROOT, "Models");
export const MINITOOLBOX_CSPROJ = path.join(
    PROJECT_ROOT, "Starfield2026.MiniToolbox", "src", "MiniToolbox.App", "MiniToolbox.App.csproj"
);

console.log(`[MiniToolbox MCP] Project root:  ${PROJECT_ROOT}`);
console.log(`[MiniToolbox MCP] Models root:   ${MODELS_ROOT}`);
console.log(`[MiniToolbox MCP] MiniToolbox:    ${MINITOOLBOX_CSPROJ}`);
