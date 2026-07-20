using Godot;
using System;
using System.Collections.Generic;

/// <summary>
/// 用一次 _Draw() 绘制整张房间级小地图。节点下不需要为每个房间创建
/// Control/Sprite 子节点，因此重新生成楼层和更新探索状态的成本都很低。
/// </summary>
[GlobalClass]
public partial class MinimapPanel : Control
{
    [ExportCategory("Layout")]

    [Export(PropertyHint.Range, "0,32,1")]
    public float ContentPadding { get; set; } = 10.0f;

    [Export(PropertyHint.Range, "0,256,1")]
    public float WorldPadding { get; set; } = 24.0f;

    [Export(PropertyHint.Range, "1,8,0.5")]
    public float RoomOutlineWidth { get; set; } = 1.5f;

    [Export(PropertyHint.Range, "2,24,1")]
    public float MinimumRoomDrawSize { get; set; } = 6.0f;

    [Export(PropertyHint.Range, "4,48,1")]
    public float IconDrawSize { get; set; } = 14.0f;

    [Export(PropertyHint.Range, "2,20,1")]
    public float PlayerMarkerSize { get; set; } = 4.0f;

    [Export]
    public bool UsePlayerArrow { get; set; }

    [ExportCategory("Colors")]

    [Export]
    public Color BackgroundColor { get; set; } = new("10152bd9");

    [Export]
    public Color BorderColor { get; set; } = new("53628fcc");

    [Export]
    public Color ConnectionColor { get; set; } = new("8792b8cc");

    [Export]
    public Color RoomFillColor { get; set; } = new("283253e6");

    [Export]
    public Color RoomOutlineColor { get; set; } = new("aab4d4");

    [Export]
    public Color CurrentRoomColor { get; set; } = new("f4cf63");

    [Export]
    public Color CompletedColor { get; set; } = new("67d995");

    [Export]
    public Color PlayerColor { get; set; } = new("ffffff");

    [ExportCategory("Room Icons")]

    [Export]
    public Texture2D StartIcon { get; set; }

    [Export]
    public Texture2D CombatIcon { get; set; }

    [Export]
    public Texture2D SafeIcon { get; set; }

    [Export]
    public Texture2D ExtractionIcon { get; set; }

    [Export]
    public Texture2D LootIcon { get; set; }

    [Export]
    public Texture2D BossIcon { get; set; }

