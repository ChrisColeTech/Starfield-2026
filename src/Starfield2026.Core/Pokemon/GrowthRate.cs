using System;

namespace Starfield2026.Core.Pokemon;

public enum GrowthRate : byte
{
    Erratic,
    Fast,
    MediumFast,
    MediumSlow,
    Slow,
    Fluctuating
}

public static class GrowthRateHelper
{
    public const int MaxLevel = 100;

    /// <summary>
    /// Total EXP required to reach the given level.
    /// </summary>
    public static uint GetEXPForLevel(GrowthRate rate, int level)
    {
        if (level <= 1) return 0;
        double n = level;
        return rate switch
        {
            GrowthRate.Fast => (uint)(4 * n * n * n / 5),
            GrowthRate.MediumFast => (uint)(n * n * n),
            GrowthRate.MediumSlow => (uint)Math.Max(0, 6 * n * n * n / 5 - 15 * n * n + 100 * n - 140),
            GrowthRate.Slow => (uint)(5 * n * n * n / 4),
            GrowthRate.Erratic => GetErraticEXP(level),
            GrowthRate.Fluctuating => GetFluctuatingEXP(level),
            _ => (uint)(n * n * n)
        };
    }

    /// <summary>
    /// EXP bar fill percentage (0..1) for the current level.
    /// </summary>
    public static float GetEXPPercent(uint currentEXP, int level, GrowthRate rate)
    {
        if (level >= MaxLevel) return 0f;
        uint expPrev = GetEXPForLevel(rate, level);
        uint expNext = GetEXPForLevel(rate, level + 1);
        if (expNext <= expPrev) return 0f;
        return (float)(currentEXP - expPrev) / (expNext - expPrev);
    }

    /// <summary>
    /// Gen V scaled EXP gain formula.
    /// </summary>
    public static uint CalculateEXPGain(
        int defeatedBaseEXPYield,
        int defeatedLevel,
        int victorLevel,
        bool isTrainerBattle,
        int participantCount)
    {
        float a = isTrainerBattle ? 1.5f : 1.0f;
        float b = defeatedBaseEXPYield;
        float L = defeatedLevel;
        float Lp = victorLevel;
        float s = Math.Max(1, participantCount);

        float numerator = a * b * L;
        float denominator = 5.0f * s;
        float scaleFactor = MathF.Pow(2 * L + 10, 2.5f) / MathF.Pow(L + Lp + 10, 2.5f);
        float exp = (numerator / denominator) * scaleFactor + 1;

        return (uint)MathF.Floor(exp);
    }

    private static uint GetErraticEXP(int level)
    {
        double n = level;
        double n3 = n * n * n;
        if (n < 50) return (uint)(n3 * (100 - n) / 50);
        if (n < 68) return (uint)(n3 * (150 - n) / 100);
        if (n < 98) return (uint)(n3 * ((1911 - 10 * n) / 3.0) / 500);
        return (uint)(n3 * (160 - n) / 100);
    }

    private static uint GetFluctuatingEXP(int level)
    {
        double n = level;
        double n3 = n * n * n;
        if (n < 15) return (uint)(n3 * (((int)(n + 1) / 3) + 24) / 50);
        if (n < 36) return (uint)(n3 * (n + 14) / 50);
        return (uint)(n3 * (n / 2 + 32) / 50);
    }
}
