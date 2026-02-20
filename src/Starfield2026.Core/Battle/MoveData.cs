namespace Starfield2026.Core.Battle;

/// <summary>
/// Static definition of a move (from the move database).
/// </summary>
public class MoveData
{
    public int Id { get; }
    public string Name { get; }
    public MoveType Type { get; }
    public MoveCategory Category { get; }
    public int Power { get; }
    public int Accuracy { get; }
    public int MaxPP { get; }
    public int Priority { get; }

    public MoveData(int id, string name, MoveType type, MoveCategory category,
                    int power, int accuracy, int maxPP, int priority = 0)
    {
        Id = id;
        Name = name;
        Type = type;
        Category = category;
        Power = power;
        Accuracy = accuracy;
        MaxPP = maxPP;
        Priority = priority;
    }
}
