using Godot;
using System;
using System.Collections.Generic;

/// <summary>
/// 在运行时使用 01_recording_floor_room 图集生成一个矩形房间，
/// 并同步布置玩家出生点与敌人生成点。
/// </summary>
[GlobalClass]
public partial class RectangularRoomGenerator : Node
{
    private const int MinimumAllowedLength = 16;
    private const int MaximumAllowedLength = 26;
    private const int MinimumAllowedWidth = 10;
    private const int MaximumAllowedWidth = 18;
    private const int EnemySpawnCount = 2;
    private const int EnemyWallClearance = 2;

    private static readonly Vector2I FloorTile = new(8, 4);
    private static readonly Vector2I TopLeftCornerTile = new(0, 0);
    private static readonly Vector2I TopRightCornerTile = new(14, 0);
    private static readonly Vector2I BottomLeftCornerTile = new(0, 14);
    private static readonly Vector2I BottomRightCornerTile = new(14, 14);
    private static readonly Vector2I TopWallTile = new(3, 0);
    private static readonly Vector2I LeftWallTile = new(0, 1);
    private static readonly Vector2I RightWallTile = new(14, 1);
    private static readonly Vector2I BottomWallTile = new(1, 14);
    private static readonly Vector2I EnemySpawnTile = new(7, 11);
    private static readonly Vector2I PlayerSpawnTile = new(6, 2);

    [ExportCategory("Room Size In Tiles")]

    [Export(PropertyHint.Range, "16,26,1")]
    public int MinimumRoomLength { get; set; } = MinimumAllowedLength;

    [Export(PropertyHint.Range, "16,26,1")]
    public int MaximumRoomLength { get; set; } = MaximumAllowedLength;

    [Export(PropertyHint.Range, "10,18,1")]
    public int MinimumRoomWidth { get; set; } = MinimumAllowedWidth;

    [Export(PropertyHint.Range, "10,18,1")]
    public int MaximumRoomWidth { get; set; } = MaximumAllowedWidth;

    [ExportCategory("Tile Atlas")]

    /// <summary>01_recording_floor_room 在 GroundTileMapLayer TileSet 中的 source id。</summary>
    [Export]
    public int RecordingFloorSourceId { get; set; } = 1;

    [ExportCategory("Scene Nodes")]

    [Export]
    public NodePath GroundLayerPath { get; set; } = new("../GroundTileMapLayer");

    [Export]
    public NodePath EnemySpawnLayerPath { get; set; } = new("../EnemySpawnerTileLayer");

    [Export]
    public NodePath PlayerSpawnLayerPath { get; set; } = new("../PlayerSpawnTileLayer");

    [Export]
    public NodePath PlayerPath { get; set; } = new("../Player");

    [Export]
    public NodePath PaintCanvasPath { get; set; } = new("../PaintCanvas2D");

    public int GeneratedRoomLength { get; private set; }
    public int GeneratedRoomWidth { get; private set; }
    public Vector2I GeneratedPlayerSpawnCell { get; private set; }

    private readonly RandomNumberGenerator _random = new();
    private readonly List<Vector2I> _enemySpawnCells = new();

    private TileMapLayer _groundLayer;
    private TileMapLayer _enemySpawnLayer;
    private TileMapLayer _playerSpawnLayer;
    private Node2D _player;
    private PaintCanvas2D _paintCanvas;

    public override void _Ready()
    {
        _random.Randomize();
        GenerateRoom();
    }

