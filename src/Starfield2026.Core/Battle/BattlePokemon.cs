using Starfield2026.Core.Pokemon;

namespace Starfield2026.Core.Battle;

/// <summary>
/// A Pokemon participating in battle. Wraps stats, moves, and display HP for animation.
/// </summary>
public class BattlePokemon
{
    public string Nickname { get; }
    public int SpeciesId { get; }
    public int Level { get; set; }
    public int CurrentHP { get; set; }
    public int MaxHP { get; set; }
    public Gender Gender { get; }
    public StatusCondition StatusCondition { get; set; }

    public string? StatusAbbreviation => StatusCondition switch
    {
        StatusCondition.None => null,
        StatusCondition.Poison => "PSN",
        StatusCondition.Burn => "BRN",
        StatusCondition.Freeze => "FRZ",
        StatusCondition.Sleep => "SLP",
        StatusCondition.Paralysis => "PAR",
        StatusCondition.Confusion => "CNF",
        _ => null
    };
    public BattleMove[] Moves { get; }

    /// <summary>The party Pokemon this was created from (null for wild foes).</summary>
    public PartyPokemon? Source { get; }

    /// <summary>Smoothly animated HP for the bar display. Lerps toward CurrentHP.</summary>
    public float DisplayHP { get; set; }

    public float HPPercent => MaxHP > 0 ? (float)CurrentHP / MaxHP : 0f;
    public float DisplayHPPercent => MaxHP > 0 ? DisplayHP / MaxHP : 0f;
    public bool IsFainted => CurrentHP <= 0;

    /// <summary>EXP bar percentage (0..1) for display. Driven by source PartyPokemon.</summary>
    public float EXPPercent => Source?.EXPPercent ?? 0f;

    public void ApplyDamage(int damage)
    {
        CurrentHP = System.Math.Max(0, CurrentHP - damage);
    }

    public BattlePokemon(string nickname, int speciesId, int level,
                         int currentHP, int maxHP, Gender gender,
                         PartyPokemon? source, params BattleMove[] moves)
    {
        Nickname = nickname;
        SpeciesId = speciesId;
        Level = level;
        CurrentHP = currentHP;
        MaxHP = maxHP;
        Gender = gender;
        Source = source;
        Moves = moves;
        DisplayHP = currentHP;
    }

    /// <summary>Update DisplayHP to smoothly approach CurrentHP.</summary>
    public void UpdateDisplayHP(float deltaTime, float drainSpeed = 80f)
    {
        if (System.Math.Abs(DisplayHP - CurrentHP) < 0.5f)
        {
            DisplayHP = CurrentHP;
            return;
        }
        float dir = CurrentHP > DisplayHP ? 1f : -1f;
        DisplayHP += dir * drainSpeed * deltaTime;
        if ((dir > 0 && DisplayHP > CurrentHP) || (dir < 0 && DisplayHP < CurrentHP))
            DisplayHP = CurrentHP;
    }

    /// <summary>Sync level/HP/stats back to the source PartyPokemon after battle.</summary>
    public void SyncToParty()
    {
        if (Source == null) return;
        Source.CurrentHP = CurrentHP;
        Source.StatusCondition = StatusCondition;
        Source.Level = Level;
        Source.MaxHP = MaxHP;
    }

    /// <summary>Create a BattlePokemon from a PartyPokemon.</summary>
    public static BattlePokemon FromParty(PartyPokemon pkmn)
    {
        var moves = new BattleMove[pkmn.MoveIds.Length];
        for (int i = 0; i < pkmn.MoveIds.Length; i++)
        {
            var moveData = MoveRegistry.GetMove(pkmn.MoveIds[i]);
            int maxPP = moveData?.MaxPP ?? 5;
            moves[i] = new BattleMove(pkmn.MoveIds[i],
                i < pkmn.MovePPs.Length ? pkmn.MovePPs[i] : maxPP);
        }
        var bp = new BattlePokemon(pkmn.Nickname, pkmn.SpeciesId, pkmn.Level,
            pkmn.CurrentHP, pkmn.MaxHP, pkmn.Gender, pkmn, moves);
        bp.StatusCondition = pkmn.StatusCondition;
        return bp;
    }

    public static BattlePokemon CreateTestAlly() => new(
        "Charmander", 4, 5, 20, 20, Gender.Male, null,
        new BattleMove(2, 35),   // Scratch
        new BattleMove(3, 40),   // Growl
        new BattleMove(5, 25),   // Ember
        new BattleMove(4, 30));  // Leer

    public static BattlePokemon CreateTestFoe() => new(
        "Pidgey", 16, 3, 15, 15, Gender.Female, null,
        new BattleMove(1, 35),   // Tackle
        new BattleMove(8, 15));  // Sand Attack
}
