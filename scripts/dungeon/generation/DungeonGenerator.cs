using Godot;
using System;
using System.Collections.Generic;

[GlobalClass]
public partial class DungeonGenerator : Node
{
    [Signal]
    public delegate void DungeonGeneratedEventHandler();

    [Signal]
    public delegate void PlayerEnteredRoomEventHandler(
        int roomId,
        string roomName,
        int roomType
    );

    [Signal]
    public delegate void PlayerExitedRoomEventHandler(int roomId);

    [Signal]
    public delegate void RoomExplorationChangedEventHandler(
        float ratio,
        int unlockedRoomCount,
        int totalRoomCount
    );

    [Signal]
    public delegate void RoomCompletedEventHandler(int roomId);

    [ExportCategory("Generation")]

    [Export]
    public DungeonGenerationConfig GenerationConfig { get; set; } = new();

    /// <summary>0 表示每次随机；非 0 用于复现同一张地牢。</summary>
    [Export]
    public long FixedSeed { get; set; }

    [Export]
    public int RecordingFloorSourceId { get; set; } = 1;

    [Export]
    public int OverlaySourceId { get; set; } = 2;

    [ExportCategory("Scene Nodes")]

    [Export]
    public NodePath GroundLayerPath { get; set; } =
        new("../GroundTileMapLayer");

    [Export]
    public NodePath EnemySpawnLayerPath { get; set; } =
        new("../EnemySpawnerTileLayer");

    [Export]
    public NodePath PlayerSpawnLayerPath { get; set; } =
        new("../PlayerSpawnTileLayer");

    [Export]
    public NodePath DoorBlockerLayerPath { get; set; } =
        new("../DoorBlockerTileMapLayer");

    [Export]
    public NodePath DoorIconLayerPath { get; set; } =
        new("../DoorIconTileMapLayer");

    [Export]
    public NodePath RuntimePath { get; set; } =
        new("../DungeonRuntime");

    [Export]
    public NodePath PlayerPath { get; set; } = new("../Player");

    [Export]
    public NodePath PaintCanvasPath { get; set; } =
        new("../PaintCanvas2D");

    [ExportCategory("Starting Room Weapons")]

    [Export]
    public PackedScene PencilWeaponScene { get; set; }

    [Export]
    public PackedScene ChalkWeaponScene { get; set; }

    [Export]
    public PackedScene EraserWeaponScene { get; set; }

    [ExportCategory("Room Clear Rewards")]

    [Export]
    public PackedScene SchoolBagSearchPointScene { get; set; }

    [ExportCategory("Monster Room Fence")]

    [Export(PropertyHint.Range, "0,1,0.05")]
    public float HorizontalFenceChance { get; set; } = 0.4f;

    [Export(PropertyHint.Range, "3,12,1")]
    public int MinimumHorizontalFenceLength { get; set; } = 3;

    [Export(PropertyHint.Range, "3,12,1")]
    public int MaximumHorizontalFenceLength { get; set; } = 7;

    [Export(PropertyHint.Range, "1,5,1")]
    public int HorizontalFenceWallClearance { get; set; } = 2;

    public DungeonLayout GeneratedLayout { get; private set; }

    public int UnlockedRoomCount => _unlockedRoomIds.Count;
    public int TotalRoomCount => GeneratedLayout?.Rooms.Count ?? 0;
    public float RoomExplorationRatio => TotalRoomCount == 0
        ? 0.0f
        : (float)UnlockedRoomCount / TotalRoomCount;

    private readonly RandomNumberGenerator _random = new();
    private TileMapLayer _groundLayer;
    private EnemySpawner _enemySpawner;
    private TileMapLayer _playerSpawnLayer;
    private TileMapLayer _doorBlockerLayer;
    private TileMapLayer _doorIconLayer;
    private Node _runtime;
    private Node2D _player;
    private PaintCanvas2D _paintCanvas;
    private int _playerRoomId = -1;
    private readonly HashSet<int> _schoolBagRoomIds = new();
    private readonly HashSet<int> _unlockedRoomIds = new();