    public void GenerateRoom()
    {
        if (!ResolveSceneNodes() || !ValidateRequiredAtlasTiles())
            return;

        int lengthA = Math.Clamp(
            MinimumRoomLength,
            MinimumAllowedLength,
            MaximumAllowedLength
        );
        int lengthB = Math.Clamp(
            MaximumRoomLength,
            MinimumAllowedLength,
            MaximumAllowedLength
        );
        int widthA = Math.Clamp(
            MinimumRoomWidth,
            MinimumAllowedWidth,
            MaximumAllowedWidth
        );
        int widthB = Math.Clamp(
            MaximumRoomWidth,
            MinimumAllowedWidth,
            MaximumAllowedWidth
        );

        GeneratedRoomLength = _random.RandiRange(
            Math.Min(lengthA, lengthB),
            Math.Max(lengthA, lengthB)
        );
        GeneratedRoomWidth = _random.RandiRange(
            Math.Min(widthA, widthB),
            Math.Max(widthA, widthB)
        );

        PrepareLayers();
        PaintFloorAndWalls();
        ConfigurePaintCanvas();
        PlacePlayerSpawn();
        PlaceEnemySpawns();

        GD.Print(
            $"[RectangularRoomGenerator] 房间生成完成: " +
            $"长={GeneratedRoomLength}, 宽={GeneratedRoomWidth}, " +
            $"画布={_paintCanvas.CanvasSize}, " +
            $"玩家出生点={GeneratedPlayerSpawnCell}, " +
            $"敌人生成点={string.Join(", ", _enemySpawnCells)}"
        );
    }

    private bool ResolveSceneNodes()
    {
        _groundLayer = GetNodeOrNull<TileMapLayer>(GroundLayerPath);
        _enemySpawnLayer = GetNodeOrNull<TileMapLayer>(EnemySpawnLayerPath);
        _playerSpawnLayer = GetNodeOrNull<TileMapLayer>(PlayerSpawnLayerPath);
        _player = GetNodeOrNull<Node2D>(PlayerPath);
        _paintCanvas = GetNodeOrNull<PaintCanvas2D>(PaintCanvasPath);

        if (
            _groundLayer != null &&
            _enemySpawnLayer != null &&
            _playerSpawnLayer != null &&
            _player != null &&
            _paintCanvas != null
        )
        {
            return true;
        }

        GD.PushError(
            "RectangularRoomGenerator 缺少 GroundTileMapLayer、" +
            "EnemySpawnerTileLayer、PlayerSpawnTileLayer、Player " +
            "或 PaintCanvas2D 节点绑定。"
        );
        return false;
    }

    private bool ValidateRequiredAtlasTiles()
    {
        TileSet tileSet = _groundLayer.TileSet;

        if (tileSet == null || !tileSet.HasSource(RecordingFloorSourceId))
        {
            GD.PushError(
                $"GroundTileMapLayer 的 TileSet 中不存在 source " +
                $"{RecordingFloorSourceId}。"
            );
            return false;
        }

        if (
            tileSet.GetSource(RecordingFloorSourceId)
            is not TileSetAtlasSource atlasSource
        )
        {
            GD.PushError(
                $"TileSet source {RecordingFloorSourceId} 不是图集类型。"
            );
            return false;
        }

        Vector2I[] requiredTiles =
        {
            FloorTile,
            TopLeftCornerTile,
            TopRightCornerTile,
            BottomLeftCornerTile,
            BottomRightCornerTile,
            TopWallTile,
            LeftWallTile,
            RightWallTile,
            BottomWallTile,
            EnemySpawnTile,
            PlayerSpawnTile
        };

        foreach (Vector2I atlasCoordinates in requiredTiles)
        {
            if (atlasSource.HasTile(atlasCoordinates))
                continue;

            GD.PushError(
                $"01_recording_floor_room 缺少图集瓦片 {atlasCoordinates}。"
            );
            return false;
        }

        return true;
    }

    private void PrepareLayers()
    {
        _groundLayer.Clear();
        _enemySpawnLayer.Clear();
        _playerSpawnLayer.Clear();

        // 三个图层共用同一个 TileSet，确保标记点使用同一张 48x48 图集。
        _enemySpawnLayer.TileSet = _groundLayer.TileSet;
        _playerSpawnLayer.TileSet = _groundLayer.TileSet;
    }

