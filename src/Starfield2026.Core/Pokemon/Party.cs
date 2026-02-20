using System.Collections.Generic;

namespace Starfield2026.Core.Pokemon;

/// <summary>
/// The player's party of up to 6 Pokemon.
/// </summary>
public class Party
{
    public const int MaxSize = 6;
    private readonly List<PartyPokemon> _members = new();

    public int Count => _members.Count;
    public PartyPokemon this[int index] => _members[index];
    public IReadOnlyList<PartyPokemon> Members => _members;

    public void Add(PartyPokemon pkmn)
    {
        if (_members.Count < MaxSize)
            _members.Add(pkmn);
    }

    /// <summary>Remove a Pokemon by reference. Fails if party would be empty.</summary>
    public bool Remove(PartyPokemon pkmn)
    {
        if (_members.Count <= 1) return false;
        return _members.Remove(pkmn);
    }

    /// <summary>Remove a Pokemon by index. Fails if party would be empty.</summary>
    public bool RemoveAt(int index)
    {
        if (_members.Count <= 1 || index < 0 || index >= _members.Count)
            return false;
        _members.RemoveAt(index);
        return true;
    }

    /// <summary>Swap two party slots.</summary>
    public bool Swap(int a, int b)
    {
        if (a < 0 || a >= _members.Count || b < 0 || b >= _members.Count || a == b)
            return false;
        (_members[a], _members[b]) = (_members[b], _members[a]);
        return true;
    }

    /// <summary>Remove all members. Used during save load.</summary>
    public void Clear()
    {
        _members.Clear();
    }

    /// <summary>Create a party with test data for development.</summary>
    public static Party CreateTestParty()
    {
        var party = new Party();

        var charmander = PartyPokemon.Create(4, 5, Gender.Male);
        charmander.MoveIds = [2, 3, 5, 4]; // Scratch, Growl, Ember, Leer
        charmander.MovePPs = [35, 40, 25, 30];
        party.Add(charmander);

        var pidgey = PartyPokemon.Create(16, 3, Gender.Female);
        pidgey.MoveIds = [1, 8]; // Tackle, Sand Attack
        pidgey.MovePPs = [35, 15];
        party.Add(pidgey);

        var bulbasaur = PartyPokemon.Create(1, 4, Gender.Male);
        bulbasaur.MoveIds = [1, 3, 6]; // Tackle, Growl, Vine Whip
        bulbasaur.MovePPs = [35, 40, 25];
        bulbasaur.CurrentHP = 0;
        party.Add(bulbasaur);

        return party;
    }
}
