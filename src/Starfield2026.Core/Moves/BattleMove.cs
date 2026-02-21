namespace Starfield2026.Core.Moves;

/// <summary>
/// A move slot on a Pokemon in battle. Tracks current PP.
/// </summary>
public class BattleMove
{
    public int MoveId { get; }
    public int CurrentPP { get; set; }
    public int MaxPP { get; }

    public BattleMove(int moveId, int maxPP)
    {
        MoveId = moveId;
        CurrentPP = maxPP;
        MaxPP = maxPP;
    }

    public BattleMove(int moveId, int currentPP, int maxPP)
    {
        MoveId = moveId;
        CurrentPP = currentPP;
        MaxPP = maxPP;
    }
}
