import type { McpServer } from "@modelcontextprotocol/sdk/server/mcp.js";
import { z } from "zod";
import fs from "fs";
import path from "path";
import { MODELS_ROOT } from "../config.js";

export function register(server: McpServer) {
    server.tool(
        "list_models",
        "List the contents of the Models directory. Shows categories with model counts and paths. Pass a category to drill into it.",
        {
            category: z.string().optional().describe("Category to drill into (e.g. 'Pokemon', 'Characters/sun-moon-v2')"),
            limit: z.number().optional().default(50).describe("Max entries to return"),
        },
        async ({ category, limit }) => {
            const dir = category ? path.join(MODELS_ROOT, category) : MODELS_ROOT;

            if (!fs.existsSync(dir)) {
                return { content: [{ type: "text" as const, text: `Not found: ${dir}` }] };
            }

            const entries = fs.readdirSync(dir, { withFileTypes: true })
                .filter(d => d.isDirectory());

            const lines: string[] = [];
            lines.push(`📁 ${dir}`);
            lines.push(`   ${entries.length} folders\n`);

            for (const entry of entries.slice(0, limit)) {
                const fullPath = path.join(dir, entry.name);
                const children = fs.readdirSync(fullPath, { withFileTypes: true });
                const subDirs = children.filter(c => c.isDirectory()).length;
                const hasManifest = children.some(c => c.name === "manifest.json");
                const hasModel = children.some(c => c.name === "model.dae" || c.name === "model.obj");

                if (hasManifest || hasModel) {
                    // This is a model folder
                    const files = children.filter(c => c.isFile()).map(c => c.name);
                    const clips = children.find(c => c.name === "clips" && c.isDirectory());
                    let clipCount = 0;
                    if (clips) {
                        clipCount = fs.readdirSync(path.join(fullPath, "clips"))
                            .filter(f => f.endsWith(".dae")).length;
                    }
                    lines.push(`  🎮 ${entry.name}/  (${clipCount} clips, ${files.length} files)`);
                    lines.push(`     ${fullPath}`);
                } else if (subDirs > 0) {
                    // This is a category with sub-folders
                    lines.push(`  📂 ${entry.name}/  (${subDirs} models)`);
                    lines.push(`     ${fullPath}`);
                } else {
                    lines.push(`  📄 ${entry.name}/`);
                }
            }

            if (entries.length > limit) {
                lines.push(`\n  ... and ${entries.length - limit} more`);
            }

            return { content: [{ type: "text" as const, text: lines.join("\n") }] };
        }
    );
}
