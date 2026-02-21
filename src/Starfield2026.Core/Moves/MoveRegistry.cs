using Starfield2026.Core.Data;

namespace Starfield2026.Core.Moves;

/// <summary>
/// Move database backed by SQLite (gamedata.db).
/// </summary>
public static class MoveRegistry
{
    public static MoveData? GetMove(int id) => GameDataDb.GetMove(id);
}
