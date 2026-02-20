using Starfield2026.Core.Pokemon;

namespace Starfield2026.Core.Items;

public static class ItemEffectParser
{
    public static ItemEffect? Parse(string? effectString)
    {
        if (string.IsNullOrEmpty(effectString))
            return null;

        return effectString switch
        {
            "heal_full" or "heal_full_status" =>
                new ItemEffect(ItemEffectType.HealFull),

            "heal_hp_full" =>
                new ItemEffect(ItemEffectType.HealHPFull),

            var s when s.StartsWith("heal_hp_percent_") =>
                new ItemEffect(ItemEffectType.HealHPPercent, Amount: ParseInt(s, "heal_hp_percent_")),

            var s when s.StartsWith("heal_hp_bitter_") =>
                new ItemEffect(ItemEffectType.HealHP, Amount: ParseInt(s, "heal_hp_bitter_"), LowersFriendship: true),

            var s when s.StartsWith("heal_hp_") =>
                new ItemEffect(ItemEffectType.HealHP, Amount: ParseInt(s, "heal_hp_")),

            "cure_status_all" or "cure_all_status" or "heal_status_all" =>
                new ItemEffect(ItemEffectType.CureStatusAll),

            "cure_status_all_bitter" =>
                new ItemEffect(ItemEffectType.CureStatusAll, LowersFriendship: true),

            "cure_poison" or "cure_status_poison" =>
                new ItemEffect(ItemEffectType.CureStatus, TargetStatus: StatusCondition.Poison),
            "cure_burn" or "cure_status_burn" =>
                new ItemEffect(ItemEffectType.CureStatus, TargetStatus: StatusCondition.Burn),
            "cure_freeze" or "cure_status_freeze" =>
                new ItemEffect(ItemEffectType.CureStatus, TargetStatus: StatusCondition.Freeze),
            "cure_sleep" or "cure_status_sleep" =>
                new ItemEffect(ItemEffectType.CureStatus, TargetStatus: StatusCondition.Sleep),
            "cure_paralysis" or "cure_status_paralysis" =>
                new ItemEffect(ItemEffectType.CureStatus, TargetStatus: StatusCondition.Paralysis),
            "cure_confusion" or "cure_status_confusion" =>
                new ItemEffect(ItemEffectType.CureStatus, TargetStatus: StatusCondition.Confusion),

            var s when s.StartsWith("revive_all_") =>
                new ItemEffect(ItemEffectType.ReviveAll, Amount: ParseInt(s, "revive_all_")),
            "revive_all" =>
                new ItemEffect(ItemEffectType.ReviveAll, Amount: 100),

            var s when s.StartsWith("revive_bitter_") =>
                new ItemEffect(ItemEffectType.Revive, Amount: ParseInt(s, "revive_bitter_"), LowersFriendship: true),

            var s when s.StartsWith("revive_") =>
                new ItemEffect(ItemEffectType.Revive, Amount: ParseInt(s, "revive_")),
            "revive" =>
                new ItemEffect(ItemEffectType.Revive, Amount: 50),

            var s when s.StartsWith("restore_pp_all_") =>
                new ItemEffect(ItemEffectType.RestorePPAll, Amount: ParseInt(s, "restore_pp_all_")),
            "restore_pp_all" or "restore_pp_all_full" =>
                new ItemEffect(ItemEffectType.RestorePPAll, Amount: int.MaxValue),

            "restore_pp_full" =>
                new ItemEffect(ItemEffectType.RestorePP, Amount: int.MaxValue),
            var s when s.StartsWith("restore_pp_") =>
                new ItemEffect(ItemEffectType.RestorePP, Amount: ParseInt(s, "restore_pp_")),

            _ => null
        };
    }

    private static int ParseInt(string source, string prefix)
    {
        var numStr = source[prefix.Length..];
        return numStr == "full" ? int.MaxValue : int.Parse(numStr);
    }
}
