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
        ("BallRecall",   new[] { "ballrecall", "ball_recall", "ballreturn", "ball_return" }),
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
        0  => "Idle",
        1  => "Walk",
        2  => "Run",
        3  => "Jump",
        4  => "BallThrow",
        5  => "BallRecall",
        6  => "LongAction1",
        7  => "ShortAction2",
        8  => "MediumAction",
        9  => "Action",
        10 => "Action2",
        11 => "ShortAction3",
        12 => "ShortAction4",
        13 => "IdleVariant",
        14 => "ShortAction5",
        15 => "LongAction2",
        16 => "ShortAction6",
        17 => "Action5",
        18 => "LongAction3",
        19 => "Action6",
        20 => "Action7",
        21 => "Action8",
        22 => "Action9",
        _  => null
    };
}
