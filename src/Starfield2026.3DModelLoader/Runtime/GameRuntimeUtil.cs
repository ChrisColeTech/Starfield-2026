#nullable enable
using System;
using System.Diagnostics;
using System.IO;

namespace Starfield2026.ModelLoader.Runtime;

public static class GameRuntimeUtil
{
    public static long GetMemoryMB() => GC.GetTotalMemory(forceFullCollection: false) / 1024 / 1024;

    public static void LogTiming(string label, long startMem, long startTicks)
    {
        long endTicks = Stopwatch.GetTimestamp();
        long endMem = GetMemoryMB();
        double ms = (endTicks - startTicks) * 1000.0 / Stopwatch.Frequency;
        ModelLoaderLog.Info($"[PERF] {label}: {ms:F1}ms, RAM: {startMem}MB -> {endMem}MB (+{endMem - startMem}MB)");
    }

    public static string FindAssetsRoot()
    {
        string? current = AppContext.BaseDirectory;
        while (current != null)
        {
            string maybe = Path.Combine(current, "Starfield2026.Assets");
            if (Directory.Exists(maybe))
                return maybe;
            current = Path.GetDirectoryName(current);
        }

        return Path.Combine(AppContext.BaseDirectory, "Assets");
    }
}
