using Godot;
using System.Collections.Generic;

/// <summary>
/// 小地图使用的房间语义。它与具体地牢生成器的枚举解耦，后续可以继续
/// 添加事件房、隐藏房等类型，而不必修改 MinimapPanel 的坐标逻辑。
/// </summary>
public enum RoomType
{
    Start,
    Combat,
    Safe,
    Extraction,
    Loot,
    Boss,
    Unknown
}

/// <summary>房间在本层探索过程中的可见状态。</summary>
public enum RoomDiscoveryState
{
    Undiscovered,
    Discovered,
    Visited
}

/// <summary>
/// MinimapPanel 的轻量输入数据。WorldBounds 与玩家位置必须使用同一个
/// 坐标系；本项目的适配代码会把 TileMap 房间边界转换为全局世界坐标。
/// </summary>
public sealed class MinimapRoomData
{
    public int Id { get; init; }
    public Rect2 WorldBounds { get; init; }
    public RoomType Type { get; init; } = RoomType.Unknown;
    public List<int> ConnectedRoomIds { get; } = new();
    public RoomDiscoveryState DiscoveryState { get; set; } =
        RoomDiscoveryState.Undiscovered;
    public bool IsCompleted { get; set; }

    public Vector2 WorldCenter => WorldBounds.GetCenter();
}
