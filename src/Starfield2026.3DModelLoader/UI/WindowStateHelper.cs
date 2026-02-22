#nullable enable
using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text.Json;
using Microsoft.Xna.Framework;

namespace Starfield2026.ModelLoader.UI;

/// <summary>
/// Saves and restores window position and size across sessions.
/// All sizes are stored in logical (DPI-independent) pixels and scaled
/// by the system DPI factor on restore, so MonoGame DesktopGL (SDL2)
/// creates the correct physical-pixel back buffer.
/// </summary>
public static class WindowStateHelper
{
    [DllImport("user32.dll")]
    private static extern int GetDpiForSystem();

    public class WindowConfig
    {
        public int Width { get; set; }
        public int Height { get; set; }
        public int X { get; set; } = int.MinValue;
        public int Y { get; set; } = int.MinValue;
    }

    /// <summary>
    /// Returns the system DPI scale (1.0 = 100%, 1.5 = 150%, 2.0 = 200%).
    /// </summary>
    public static float GetDpiScale()
    {
        try { return GetDpiForSystem() / 96f; }
        catch { return 1f; }
    }

    /// <summary>
    /// Loads config from JSON, returning defaults if absent or invalid.
    /// Width/Height are in logical pixels.
    /// </summary>
    public static WindowConfig Load(string path, int defaultW = 800, int defaultH = 600)
    {
        try
        {
            if (File.Exists(path))
            {
                var cfg = JsonSerializer.Deserialize<WindowConfig>(File.ReadAllText(path));
                if (cfg != null && cfg.Width > 0 && cfg.Height > 0)
                    return cfg;
            }
        }
        catch { }
        return new WindowConfig { Width = defaultW, Height = defaultH };
    }

    /// <summary>
    /// Saves current window bounds as logical pixels.
    /// </summary>
    public static void Save(string path, GameWindow window)
    {
        try
        {
            var b = window.ClientBounds;
            if (b.Width <= 0 || b.Height <= 0) return;
            float s = GetDpiScale();
            var cfg = new WindowConfig
            {
                Width = (int)(b.Width / s),
                Height = (int)(b.Height / s),
                X = b.X,
                Y = b.Y,
            };
            File.WriteAllText(path, JsonSerializer.Serialize(cfg));
        }
        catch { }
    }

    /// <summary>
    /// Applies saved config: scales logical size by DPI and sets position.
    /// </summary>
    public static void Restore(GameWindow window, GraphicsDeviceManager gfx, WindowConfig cfg)
    {
        float s = GetDpiScale();
        gfx.PreferredBackBufferWidth = (int)(cfg.Width * s);
        gfx.PreferredBackBufferHeight = (int)(cfg.Height * s);
        gfx.ApplyChanges();

        if (cfg.X != int.MinValue && cfg.Y != int.MinValue)
            window.Position = new Point(cfg.X, cfg.Y);
    }
}