    public override void _Ready()
    {
        GenerateDungeon();
    }

    public void GenerateDungeon()
    {
        if (!ResolveSceneNodes())
            return;


        ConfigureRandomSeed();
        DungeonGenerationConfig config;
        if (GenerationConfig != null)
            config = GenerationConfig;
        else
            config = new DungeonGenerationConfig();

        DungeonTilePainter painter = new(
            _groundLayer,
            _enemySpawner,
            _playerSpawnLayer,
            _doorBlockerLayer,
            _doorIconLayer,
            RecordingFloorSourceId,
            GenerationConfig,
            _random
        );

        if (!painter.ValidateRequiredAtlasTiles() || !ValidateOverlayTiles())
            return;

        DungeonLayoutGenerator layoutGenerator = new(config, _random);
        string validationError = string.Empty;
        GeneratedLayout = null;
        _schoolBagRoomIds.Clear();
        _unlockedRoomIds.Clear();

        for (
            int attempt = 1;
            attempt <= Math.Max(config.MaximumGenerationAttempts, 1);
            attempt++
        )
        {
            try
            {
                DungeonLayout candidate = layoutGenerator.Generate();

                if (
                    DungeonLayoutValidator.Validate(
                        candidate,
                        config,
                        out validationError
                    )
                )
                {
                    GeneratedLayout = candidate;
                    break;
                }
            }
            catch (Exception exception)
            {
                validationError = exception.Message;
            }

            GD.PushWarning(
                $"程序化地牢第 {attempt} 次生成无效: {validationError}"
            );
        }

        if (GeneratedLayout == null)
        {
            GD.PushError(
                $"程序化地牢生成失败: {validationError}"
            );
            return;
        }

        _enemySpawner.EndEncounter();
        painter.Paint(GeneratedLayout);
        PlacePlayerInStartRoom();
        UnlockRoomForExploration(GeneratedLayout.StartRoom);
        BuildRuntimeControllers();
        SpawnStartingWeapons();

        int monsterRoomCount = 0;
        int normalRoomCount = 0;
        int extractionRoomId = -1;

        foreach (DungeonRoomData room in GeneratedLayout.Rooms)
        {
            if (room.Type == DungeonRoomType.Monster)
                monsterRoomCount++;
            else if (room.Type == DungeonRoomType.Normal)
                normalRoomCount++;
            else if (room.Type == DungeonRoomType.Extraction)
                extractionRoomId = room.Id;
        }

        Vector2I treeDistanceRange = GetCorridorDistanceRange(
            GeneratedLayout,
            false
        );
        Vector2I crossLinkDistanceRange = GetCorridorDistanceRange(
            GeneratedLayout,
            true
        );
        int crossLinkCount = CountCorridors(
            GeneratedLayout,
            DungeonCorridorKind.CrossLink
        );

        GD.Print(
            $"[DungeonGenerator] 地牢生成完成: " +
            $"房间={GeneratedLayout.Rooms.Count}, " +
            $"怪物房={monsterRoomCount}, " +
            $"普通房={normalRoomCount}, " +
            $"撤离房={extractionRoomId}(DFS最深), " +
            $"走廊={GeneratedLayout.Corridors.Count}, " +
            $"主干/分支距离={treeDistanceRange.X}-" +
            $"{treeDistanceRange.Y}, " +
            $"跨分支={crossLinkCount}" +
            $"({crossLinkDistanceRange.X}-" +
            $"{crossLinkDistanceRange.Y}), " +
            $"转角={CountCorridorTurns(GeneratedLayout)}, " +
            $"种子={_random.Seed}"
        );
        EmitSignal(SignalName.DungeonGenerated);
    }

    public bool TryGetRoomAtGlobalPosition(
        Vector2 globalPosition,
        out DungeonRoomData room
    )
    {
        room = null;

        if (
            GeneratedLayout == null ||
            !GodotObject.IsInstanceValid(_groundLayer)
        )
        {
            return false;
        }

        Vector2 localPosition = _groundLayer.ToLocal(globalPosition);
        Vector2I cell = _groundLayer.LocalToMap(localPosition);

        foreach (DungeonRoomData candidate in GeneratedLayout.Rooms)
        {
            if (!candidate.ContainsCell(cell))
                continue;

            room = candidate;
            return true;
        }

        return false;
    }