    private readonly Dictionary<int, MinimapRoomData> _rooms = new();
    private DungeonGenerator _dungeonGenerator;
    private TileMapLayer _groundLayer;
    private Node2D _player;
    private Rect2 _floorWorldBounds;
    private Vector2 _mapOrigin;
    private float _worldToMapScale = 1.0f;
    private int _currentRoomId = -1;
    private Vector2 _playerWorldPosition;
    private float _playerWorldRotation;
    private bool _hasPlayerPosition;

    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Ignore;
        ClipContents = true;
        Resized += OnPanelResized;
        QueueRedraw();
    }

    /// <summary>
    /// 替换整层房间。缩放边界使用全部房间计算，但 _Draw() 只绘制已发现
    /// 的房间，因此不会泄露房间，同时探索时也不会发生缩放跳变。
    /// </summary>
    public void SetDungeonRooms(IEnumerable<MinimapRoomData> rooms)
    {
        _rooms.Clear();
        _currentRoomId = -1;
        _floorWorldBounds = new Rect2();
        bool hasBounds = false;

        if(rooms != null)
        {
            foreach(MinimapRoomData room in rooms)
            {
                if(room == null)
                    continue;

                _rooms[room.Id] = room;
                _floorWorldBounds = hasBounds
                    ? _floorWorldBounds.Merge(room.WorldBounds)
                    : room.WorldBounds;
                hasBounds = true;
            }
        }

        if(hasBounds && WorldPadding > 0.0f)
            _floorWorldBounds = _floorWorldBounds.Grow(WorldPadding);

        RecalculateMapTransform();
        QueueRedraw();
    }

    /// <summary>设置当前房间；传 -1 可在玩家进入通道时取消房间高亮。</summary>
    public void SetCurrentRoom(int roomId)
    {
        _currentRoomId = _rooms.ContainsKey(roomId) ? roomId : -1;

        if(_currentRoomId >= 0)
        {
            MinimapRoomData room = _rooms[_currentRoomId];
            room.DiscoveryState = RoomDiscoveryState.Visited;
        }

        QueueRedraw();
    }

    public void MarkRoomDiscovered(int roomId, bool visited = false)
    {
        if(!_rooms.TryGetValue(roomId, out MinimapRoomData room))
            return;

        RoomDiscoveryState targetState = visited
            ? RoomDiscoveryState.Visited
            : RoomDiscoveryState.Discovered;

        if(room.DiscoveryState < targetState)
            room.DiscoveryState = targetState;

        QueueRedraw();
    }

    public void MarkRoomCompleted(int roomId, bool completed = true)
    {
        if(!_rooms.TryGetValue(roomId, out MinimapRoomData room))
            return;

        room.IsCompleted = completed;

        if(completed && room.DiscoveryState == RoomDiscoveryState.Undiscovered)
            room.DiscoveryState = RoomDiscoveryState.Discovered;

        QueueRedraw();
    }

    /// <summary>
    /// 更新玩家标记。worldPosition 必须与 MinimapRoomData.WorldBounds 使用
    /// 同一坐标系；rotationRadians 只在 UsePlayerArrow=true 时使用。
    /// </summary>
    public void SetPlayerWorldPosition(
        Vector2 worldPosition,
        float rotationRadians = 0.0f
    )
    {
        bool changed =
            !_hasPlayerPosition ||
            !_playerWorldPosition.IsEqualApprox(worldPosition) ||
            !Mathf.IsEqualApprox(_playerWorldRotation, rotationRadians);

        _playerWorldPosition = worldPosition;
        _playerWorldRotation = rotationRadians;
        _hasPlayerPosition = true;

        if(changed)
            QueueRedraw();
    }

    public void SetPlayerMarkerVisible(bool visible)
    {
        if(_hasPlayerPosition == visible)
            return;

        _hasPlayerPosition = visible;
        QueueRedraw();
    }

    /// <summary>把房间/玩家的世界坐标投影到此 Control 的局部坐标。</summary>
    public Vector2 WorldToMinimap(Vector2 worldPosition)
    {
        return _mapOrigin + worldPosition * _worldToMapScale;
    }

    /// <summary>
    /// 本项目的便捷接入入口。通用项目也可以完全不调用 Bind，而直接使用
    /// SetDungeonRooms/SetCurrentRoom 等接口推送数据。
    /// </summary>
    public void Bind(DungeonGenerator dungeonGenerator, Node2D player)
    {
        UnbindDungeonGenerator();
        _dungeonGenerator = dungeonGenerator;
        _player = player;

        if(!GodotObject.IsInstanceValid(_dungeonGenerator))
        {
            SetDungeonRooms(Array.Empty<MinimapRoomData>());
            return;
        }

        _groundLayer = _dungeonGenerator.GetNodeOrNull<TileMapLayer>(
            _dungeonGenerator.GroundLayerPath
        );
        _dungeonGenerator.DungeonGenerated += OnDungeonGenerated;
        _dungeonGenerator.PlayerEnteredRoom += OnPlayerEnteredRoom;
        _dungeonGenerator.PlayerExitedRoom += OnPlayerExitedRoom;
        _dungeonGenerator.RoomCompleted += OnRoomCompleted;
        RebuildFromGeneratedDungeon();
        SetProcess(true);
    }

    public override void _Process(double delta)
    {
        if(!GodotObject.IsInstanceValid(_player))
            return;

        SetPlayerWorldPosition(
            _player.GlobalPosition,
            _player.GlobalRotation
        );
    }

    public override void _Draw()
    {
        DrawRect(new Rect2(Vector2.Zero, Size), BackgroundColor, true);
        DrawRect(
            new Rect2(Vector2.Zero, Size),
            BorderColor,
            false,
            1.0f,
            true
        );

        if(_rooms.Count == 0)
            return;

        DrawConnections();

        foreach(MinimapRoomData room in _rooms.Values)
        {
            if(room.DiscoveryState == RoomDiscoveryState.Undiscovered)
                continue;

            DrawRoom(room);
        }

        if(_hasPlayerPosition)
            DrawPlayerMarker();
    }

    private void DrawConnections()
    {
        HashSet<long> drawnConnections = new();

        foreach(MinimapRoomData room in _rooms.Values)
        {
            if(room.DiscoveryState == RoomDiscoveryState.Undiscovered)
                continue;

            foreach(int connectedRoomId in room.ConnectedRoomIds)
            {
                if(
                    !_rooms.TryGetValue(
                        connectedRoomId,
                        out MinimapRoomData connectedRoom
                    ) ||
                    connectedRoom.DiscoveryState ==
                        RoomDiscoveryState.Undiscovered
                )
                {
                    continue;
                }

                int lowerId = Math.Min(room.Id, connectedRoomId);
                int higherId = Math.Max(room.Id, connectedRoomId);
                long connectionKey = ((long)lowerId << 32) |
                    (uint)higherId;

                if(!drawnConnections.Add(connectionKey))
                    continue;

                DrawLine(
                    WorldToMinimap(room.WorldCenter),
                    WorldToMinimap(connectedRoom.WorldCenter),
                    ConnectionColor,
                    2.0f,
                    true
                );
            }
        }
    }

    private void DrawRoom(MinimapRoomData room)
    {
        Rect2 roomRect = WorldRectToMinimap(room.WorldBounds);
        roomRect = EnsureMinimumSize(roomRect, MinimumRoomDrawSize);
        bool isCurrentRoom = room.Id == _currentRoomId;
        Color fillColor = isCurrentRoom
            ? RoomFillColor.Lightened(0.18f)
            : RoomFillColor;
        Color outlineColor = isCurrentRoom
            ? CurrentRoomColor
            : RoomOutlineColor;
        float outlineWidth = isCurrentRoom
            ? RoomOutlineWidth + 1.5f
            : RoomOutlineWidth;

        DrawRect(roomRect, fillColor, true);
        DrawRect(roomRect, outlineColor, false, outlineWidth, true);
        DrawRoomIconOrPlaceholder(room, roomRect);

        if(room.Type == RoomType.Combat && room.IsCompleted)
            DrawCompletionMark(roomRect);
    }

    private void DrawRoomIconOrPlaceholder(
        MinimapRoomData room,
        Rect2 roomRect
    )
    {
        Texture2D icon = GetIcon(room.Type);
        float drawSize = Math.Min(
            IconDrawSize,
            Math.Min(roomRect.Size.X, roomRect.Size.Y) - 2.0f
        );

        if(icon != null && drawSize >= 2.0f)
        {
            Rect2 iconRect = new(
                roomRect.GetCenter() - Vector2.One * drawSize * 0.5f,
                Vector2.One * drawSize
            );
            DrawTextureRect(icon, iconRect, false, Colors.White);
            return;
        }

        // 未绑定贴图时使用几何图形占位，保证原型阶段仍能区分房型。
        DrawPlaceholderIcon(
            room.Type,
            roomRect.GetCenter(),
            Mathf.Max(2.0f, drawSize * 0.42f)
        );
    }

    private void DrawPlaceholderIcon(
        RoomType roomType,
        Vector2 center,
        float radius
    )
    {
        Color color = GetRoomTypeColor(roomType);
        float width = 1.5f;

        switch(roomType)
        {
            case RoomType.Start:
                DrawDiamond(center, radius, color, width);
                DrawCircle(center, Mathf.Max(1.0f, radius * 0.28f), color);
                break;
            case RoomType.Combat:
                DrawLine(
                    center + new Vector2(-radius, -radius),
                    center + new Vector2(radius, radius),
                    color,
                    width,
                    true
                );
                DrawLine(
                    center + new Vector2(radius, -radius),
                    center + new Vector2(-radius, radius),
                    color,
                    width,
                    true
                );
                break;
            case RoomType.Safe:
                DrawCircle(center, radius, color, false, width, true);
                DrawLine(
                    center + Vector2.Left * radius * 0.65f,
                    center + Vector2.Right * radius * 0.65f,
                    color,
                    width,
                    true
                );
                DrawLine(
                    center + Vector2.Up * radius * 0.65f,
                    center + Vector2.Down * radius * 0.65f,
                    color,
                    width,
                    true
                );
                break;
            case RoomType.Extraction:
                DrawDiamond(center, radius, color, width);
                DrawLine(
                    center + Vector2.Down * radius * 0.5f,
                    center + Vector2.Up * radius * 0.55f,
                    color,
                    width,
                    true
                );
                break;
            case RoomType.Loot:
                Rect2 box = new(
                    center - new Vector2(radius, radius * 0.55f),
                    new Vector2(radius * 2.0f, radius * 1.35f)
                );
                DrawRect(box, color, false, width, true);
                DrawLine(
                    box.Position + new Vector2(0.0f, radius * 0.35f),
                    box.Position + new Vector2(box.Size.X, radius * 0.35f),
                    color,
                    width,
                    true
                );
                break;
            case RoomType.Boss:
                Vector2[] crown =
                {
                    center + new Vector2(-radius, radius * 0.55f),
                    center + new Vector2(-radius, -radius * 0.55f),
                    center + new Vector2(-radius * 0.35f, 0.0f),
                    center + new Vector2(0.0f, -radius),
                    center + new Vector2(radius * 0.35f, 0.0f),
                    center + new Vector2(radius, -radius * 0.55f),
                    center + new Vector2(radius, radius * 0.55f)
                };
                DrawPolyline(crown, color, width, true);
                break;
            default:
                DrawCircle(center, Mathf.Max(1.0f, radius * 0.3f), color);
                break;
        }
    }

    private void DrawCompletionMark(Rect2 roomRect)
    {
        Vector2 center = roomRect.Position + new Vector2(
            roomRect.Size.X - 3.5f,
            3.5f
        );
        DrawCircle(center, 3.5f, BackgroundColor);
        DrawLine(
            center + new Vector2(-2.0f, 0.0f),
            center + new Vector2(-0.4f, 1.7f),
            CompletedColor,
            1.5f,
            true
        );
        DrawLine(
            center + new Vector2(-0.4f, 1.7f),
            center + new Vector2(2.3f, -1.8f),
            CompletedColor,
            1.5f,
            true
        );
    }

    private void DrawPlayerMarker()
    {
        Vector2 center = WorldToMinimap(_playerWorldPosition);

        if(!UsePlayerArrow)
        {
            DrawCircle(center, PlayerMarkerSize + 1.5f, BackgroundColor);
            DrawCircle(center, PlayerMarkerSize, PlayerColor);
            return;
        }

        Vector2 forward = Vector2.Right.Rotated(_playerWorldRotation);
        Vector2 side = forward.Orthogonal();
        Vector2[] points =
        {
            center + forward * PlayerMarkerSize * 1.6f,
            center - forward * PlayerMarkerSize +
                side * PlayerMarkerSize * 0.85f,
            center - forward * PlayerMarkerSize -
                side * PlayerMarkerSize * 0.85f
        };
        DrawColoredPolygon(points, PlayerColor);
        DrawPolyline(
            new[] { points[0], points[1], points[2], points[0] },
            BackgroundColor,
            1.0f,
            true
        );
    }

    private void DrawDiamond(
        Vector2 center,
        float radius,
        Color color,
        float width
    )
    {
        Vector2[] points =
        {
            center + Vector2.Up * radius,
            center + Vector2.Right * radius,
            center + Vector2.Down * radius,
            center + Vector2.Left * radius,
            center + Vector2.Up * radius
        };
        DrawPolyline(points, color, width, true);
    }

    private Texture2D GetIcon(RoomType roomType)
    {
        return roomType switch
        {
            RoomType.Start => StartIcon,
            RoomType.Combat => CombatIcon,
            RoomType.Safe => SafeIcon,
            RoomType.Extraction => ExtractionIcon,
            RoomType.Loot => LootIcon,
            RoomType.Boss => BossIcon,
            _ => null
        };
    }

    private static Color GetRoomTypeColor(RoomType roomType)
    {
        return roomType switch
        {
            RoomType.Start => new Color("8ed6ff"),
            RoomType.Combat => new Color("ff718d"),
            RoomType.Safe => new Color("72e3a6"),
            RoomType.Extraction => new Color("f4cf63"),
            RoomType.Loot => new Color("f1a95b"),
            RoomType.Boss => new Color("d68cff"),
            _ => new Color("b8c0da")
        };
    }

    private Rect2 WorldRectToMinimap(Rect2 worldRect)
    {
        Vector2 topLeft = WorldToMinimap(worldRect.Position);
        Vector2 bottomRight = WorldToMinimap(worldRect.End);
        return new Rect2(topLeft, bottomRight - topLeft).Abs();
    }

    private static Rect2 EnsureMinimumSize(Rect2 rect, float minimumSize)
    {
        Vector2 size = new(
            Mathf.Max(rect.Size.X, minimumSize),
            Mathf.Max(rect.Size.Y, minimumSize)
        );
        return new Rect2(rect.GetCenter() - size * 0.5f, size);
    }

    private void RecalculateMapTransform()
    {
        float availableWidth = Mathf.Max(
            1.0f,
            Size.X - ContentPadding * 2.0f
        );
        float availableHeight = Mathf.Max(
            1.0f,
            Size.Y - ContentPadding * 2.0f
        );

        if(
            _rooms.Count == 0 ||
            _floorWorldBounds.Size.X <= Mathf.Epsilon ||
            _floorWorldBounds.Size.Y <= Mathf.Epsilon
        )
        {
            _worldToMapScale = 1.0f;
            _mapOrigin = Size * 0.5f;
            return;
        }

        _worldToMapScale = Mathf.Min(
            availableWidth / _floorWorldBounds.Size.X,
            availableHeight / _floorWorldBounds.Size.Y
        );
        _mapOrigin = Size * 0.5f -
            _floorWorldBounds.GetCenter() * _worldToMapScale;
    }

    private void RebuildFromGeneratedDungeon()
    {
        DungeonLayout layout = _dungeonGenerator?.GeneratedLayout;

        if(layout == null)
        {
            SetDungeonRooms(Array.Empty<MinimapRoomData>());
            return;
        }

        List<MinimapRoomData> minimapRooms = new(layout.Rooms.Count);

        foreach(DungeonRoomData sourceRoom in layout.Rooms)
        {
            MinimapRoomData minimapRoom = new()
            {
                Id = sourceRoom.Id,
                WorldBounds = GetRoomWorldBounds(sourceRoom.Bounds),
                Type = ConvertRoomType(sourceRoom.Type),
                DiscoveryState = sourceRoom.Type == DungeonRoomType.Start
                    ? RoomDiscoveryState.Visited
                    : sourceRoom.Visited
                        ? RoomDiscoveryState.Visited
                        : RoomDiscoveryState.Undiscovered,
                IsCompleted = sourceRoom.Cleared
            };

            foreach(DungeonDoorData door in sourceRoom.Doors)
            {
                if(!minimapRoom.ConnectedRoomIds.Contains(door.ConnectedRoomId))
                    minimapRoom.ConnectedRoomIds.Add(door.ConnectedRoomId);
            }

            minimapRooms.Add(minimapRoom);
        }

        SetDungeonRooms(minimapRooms);
        DungeonRoomData startRoom = layout.StartRoom;

        if(startRoom != null)
            SetCurrentRoom(startRoom.Id);
    }

    private Rect2 GetRoomWorldBounds(Rect2I cellBounds)
    {
        if(
            !GodotObject.IsInstanceValid(_groundLayer) ||
            _groundLayer.TileSet == null
        )
        {
            return new Rect2(cellBounds.Position, cellBounds.Size);
        }

        Vector2 halfTile = (Vector2)_groundLayer.TileSet.TileSize * 0.5f;
        Vector2 localTopLeft = _groundLayer.MapToLocal(cellBounds.Position) -
            halfTile;
        Vector2 localBottomRight = _groundLayer.MapToLocal(
            cellBounds.End - Vector2I.One
        ) + halfTile;
        Vector2[] corners =
        {
            _groundLayer.ToGlobal(localTopLeft),
            _groundLayer.ToGlobal(
                new Vector2(localBottomRight.X, localTopLeft.Y)
            ),
            _groundLayer.ToGlobal(localBottomRight),
            _groundLayer.ToGlobal(
                new Vector2(localTopLeft.X, localBottomRight.Y)
            )
        };
        Vector2 minimum = corners[0];
        Vector2 maximum = corners[0];

        for(int index = 1; index < corners.Length; index++)
        {
            minimum = new Vector2(
                Mathf.Min(minimum.X, corners[index].X),
                Mathf.Min(minimum.Y, corners[index].Y)
            );
            maximum = new Vector2(
                Mathf.Max(maximum.X, corners[index].X),
                Mathf.Max(maximum.Y, corners[index].Y)
            );
        }

        return new Rect2(minimum, maximum - minimum);
    }

    private static RoomType ConvertRoomType(DungeonRoomType roomType)
    {
        return roomType switch
        {
            DungeonRoomType.Start => RoomType.Start,
            DungeonRoomType.Monster => RoomType.Combat,
            // 当前项目的 Shop 是本层取得资源的特殊房；若以后拆出 Safe/Loot，
            // 只需要调整这里的映射，不需要修改绘制代码。
            DungeonRoomType.Shop => RoomType.Loot,
            DungeonRoomType.Extraction => RoomType.Extraction,
            _ => RoomType.Unknown
        };
    }

    private void OnDungeonGenerated()
    {
        _groundLayer = _dungeonGenerator?.GetNodeOrNull<TileMapLayer>(
            _dungeonGenerator.GroundLayerPath
        );
        RebuildFromGeneratedDungeon();
    }

    private void OnPlayerEnteredRoom(
        int roomId,
        string roomName,
        int roomType
    )
    {
        MarkRoomDiscovered(roomId, true);
        SetCurrentRoom(roomId);
    }

    private void OnPlayerExitedRoom(int roomId)
    {
        if(roomId == _currentRoomId)
            SetCurrentRoom(-1);
    }

    private void OnRoomCompleted(int roomId)
    {
        MarkRoomCompleted(roomId);
    }

    private void OnPanelResized()
    {
        RecalculateMapTransform();
        QueueRedraw();
    }

    private void UnbindDungeonGenerator()
    {
        if(!GodotObject.IsInstanceValid(_dungeonGenerator))
            return;

        _dungeonGenerator.DungeonGenerated -= OnDungeonGenerated;
        _dungeonGenerator.PlayerEnteredRoom -= OnPlayerEnteredRoom;
        _dungeonGenerator.PlayerExitedRoom -= OnPlayerExitedRoom;
        _dungeonGenerator.RoomCompleted -= OnRoomCompleted;
    }

    public override void _ExitTree()
    {
        Resized -= OnPanelResized;
        UnbindDungeonGenerator();
    }
}
