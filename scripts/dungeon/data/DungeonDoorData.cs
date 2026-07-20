using Godot;
using System.Collections.Generic;

public sealed class DungeonDoorData
{
    public int Id { get; init; }
    public int RoomId { get; init; }
    public int ConnectedRoomId { get; init; }
    public int CorridorId { get; init; }
    public Vector2I Cell { get; init; }
    public DungeonDirection Direction { get; init; }

    public bool IsTwoCellsWide =>
        Direction == DungeonDirection.Up ||
        Direction == DungeonDirection.Down;

    /// <summary>
    /// Vertical corridors are two cells wide. Their anchor is the right cell,
    /// so top/bottom doorways also open the cell immediately to its left.
    /// </summary>
    public IEnumerable<Vector2I> GetOccupiedCells()
    {
        if(IsTwoCellsWide)
            yield return Cell + Vector2I.Left;

        yield return Cell;
    }
}
