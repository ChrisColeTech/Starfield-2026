#nullable enable
using System;
using System.IO;

namespace Starfield2026.ModelLoader;

/// <summary>
/// Simple file + console logger for the model loader.
/// </summary>
public static class ModelLoaderLog
{
    private static readonly string LogPath = Path.Combine(AppContext.BaseDirectory, "modelloader.log");
    private static StreamWriter? _writer;

    public static void Initialize()
    {
        try
        {
            _writer = new StreamWriter(LogPath, append: false) { AutoFlush = true };
            Console.SetOut(_writer);
            Console.SetError(_writer);
        }
        catch
        {
            // If we can't redirect, just use default console
        }
        Info("ModelLoaderLog initialized");
    }

    public static void Info(string message)
    {
        string line = $"[{DateTime.Now:HH:mm:ss.fff}] {message}";
        try { _writer?.WriteLine(line); } catch { }
    }

    public static void Error(string message, Exception? ex = null)
    {
        string line = $"[{DateTime.Now:HH:mm:ss.fff}] ERROR: {message}";
        if (ex != null) line += $"\n  {ex}";
        try { _writer?.WriteLine(line); } catch { }
    }

    public static void Flush()
    {
        try { _writer?.Flush(); } catch { }
    }

    public static void Shutdown()
    {
        try
        {
            _writer?.Flush();
            _writer?.Dispose();
            _writer = null;
        }
        catch { }
    }
}
