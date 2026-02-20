using Starfield2026.Core.Pokemon;

namespace Starfield2026.Core.Items;

public record ItemEffect(
    ItemEffectType Type,
    int Amount = 0,
    StatusCondition TargetStatus = StatusCondition.None,
    bool LowersFriendship = false
);
