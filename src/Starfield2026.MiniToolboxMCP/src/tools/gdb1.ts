import type { McpServer } from "@modelcontextprotocol/sdk/server/mcp.js";
import { z } from "zod";
import { runMiniToolbox } from "../helpers/minitoolbox.js";

export function register(server: McpServer) {
    server.tool(
        "gdb1_list",
        "List all available models in a GDB1 resource directory.",
        { input: z.string().describe("Input directory containing .modelgdb/.texturegdb files") },
        async ({ input }) => {
            const output = runMiniToolbox(`gdb1 --input "${input}" --list`);
            return { content: [{ type: "text" as const, text: output }] };
        }
    );

    server.tool(
        "gdb1_extract",
        "Extract a model from a GDB1 resource directory.",
        {
            input: z.string().describe("Input directory containing .modelgdb/.texturegdb files"),
            model: z.string().optional().describe("Model ID (filename without extension)"),
            output: z.string().describe("Output directory"),
            all: z.boolean().optional().default(false).describe("Extract all models"),
            parallel: z.number().optional().describe("Max parallel jobs"),
        },
        async ({ input, model, output, all, parallel }) => {
            let args = `gdb1 --input "${input}" -o "${output}"`;
            if (all) args += " --all";
            else if (model) args += ` --model ${model}`;
            if (parallel) args += ` -p ${parallel}`;
            const result = runMiniToolbox(args, 600_000);
            return { content: [{ type: "text" as const, text: result }] };
        }
    );
}
