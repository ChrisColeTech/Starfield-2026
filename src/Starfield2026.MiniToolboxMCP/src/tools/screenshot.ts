import type { McpServer } from "@modelcontextprotocol/sdk/server/mcp.js";
import { z } from "zod";
import { captureActiveWindow, purgeScreenshots, getScreenshotDir } from "../helpers/screenshot.js";

export function register(server: McpServer) {
    server.tool(
        "screenshot",
        "Capture a screenshot of the active window. Returns the image as a base64 PNG. Screenshots are saved to the screenshots/ directory.",
        {
            outputPath: z.string().optional().describe("Optional custom path to save the PNG file"),
        },
        async ({ outputPath }) => {
            try {
                const { base64, savedPath } = captureActiveWindow(outputPath);
                return {
                    content: [
                        { type: "image" as const, data: base64, mimeType: "image/png" },
                        { type: "text" as const, text: `Saved to ${savedPath}` },
                    ],
                };
            } catch (err: any) {
                return { content: [{ type: "text" as const, text: `Screenshot failed: ${err.message}` }] };
            }
        }
    );

    server.tool(
        "purge_screenshots",
        "Delete all saved screenshots from the screenshots/ directory.",
        {},
        async () => {
            const dir = getScreenshotDir();
            const { deleted, freedBytes } = purgeScreenshots();
            const mb = (freedBytes / 1024 / 1024).toFixed(1);
            return {
                content: [{
                    type: "text" as const,
                    text: `Purged ${deleted} screenshots (${mb} MB freed) from ${dir}`,
                }],
            };
        }
    );
}
