using System;
using Microsoft.Xna.Framework;
using Starfield2026.Core.Rendering;

namespace Starfield2026.Core.UI
{
    /// <summary>
    /// Global UI settings and themes — single source of truth for font scale.
    /// </summary>
    public static class UITheme
    {
        public static PixelFont? GlobalFont { get; set; }

        public static Color MenuBackground { get; set; } = Color.Black * 0.65f;

        /// <summary>
        /// Unified font scale for the entire game. All UI components use this.
        /// At 800px → 2, 1200px → 3, 1600px → 4, 2000px → 5
        /// </summary>
        public static int GetFontScale(int screenWidth)
        {
            return Math.Max(2, screenWidth / 400);
        }
    }
}