    private void PaintFloorAndWalls()
    {
        for (int y = 0; y < GeneratedRoomWidth; y++)
        {
            for (int x = 0; x < GeneratedRoomLength; x++)
                SetRecordingTile(_groundLayer, new Vector2I(x, y), FloorTile);
        }

        for (int x = 1; x < GeneratedRoomLength - 1; x++)
        {
            SetRecordingTile(_groundLayer, new Vector2I(x, 0), TopWallTile);
            SetRecordingTile(
                _groundLayer,
                new Vector2I(x, GeneratedRoomWidth - 1),
                BottomWallTile
            );
        }

        for (int y = 1; y < GeneratedRoomWidth - 1; y++)
        {
            SetRecordingTile(_groundLayer, new Vector2I(0, y), LeftWallTile);
            SetRecordingTile(
                _groundLayer,
                new Vector2I(GeneratedRoomLength - 1, y),
                RightWallTile
            );
        }

        SetRecordingTile(_groundLayer, Vector2I.Zero, TopLeftCornerTile);
        SetRecordingTile(
            _groundLayer,
            new Vector2I(GeneratedRoomLength - 1, 0),
            TopRightCornerTile
        );
        SetRecordingTile(
            _groundLayer,
            new Vector2I(0, GeneratedRoomWidth - 1),
            BottomLeftCornerTile
        );
        SetRecordingTile(
            _groundLayer,
            new Vector2I(
                GeneratedRoomLength - 1,
                GeneratedRoomWidth - 1
            ),
            BottomRightCornerTile
        );
    }

    private void ConfigurePaintCanvas()
    {
        Vector2I tileSize = _groundLayer.TileSet.TileSize;
        Vector2I canvasSize = new(
            (GeneratedRoomLength - 2) * tileSize.X,
            (GeneratedRoomWidth - 2) * tileSize.Y
        );

        Vector2 firstInteriorCellCenter =
            _groundLayer.MapToLocal(Vector2I.One);
        Vector2 halfTileSize = new(
            tileSize.X * 0.5f,
            tileSize.Y * 0.5f
        );
        Vector2 globalTopLeft = _groundLayer.ToGlobal(
            firstInteriorCellCenter - halfTileSize
        );

        _paintCanvas.ConfigureCanvas(canvasSize, globalTopLeft);
    }

    private void PlacePlayerSpawn()
    {
        GeneratedPlayerSpawnCell = new Vector2I(
            GeneratedRoomLength / 2,
            GeneratedRoomWidth / 2
        );

        SetRecordingTile(
            _playerSpawnLayer,
            GeneratedPlayerSpawnCell,
            PlayerSpawnTile
        );

        _player.GlobalPosition = _groundLayer.ToGlobal(
            _groundLayer.MapToLocal(GeneratedPlayerSpawnCell)
        );
    }

    private void PlaceEnemySpawns()
    {
        _enemySpawnCells.Clear();

        int minimumX = EnemyWallClearance + 1;
        int maximumX = GeneratedRoomLength - EnemyWallClearance - 2;
        int minimumY = EnemyWallClearance + 1;
        int maximumY = GeneratedRoomWidth - EnemyWallClearance - 2;

        var occupiedCells = new HashSet<Vector2I>
        {
            GeneratedPlayerSpawnCell
        };

        while (_enemySpawnCells.Count < EnemySpawnCount)
        {
            var spawnCell = new Vector2I(
                _random.RandiRange(minimumX, maximumX),
                _random.RandiRange(minimumY, maximumY)
            );

            if (!occupiedCells.Add(spawnCell))
                continue;

            _enemySpawnCells.Add(spawnCell);
            SetRecordingTile(_enemySpawnLayer, spawnCell, EnemySpawnTile);
        }
    }

    private void SetRecordingTile(
        TileMapLayer layer,
        Vector2I cell,
        Vector2I atlasCoordinates
    )
    {
        layer.SetCell(
            cell,
            RecordingFloorSourceId,
            atlasCoordinates,
            0
        );
    }
}
