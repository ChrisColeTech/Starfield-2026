/**
 * Capture a screenshot of the active window via PowerShell + System.Drawing.
 * Screenshots are stored in <project>/screenshots/ by default.
 */
import { execSync } from "child_process";
import fs from "fs";
import path from "path";

const SCREENSHOTS_DIR = path.resolve(import.meta.dirname, "../../screenshots");
const POWERSHELL = "C:\\Windows\\System32\\WindowsPowerShell\\v1.0\\powershell.exe";

export interface ScreenshotResult {
    base64: string;
    savedPath: string;
}

export function getScreenshotDir(): string {
    return SCREENSHOTS_DIR;
}

function buildPsScript(outputPath: string): string {
    // Single Add-Type block with everything in one C# class to avoid conflicts
    const lines = [
        'Add-Type -AssemblyName System.Drawing',
        '',
        'Add-Type -TypeDefinition @"',
        'using System;',
        'using System.Drawing;',
        'using System.Drawing.Imaging;',
        'using System.Runtime.InteropServices;',
        '',
        'public class ScreenCapture {',
        '    [DllImport("user32.dll")]',
        '    public static extern IntPtr GetForegroundWindow();',
        '',
        '    [DllImport("user32.dll")]',
        '    [return: MarshalAs(UnmanagedType.Bool)]',
        '    public static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);',
        '',
        '    [DllImport("user32.dll")]',
        '    public static extern bool SetProcessDPIAware();',
        '',
        '    [StructLayout(LayoutKind.Sequential)]',
        '    public struct RECT {',
        '        public int Left, Top, Right, Bottom;',
        '    }',
        '',
        '    public static void Capture(string path) {',
        '        SetProcessDPIAware();',
        '        IntPtr hwnd = GetForegroundWindow();',
        '        RECT rect;',
        '        GetWindowRect(hwnd, out rect);',
        '        int w = rect.Right - rect.Left;',
        '        int h = rect.Bottom - rect.Top;',
        '        if (w <= 0 || h <= 0) throw new Exception("Invalid window size");',
        '        using (var bmp = new Bitmap(w, h))',
        '        using (var g = Graphics.FromImage(bmp)) {',
        '            g.CopyFromScreen(rect.Left, rect.Top, 0, 0, new Size(w, h));',
        '            bmp.Save(path, ImageFormat.Png);',
        '        }',
        '    }',
        '}',
        '"@ -ReferencedAssemblies System.Drawing',
        '',
        `[ScreenCapture]::Capture("${outputPath}")`,
        'Write-Output "OK"',
    ];
    return lines.join("\r\n");
}

export function captureActiveWindow(outputPath?: string): ScreenshotResult {
    const now = new Date();
    const stamp = now.toISOString().replace(/T/, "_").replace(/:/g, "-").replace(/\..+/, "");
    const savePath = outputPath || path.join(
        SCREENSHOTS_DIR,
        `screenshot_${stamp}.png`
    );
    fs.mkdirSync(path.dirname(savePath), { recursive: true });

    // Write PS script to temp file to avoid quoting issues
    const tmpScript = path.join(
        process.env.TEMP || "C:\\Temp",
        `minitoolbox_screenshot_${Date.now()}.ps1`
    );
    fs.writeFileSync(tmpScript, buildPsScript(savePath), "utf-8");

    try {
        execSync(
            `${POWERSHELL} -NoProfile -ExecutionPolicy Bypass -File "${tmpScript}"`,
            { encoding: "utf-8", timeout: 15_000 }
        );

        const imgBuffer = fs.readFileSync(savePath);
        const base64 = imgBuffer.toString("base64");

        return { base64, savedPath: savePath };
    } finally {
        try { fs.unlinkSync(tmpScript); } catch { /* ignore */ }
    }
}

export function purgeScreenshots(): { deleted: number; freedBytes: number } {
    if (!fs.existsSync(SCREENSHOTS_DIR)) return { deleted: 0, freedBytes: 0 };

    let deleted = 0;
    let freedBytes = 0;
    for (const file of fs.readdirSync(SCREENSHOTS_DIR)) {
        if (!file.endsWith(".png")) continue;
        const fullPath = path.join(SCREENSHOTS_DIR, file);
        const stat = fs.statSync(fullPath);
        freedBytes += stat.size;
        fs.unlinkSync(fullPath);
        deleted++;
    }
    return { deleted, freedBytes };
}
