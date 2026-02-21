using System;
using Microsoft.Xna.Framework;
using Starfield2026.Core.Rendering;

namespace Starfield2026.Core.UI
{
    /// <summary>
    /// Centralized design tokens — single source of truth for the entire UI palette.
    /// Electric Purple on Dark Slate design system.
    /// </summary>
    public static class UITheme
    {
        public static PixelFont? GlobalFont { get; set; }

        // ── Slate backgrounds ──
        public static readonly Color SlateBase = new(20, 22, 32);
        public static readonly Color SlatePanelBg = new(20, 22, 32, 160);
        public static readonly Color SlateCard = new(30, 34, 48, 200);

        // ── Purple accents ──
        public static readonly Color PurpleAccent = new(120, 60, 220);
        public static readonly Color PurpleGlow = new(140, 80, 255, 120);
        public static readonly Color PurpleMuted = new(80, 50, 140, 150);
        public static readonly Color PurpleSelected = new(120, 60, 220, 150);

        // ── Screen gradients ──
        public static readonly Color GradTop = new(35, 20, 60);
        public static readonly Color GradBot = new(15, 12, 30);

        // ── Text ──
        public static readonly Color TextPrimary = new(235, 235, 245);
        public static readonly Color TextSecondary = new(160, 155, 180);
        public static readonly Color TextDisabled = new(80, 75, 95);
        public static readonly Color TextShadow = Color.Black * 0.5f;

        // ── Warm accents ──
        public static readonly Color WarmHighlight = new(240, 180, 80);
        public static readonly Color SelectionBorder = new(255, 140, 100);

        // ── Gameplay colors (not theme-dependent) ──
        public static readonly Color HPLabel = new(140, 200, 140);
        public static readonly Color GenderMale = new(80, 140, 255);
        public static readonly Color GenderFemale = new(255, 100, 130);
        public static readonly Color StatusBad = new(255, 100, 100);
        public static readonly Color CardFainted = new(60, 40, 50, 200);

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
