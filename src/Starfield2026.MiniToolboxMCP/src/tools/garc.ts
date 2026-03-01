import type { McpServer } from "@modelcontextprotocol/sdk/server/mcp.js";
import { z } from "zod";
import { runMiniToolbox } from "../helpers/minitoolbox.js";

export function register(server: McpServer) {
    server.tool(
        "garc_info",
        "Show summary info for a GARC archive file (entry count, type breakdown).",
        { input: z.string().describe("Path to the GARC file") },
        async ({ input }) => {
            const output = runMiniToolbox(`garc -i "${input}" --info`);
            return { content: [{ type: "text" as const, text: output }] };
        }
    );

    server.tool(
        "garc_list",
        "List entries in a GARC archive with detected types and model names.",
        {
            input: z.string().describe("Path to the GARC file"),
            skip: z.number().optional().describe("Skip first N entries"),
            limit: z.number().optional().describe("Max entries to show (0 = all)"),
        },
        async ({ input, skip, limit }) => {
            let args = `garc -i "${input}" --list`;
            if (skip) args += ` --skip ${skip}`;
            if (limit) args += ` -n ${limit}`;
            const output = runMiniToolbox(args);
            return { content: [{ type: "text" as const, text: output }] };
        }
    );

    server.tool(
        "garc_extract",
        "Extract models, textures, and animations from a GARC archive.",
        {
            input: z.string().describe("Path to the GARC file"),
            output: z.string().describe("Output directory"),
            format: z.enum(["dae", "obj"]).optional().default("dae").describe("Output format"),
            filter: z.string().optional().describe("Only extract entries whose name contains this string"),
            limit: z.number().optional().describe("Max entries to extract (0 = all)"),
        },
        async ({ input, output, format, filter, limit }) => {
            let args = `garc -i "${input}" --extract -o "${output}" -f ${format}`;
            if (filter) args += ` --filter ${filter}`;
            if (limit) args += ` -n ${limit}`;
            const result = runMiniToolbox(args, 600_000);
            return { content: [{ type: "text" as const, text: result }] };
        }
    );
}
