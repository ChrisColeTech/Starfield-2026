/**
 * Shell out to MiniToolbox .NET CLI.
 */
import { execSync } from "child_process";
import { MINITOOLBOX_CSPROJ } from "../config.js";

export function runMiniToolbox(args: string, timeoutMs = 120_000): string {
    const cmd = `dotnet run --project "${MINITOOLBOX_CSPROJ}" -- ${args}`;
    console.log(`[exec] ${cmd}`);
    try {
        return execSync(cmd, {
            encoding: "utf-8",
            timeout: timeoutMs,
            maxBuffer: 10 * 1024 * 1024,
        });
    } catch (err: any) {
        return err.stdout || err.stderr || err.message;
    }
}
