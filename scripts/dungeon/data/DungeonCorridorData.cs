using Godot;
using System.Collections.Generic;

public enum DungeonCorridorKind
{
    MainPath,
    Branch,
    CrossLink
}

public sealed class DungeonCorridorData
{
    public int Id { get; init; }
    public int FromRoomId { get; init; }
    public int ToRoomId { get; init; }
    public DungeonCorridorKind Kind { get; init; }
    public DungeonDoorData FromDoor { get; init; }
    public DungeonDoorData ToDoor { get; init; }
    public List<Vector2I> PathCells { get; } = new();
}
