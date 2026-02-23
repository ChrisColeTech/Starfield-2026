#nullable enable
using System;

namespace Starfield2026.ModelLoader.Animations;

public static class TagResolver
{
    private static readonly (string tag, string[] patterns)[] TagPatterns =
    {
        ("BattleIdle",   new[] { "battle_idle", "battle_wait", "battlewait" }),
        ("BattleAttack", new[] { "battle_attack", "attack01", "attack_01" }),
        ("BattleHit",    new[] { "battle_damage", "damage01", "hit01" }),
        ("BattleFaint",  new[] { "battle_down", "down01", "faint" }),
        ("Jump",         new[] { "jump", "leap" }),
        ("Land",         new[] { "land" }),
        ("Run",          new[] { "run", "dash" }),
        ("Walk",         new[] { "walk" }),
        ("Idle",         new[] { "wait", "idle", "stand" }),
        ("Speak",        new[] { "speak", "talk" }),
        ("Turn",         new[] { "turn" }),
        ("Greet",        new[] { "greet", "hello" }),
        ("BallThrow",    new[] { "ballthrow", "ball_throw" }),
    };

    public static string? FromName(string sourceName)
    {
        string lower = sourceName.ToLowerInvariant();
        foreach (var (tag, patterns) in TagPatterns)
            foreach (var pattern in patterns)
                if (lower.Contains(pattern))
                    return tag;
        return null;
    }

    public static string? FromSlot(int slot) => slot switch
    {
        0   => "Idle",
        1   => "Walk",
        2   => "Run",
        4   => "Jump",
        5   => "Land",
        7   => "ShortAction1",
        8   => "LongAction1",
        9   => "ShortAction2",
        17  => "MediumAction",
        20  => "Action",
        23  => "Action2",
        30  => "ShortAction3",
        31  => "ShortAction4",
        52  => "IdleVariant",
        54  => "ShortAction5",
        55  => "LongAction2",
        56  => "ShortAction6",
        72  => "Action5",
        123 => "LongAction3",
        124 => "Action6",
        125 => "Action7",
        126 => "Action8",
        127 => "Action9",
        _   => null
    };

    public static int ParseSlotFromName(string? name, int fallback)
    {
        if (string.IsNullOrWhiteSpace(name)) return fallback;
        int lastUnderscore = name.LastIndexOf('_');
        if (lastUnderscore >= 0 && lastUnderscore < name.Length - 1)
            if (int.TryParse(name.Substring(lastUnderscore + 1), out int slot))
                return slot;
        return fallback;
    }
}
