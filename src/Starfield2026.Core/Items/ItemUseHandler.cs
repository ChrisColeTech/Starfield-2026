using System;
using Starfield2026.Core.Pokemon;

namespace Starfield2026.Core.Items;

public record ItemUseResult(
    bool Success,
    string Message,
    int HPRestored = 0,
    StatusCondition StatusCured = StatusCondition.None
);

public static class ItemUseHandler
{
    public static bool CanUseItem(ItemDefinition item, PartyPokemon target,
        bool inBattle, int? moveIndex = null)
    {
        if (item.ParsedEffect == null)
            return false;
        if (inBattle && !item.UsableInBattle)
            return false;
        if (!inBattle && !item.UsableOverworld)
            return false;

        var effect = item.ParsedEffect;
        return effect.Type switch
        {
            ItemEffectType.HealHP or
            ItemEffectType.HealHPFull or
            ItemEffectType.HealHPPercent =>
                !target.IsFainted && target.CurrentHP < target.MaxHP,

            ItemEffectType.CureStatus =>
                !target.IsFainted && target.StatusCondition == effect.TargetStatus,

            ItemEffectType.CureStatusAll =>
                !target.IsFainted && target.StatusCondition != StatusCondition.None,

            ItemEffectType.HealFull =>
                !target.IsFainted &&
                (target.CurrentHP < target.MaxHP ||
                 target.StatusCondition != StatusCondition.None),

            ItemEffectType.Revive or ItemEffectType.ReviveAll =>
                target.IsFainted,

            ItemEffectType.RestorePP =>
                moveIndex.HasValue && !target.IsFainted,

            ItemEffectType.RestorePPAll =>
                !target.IsFainted,

            _ => false
        };
    }

    public static ItemUseResult UseItem(ItemDefinition item, PartyPokemon target,
        int? moveIndex = null)
    {
        if (item.ParsedEffect == null)
            return Fail("This item has no effect.");

        var effect = item.ParsedEffect;

        switch (effect.Type)
        {
            case ItemEffectType.HealHP:
            {
                if (target.IsFainted) return Fail($"{target.Nickname} has fainted!");
                if (target.CurrentHP >= target.MaxHP) return Fail($"{target.Nickname} is already at full HP!");
                int healed = HealHP(target, effect.Amount);
                return new ItemUseResult(true, $"{target.Nickname} recovered {healed} HP!", HPRestored: healed);
            }

            case ItemEffectType.HealHPFull:
            {
                if (target.IsFainted) return Fail($"{target.Nickname} has fainted!");
                if (target.CurrentHP >= target.MaxHP) return Fail($"{target.Nickname} is already at full HP!");
                int healed = HealHP(target, target.MaxHP);
                return new ItemUseResult(true, $"{target.Nickname}'s HP was fully restored!", HPRestored: healed);
            }

            case ItemEffectType.HealHPPercent:
            {
                if (target.IsFainted) return Fail($"{target.Nickname} has fainted!");
                if (target.CurrentHP >= target.MaxHP) return Fail($"{target.Nickname} is already at full HP!");
                int amount = Math.Max(1, target.MaxHP * effect.Amount / 100);
                int healed = HealHP(target, amount);
                return new ItemUseResult(true, $"{target.Nickname} recovered {healed} HP!", HPRestored: healed);
            }

            case ItemEffectType.CureStatus:
            {
                if (target.IsFainted) return Fail($"{target.Nickname} has fainted!");
                if (target.StatusCondition != effect.TargetStatus)
                    return Fail($"It won't have any effect on {target.Nickname}.");
                var cured = target.StatusCondition;
                target.StatusCondition = StatusCondition.None;
                return new ItemUseResult(true, $"{target.Nickname} was cured of {StatusName(cured)}!", StatusCured: cured);
            }

            case ItemEffectType.CureStatusAll:
            {
                if (target.IsFainted) return Fail($"{target.Nickname} has fainted!");
                if (target.StatusCondition == StatusCondition.None) return Fail($"{target.Nickname} is healthy.");
                var cured = target.StatusCondition;
                target.StatusCondition = StatusCondition.None;
                return new ItemUseResult(true, $"{target.Nickname} was cured of {StatusName(cured)}!", StatusCured: cured);
            }

            case ItemEffectType.HealFull:
            {
                if (target.IsFainted) return Fail($"{target.Nickname} has fainted!");
                if (target.CurrentHP >= target.MaxHP && target.StatusCondition == StatusCondition.None)
                    return Fail($"It won't have any effect on {target.Nickname}.");
                int healed = HealHP(target, target.MaxHP);
                var cured = target.StatusCondition;
                target.StatusCondition = StatusCondition.None;
                return new ItemUseResult(true, $"{target.Nickname} was fully restored!",
                    HPRestored: healed, StatusCured: cured);
            }

            case ItemEffectType.Revive:
            case ItemEffectType.ReviveAll:
            {
                if (!target.IsFainted) return Fail($"{target.Nickname} is not fainted!");
                int restoreHP = effect.Amount >= 100
                    ? target.MaxHP
                    : Math.Max(1, target.MaxHP * effect.Amount / 100);
                target.CurrentHP = restoreHP;
                target.StatusCondition = StatusCondition.None;
                return new ItemUseResult(true, $"{target.Nickname} was revived!", HPRestored: restoreHP);
            }

            case ItemEffectType.RestorePP:
            case ItemEffectType.RestorePPAll:
                return new ItemUseResult(true, $"{target.Nickname}'s PP was restored!");

            default:
                return Fail("This item has no effect.");
        }
    }

    private static int HealHP(PartyPokemon target, int amount)
    {
        int before = target.CurrentHP;
        target.CurrentHP = Math.Min(target.MaxHP, target.CurrentHP + amount);
        return target.CurrentHP - before;
    }

    private static ItemUseResult Fail(string message) => new(false, message);

    private static string StatusName(StatusCondition status) => status switch
    {
        StatusCondition.Poison => "poisoning",
        StatusCondition.Burn => "its burn",
        StatusCondition.Freeze => "being frozen",
        StatusCondition.Sleep => "sleep",
        StatusCondition.Paralysis => "paralysis",
        StatusCondition.Confusion => "confusion",
        _ => "its condition"
    };
}