    public void ConfigurePaintCanvasForRoom(DungeonRoomData room)
    {
        if (
            room == null ||
            !GodotObject.IsInstanceValid(_groundLayer) ||
            !GodotObject.IsInstanceValid(_paintCanvas)
        )
        {
            return;
        }

        Vector2I tileSize = _groundLayer.TileSet.TileSize;
        Vector2I canvasSize = new(
            (room.Bounds.Size.X - 2) * tileSize.X,
            (room.Bounds.Size.Y - 2) * tileSize.Y
        );
        Vector2 firstInteriorCenter = _groundLayer.MapToLocal(
            room.Bounds.Position + Vector2I.One
        );
        Vector2 halfTileSize = new(
            tileSize.X * 0.5f,
            tileSize.Y * 0.5f
        );
        Vector2 globalTopLeft = _groundLayer.ToGlobal(
            firstInteriorCenter - halfTileSize
        );
        _paintCanvas.ConfigureCanvas(canvasSize, globalTopLeft);
    }

    public void NotifyPlayerEnteredRoom(DungeonRoomData room)
    {
        if (room == null)
            return;

        _playerRoomId = room.Id;

        if (!room.Type.IsCombatRoom())
            UnlockRoomForExploration(room);

        EmitSignal(
            SignalName.PlayerEnteredRoom,
            room.Id,
            GetRoomDisplayName(room.Type),
            (int)room.Type
        );
    }

    public void NotifyPlayerExitedRoom(DungeonRoomData room)
    {
        if (room == null || _playerRoomId != room.Id)
            return;

        _playerRoomId = -1;
        EmitSignal(SignalName.PlayerExitedRoom, room.Id);
    }

    private bool ResolveSceneNodes()
    {
        _groundLayer = GetNodeOrNull<TileMapLayer>(GroundLayerPath);
        _enemySpawner = GetNodeOrNull<EnemySpawner>(EnemySpawnLayerPath);
        _playerSpawnLayer = GetNodeOrNull<TileMapLayer>(
            PlayerSpawnLayerPath
        );
        _doorBlockerLayer = GetNodeOrNull<TileMapLayer>(
            DoorBlockerLayerPath
        );
        _doorIconLayer = GetNodeOrNull<TileMapLayer>(DoorIconLayerPath);
        _runtime = GetNodeOrNull<Node>(RuntimePath);
        _player = GetNodeOrNull<Node2D>(PlayerPath);
        _paintCanvas = GetNodeOrNull<PaintCanvas2D>(PaintCanvasPath);

        if (
            _groundLayer != null &&
            _enemySpawner != null &&
            _playerSpawnLayer != null &&
            _doorBlockerLayer != null &&
            _doorIconLayer != null &&
            _runtime != null &&
            _player != null &&
            _paintCanvas != null
        )
        {
            return true;
        }

        GD.PushError(
            "DungeonGenerator 缺少地图图层、DungeonRuntime、Player " +
            "或 PaintCanvas2D 节点绑定。"
        );
        return false;
    }

    private bool ValidateOverlayTiles()
    {
        TileSet tileSet = _groundLayer.TileSet;

        if (
            tileSet == null ||
            !tileSet.HasSource(OverlaySourceId) ||
            tileSet.GetSource(OverlaySourceId)
                is not TileSetAtlasSource atlasSource
        )
        {
            GD.PushError(
                $"地图 TileSet 缺少覆盖图集 source {OverlaySourceId}。"
            );
            return false;
        }

        for (int y = 0; y <= 4; y++)
        {
            Vector2I tile = new(0, y);

            if (atlasSource.HasTile(tile))
                continue;

            GD.PushError($"01_tile_overlay_layer 缺少瓦片 {tile}。");
            return false;
        }

        return true;
    }

