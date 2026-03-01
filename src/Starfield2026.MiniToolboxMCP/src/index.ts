#!/usr/bin/env tsx
/**
 * MiniToolbox MCP Server
 *
 * MCP server wrapping MiniToolbox CLI and project utilities.
 */

// Config must be imported first — it redirects console to stderr
import { _stderr } from "./config.js";

import { McpServer } from "@modelcontextprotocol/sdk/server/mcp.js";
import { StdioServerTransport } from "@modelcontextprotocol/sdk/server/stdio.js";

import { register as registerModels } from "./tools/models.js";
import { register as registerGarc } from "./tools/garc.js";
import { register as registerTrpak } from "./tools/trpak.js";
import { register as registerGdb1 } from "./tools/gdb1.js";
import { register as registerScreenshot } from "./tools/screenshot.js";

async function main() {
    const server = new McpServer({
        name: "minitoolbox",
        version: "0.1.0",
    });

    // Register tools
    registerModels(server);
    registerGarc(server);
    registerTrpak(server);
    registerGdb1(server);
    registerScreenshot(server);

    // Connect
    const transport = new StdioServerTransport();
    await server.connect(transport);
    console.log("[Starfield2026 MCP] Server started on stdio");
}

main().catch(err => {
    _stderr(`Starfield2026 MCP server error: ${err}\n`);
    process.exit(1);
});
