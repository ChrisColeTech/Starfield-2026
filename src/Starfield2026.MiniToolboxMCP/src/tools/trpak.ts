import type { McpServer } from "@modelcontextprotocol/sdk/server/mcp.js";
import { z } from "zod";
import { runMiniToolbox } from "../helpers/minitoolbox.js";

export function register(server: McpServer) {
    server.tool(
        "trpak_list",
        "List all .trmdl models in a TRPAK/TRPFS archive.",
        { arc: z.string().describe("Archive directory containing data.trpfd/data.trpfs") },
        async ({ arc }) => {
            const output = runMiniToolbox(`trpak --arc "${arc}" --list`);
            return { content: [{ type: "text" as const, text: output }] };
        }
    );

    server.tool(
        "trpak_extract",
        "Extract a model from a TRPAK archive.",
        {
            arc: z.string().describe("Archive directory containing data.trpfd/data.trpfs"),
            model: z.string().optional().describe("Model path within archive (e.g. pokemon/pm0025/pm0025_00.trmdl)"),
            output: z.string().describe("Output directory"),
            all: z.boolean().optional().default(false).describe("Extract all models"),
            parallel: z.number().optional().describe("Max parallel jobs"),
        },
        async ({ arc, model, output, all, parallel }) => {
            let args = `trpak --arc "${arc}" -o "${output}"`;
            if (all) args += " --all";
            else if (model) args += ` --model ${model}`;
            if (parallel) args += ` -p ${parallel}`;
            const result = runMiniToolbox(args, 600_000);
            return { content: [{ type: "text" as const, text: result }] };
        }
    );
}