    private void ConfigureRandomSeed()
    {
        if (FixedSeed == 0)
        {
            _random.Randomize();
            return;
        }

        _random.Seed = unchecked((ulong)FixedSeed);
    }

    private void PlacePlayerInStartRoom()
    {
        DungeonRoomData startRoom = GeneratedLayout.StartRoom;

        if (startRoom == null)
            return;

        _player.GlobalPosition = _groundLayer.ToGlobal(
            _groundLayer.MapToLocal(startRoom.CenterCell)
        );
        ConfigurePaintCanvasForRoom(startRoom);
    }

    private void BuildRuntimeControllers()
    {
        foreach (Node child in _runtime.GetChildren())
            child.QueueFree();

        DungeonEncounterController encounterController = new()
        {
            Name = "DungeonEncounterController"
        };
        _runtime.AddChild(encounterController);
        encounterController.Configure(_enemySpawner);
        encounterController.EncounterCleared += OnEncounterCleared;

        Dictionary<int, List<DungeonDoorController>> doorsByRoom = new();

        foreach (DungeonRoomData room in GeneratedLayout.Rooms)
            doorsByRoom[room.Id] = new List<DungeonDoorController>();

        foreach (DungeonRoomData room in GeneratedLayout.Rooms)
        {
            foreach (DungeonDoorData door in room.Doors)
            {
                DungeonDoorController controller = new();
                _runtime.AddChild(controller);
                controller.Configure(
                    door,
                    _doorBlockerLayer,
                    _doorIconLayer,
                    OverlaySourceId
                );
                doorsByRoom[room.Id].Add(controller);
            }
        }

        foreach (DungeonRoomData room in GeneratedLayout.Rooms)
        {
            DungeonRoomController controller = new();
            _runtime.AddChild(controller);
            controller.Configure(
                room,
                _groundLayer,
                this,
                encounterController,
                doorsByRoom[room.Id]
            );
        }
    }

    private void SpawnStartingWeapons()
    {
        DungeonRoomData startRoom = GeneratedLayout.StartRoom;

        if (startRoom == null)
            return;

        PackedScene[] scenes =
        {
            EraserWeaponScene,
            PencilWeaponScene,
            ChalkWeaponScene
        };
        string[] displayNames =
        {
            "橡皮",
            "铅笔",
            "粉笔"
        };
        int[] horizontalOffsets = { -1, 0, 1 };

        for (int index = 0; index < scenes.Length; index++)
        {
            Vector2I dropCell = startRoom.CenterCell +
                new Vector2I(horizontalOffsets[index], 1);
            SpawnStartingWeapon(
                scenes[index],
                displayNames[index],
                dropCell
            );
        }
    }

    private void OnEncounterCleared(int roomId)
    {
        DungeonRoomData room = GeneratedLayout?.FindRoom(roomId);

        if (room == null || !room.Type.IsCombatRoom())
            return;

        UnlockRoomForExploration(room);
        EmitSignal(SignalName.RoomCompleted, roomId);

        if (
            !_schoolBagRoomIds.Add(roomId) ||
            SchoolBagSearchPointScene == null
        )
        {
            return;
        }

        Node instance = SchoolBagSearchPointScene.Instantiate();

        if (instance is not SchoolBagSearchPoint schoolBag)
        {
            GD.PushWarning("书包搜索点场景的根节点不是 SchoolBagSearchPoint。");
            instance.Free();
            return;
        }

        schoolBag.Name = $"SchoolBag_Room_{roomId}";
        schoolBag.ConfigureRoomReward(room.Id, room.EncounterDifficulty);
        _runtime.AddChild(schoolBag);
        schoolBag.GlobalPosition = _groundLayer.ToGlobal(
            _groundLayer.MapToLocal(room.CenterCell)
        );
        GD.Print($"[DungeonReward] 房间 {roomId} 中央已生成书包。");
    }

