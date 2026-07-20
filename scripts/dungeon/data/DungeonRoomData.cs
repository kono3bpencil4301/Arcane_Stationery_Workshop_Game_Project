using Godot;
using System.Collections.Generic;

public sealed class DungeonRoomData
{
    public int Id { get; init; }
    public Rect2I Bounds { get; init; }
    public DungeonRoomType Type { get; set; }
    /// <summary>生成树中的父房间；出生房为 -1。</summary>
    public int ParentRoomId { get; init; } = -1;
    /// <summary>不考虑跨分支补边时，与出生房相隔的连接层数。</summary>
    public int GraphDepth { get; init; }
    /// <summary>是否属于从出生房通向本层深处的主干。</summary>
    public bool IsMainPath { get; init; }
    public DungeonEncounterDifficulty EncounterDifficulty { get; set; } =
        DungeonEncounterDifficulty.Late;
    public List<DungeonDoorData> Doors { get; } = new();
    public List<Vector2I> EnemySpawnCells { get; } = new();
    public bool Visited { get; set; }
    public bool Cleared { get; set; }

    public Vector2I CenterCell => new(
        Bounds.Position.X + Bounds.Size.X / 2,
        Bounds.Position.Y + Bounds.Size.Y / 2
    );

    public bool ContainsCell(Vector2I cell)
    {
        return Bounds.HasPoint(cell);
    }
}
