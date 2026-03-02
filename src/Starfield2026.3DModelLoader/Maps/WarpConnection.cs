namespace Starfield2026.ModelLoader.Maps;

public enum WarpTrigger { Step, Interact }

public record WarpConnection(
    int X, int Y,
    string TargetMapId,
    int TargetX, int TargetY,
    WarpTrigger Trigger = WarpTrigger.Step
);

public enum MapEdge { North, South, East, West }

public record MapConnection(
    MapEdge Edge,
    string TargetMapId,
    int Offset = 0
);