    private void UnlockRoomForExploration(DungeonRoomData room)
    {
        if (
            room == null ||
            GeneratedLayout == null ||
            !_unlockedRoomIds.Add(room.Id)
        )
        {
            return;
        }

        EmitSignal(
            SignalName.RoomExplorationChanged,
            RoomExplorationRatio,
            UnlockedRoomCount,
            TotalRoomCount
        );
        GD.Print(
            $"[DungeonExploration] 解锁房间 {room.Id}，" +
            $"进度={UnlockedRoomCount}/{TotalRoomCount}。"
        );
    }

    private void SpawnStartingWeapon(
        PackedScene weaponScene,
        string displayName,
        Vector2I cell
    )
    {
        if (weaponScene == null)
        {
            GD.PushWarning($"出生房缺少 {displayName} 武器场景绑定。");
            return;
        }

        Node preview = weaponScene.Instantiate();

        if (preview is not WeaponBase weapon)
        {
            GD.PushWarning(
                $"出生房武器 {weaponScene.ResourcePath} 不是 WeaponBase。"
            );
            preview.Free();
            return;
        }

        WeaponData data = new()
        {
            WeaponId = weaponScene.ResourcePath.GetFile().GetBaseName(),
            DisplayName = displayName,
            ControllerScene = weaponScene,
            Icon = FindSpriteWithTexture(weapon)?.Texture,
            AllowDrop = true
        };
        WeaponInstance instance = new(data);
        preview.Free();

        WeaponDropSpawner drop = new()
        {
            Name = $"StartingWeapon_{data.WeaponId}"
        };
        Vector2 globalPosition = _groundLayer.ToGlobal(
            _groundLayer.MapToLocal(cell)
        );
        drop.Initialize(instance, globalPosition);
        _runtime.AddChild(drop);
    }

    private static Sprite2D FindSpriteWithTexture(Node root)
    {
        if (root is Sprite2D sprite && sprite.Texture != null)
            return sprite;

        foreach (Node child in root.GetChildren())
        {
            Sprite2D found = FindSpriteWithTexture(child);

            if (found != null)
                return found;
        }

        return null;
    }

    private static int CountCorridorTurns(DungeonLayout layout)
    {
        int turnCount = 0;

        foreach (DungeonCorridorData corridor in layout.Corridors)
        {
            for (
                int index = 1;
                index < corridor.PathCells.Count - 1;
                index++
            )
            {
                Vector2I incoming = corridor.PathCells[index] -
                    corridor.PathCells[index - 1];
                Vector2I outgoing = corridor.PathCells[index + 1] -
                    corridor.PathCells[index];

                if (
                    incoming.X != outgoing.X ||
                    incoming.Y != outgoing.Y
                )
                {
                    turnCount++;
                }
            }
        }

        return turnCount;
    }

    private static Vector2I GetCorridorDistanceRange(
        DungeonLayout layout,
        bool crossLinks
    )
    {
        int minimum = int.MaxValue;
        int maximum = int.MinValue;

        foreach (DungeonCorridorData corridor in layout.Corridors)
        {
            if (
                (corridor.Kind == DungeonCorridorKind.CrossLink) !=
                crossLinks
            )
            {
                continue;
            }

            int distance = Math.Abs(
                corridor.FromDoor.Cell.X - corridor.ToDoor.Cell.X
            ) + Math.Abs(
                corridor.FromDoor.Cell.Y - corridor.ToDoor.Cell.Y
            );
            minimum = Math.Min(minimum, distance);
            maximum = Math.Max(maximum, distance);
        }

        return minimum == int.MaxValue
            ? Vector2I.Zero
            : new Vector2I(minimum, maximum);
    }

    private static int CountCorridors(
        DungeonLayout layout,
        DungeonCorridorKind kind
    )
    {
        int count = 0;

        foreach (DungeonCorridorData corridor in layout.Corridors)
        {
            if (corridor.Kind == kind)
                count++;
        }

        return count;
    }

    private static string GetRoomDisplayName(DungeonRoomType roomType)
    {
        return roomType switch
        {
            DungeonRoomType.Start => "出生地点",
            DungeonRoomType.Monster => "怪物房",
            DungeonRoomType.Shop => "商品房",
            DungeonRoomType.Extraction => "撤离房",
            _ => "普通房"
        };
    }
}
