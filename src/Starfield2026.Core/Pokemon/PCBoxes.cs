namespace Starfield2026.Core.Pokemon;

/// <summary>
/// A single PC box holding up to 30 Pokemon.
/// </summary>
public class PCBox
{
    public const int Capacity = 30;

    private readonly PartyPokemon?[] _slots = new PartyPokemon?[Capacity];

    public string Name { get; set; }
    public int Count { get; private set; }
    public bool IsFull => Count >= Capacity;

    public PCBox(string name)
    {
        Name = name;
    }

    public PartyPokemon? this[int slot] => _slots[slot];

    /// <summary>Store in first empty slot. Returns the slot index, or -1 if full.</summary>
    public int Store(PartyPokemon pkmn)
    {
        for (int i = 0; i < Capacity; i++)
        {
            if (_slots[i] == null)
            {
                _slots[i] = pkmn;
                Count++;
                return i;
            }
        }
        return -1;
    }

    /// <summary>Store at a specific slot. Returns false if occupied.</summary>
    public bool StoreAt(int slot, PartyPokemon pkmn)
    {
        if (slot < 0 || slot >= Capacity || _slots[slot] != null)
            return false;

        _slots[slot] = pkmn;
        Count++;
        return true;
    }

    /// <summary>Remove and return the Pokemon at the given slot.</summary>
    public PartyPokemon? Withdraw(int slot)
    {
        if (slot < 0 || slot >= Capacity)
            return null;

        var pkmn = _slots[slot];
        if (pkmn != null)
        {
            _slots[slot] = null;
            Count--;
        }
        return pkmn;
    }

    /// <summary>Release (delete) the Pokemon at the given slot.</summary>
    public void Clear(int slot)
    {
        if (slot >= 0 && slot < Capacity && _slots[slot] != null)
        {
            _slots[slot] = null;
            Count--;
        }
    }
}

/// <summary>
/// The PC box storage system: 18 boxes, each holding 30 Pokemon.
/// </summary>
public class PCBoxes
{
    public const int NumBoxes = 18;

    private readonly PCBox[] _boxes;

    public PCBoxes()
    {
        _boxes = new PCBox[NumBoxes];
        for (int i = 0; i < NumBoxes; i++)
            _boxes[i] = new PCBox($"Box {i + 1}");
    }

    public PCBox this[int boxIndex] => _boxes[boxIndex];

    public int TotalCount
    {
        get
        {
            int total = 0;
            for (int i = 0; i < NumBoxes; i++)
                total += _boxes[i].Count;
            return total;
        }
    }

    public bool IsFull
    {
        get
        {
            for (int i = 0; i < NumBoxes; i++)
                if (!_boxes[i].IsFull)
                    return false;
            return true;
        }
    }

    /// <summary>Store in first available slot across all boxes. Returns (box, slot) or (-1, -1) if full.</summary>
    public (int box, int slot) Store(PartyPokemon pkmn)
    {
        for (int b = 0; b < NumBoxes; b++)
        {
            int slot = _boxes[b].Store(pkmn);
            if (slot >= 0)
                return (b, slot);
        }
        return (-1, -1);
    }
}
