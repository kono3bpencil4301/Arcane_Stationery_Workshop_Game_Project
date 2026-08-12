using Godot;
using System.Collections.Generic;
using System;

public sealed class DungeonTilePainter
{
    public static readonly Vector2I FloorTile = new(8, 4);
    public static readonly Vector2I EnemySpawnTile = new(7, 11);
    public static readonly Vector2I PlayerSpawnTile = new(6, 2);

    private static readonly Vector2I TopLeftCornerTile = new(0, 0);
    private static readonly Vector2I TopRightCornerTile = new(14, 0);
    private static readonly Vector2I BottomLeftCornerTile = new(0, 14);
    private static readonly Vector2I BottomRightCornerTile = new(14, 14);
    private static readonly Vector2I TopWallTile = new(3, 0);
    private static readonly Vector2I LeftWallTile = new(0, 1);
    private static readonly Vector2I RightWallTile = new(14, 1);
    private static readonly Vector2I BottomWallTile = new(1, 14);

    private static readonly Vector2I RightDoorUpperWallTile = new(14, 8);
    private static readonly Vector2I RightDoorLowerWallTile = new(14, 10);
    private static readonly Vector2I LeftDoorUpperWallTile = new(0, 8);
    private static readonly Vector2I LeftDoorLowerWallTile = new(0, 10);
    private static readonly Vector2I TopDoorLeftWallTile = new(8, 0);
    private static readonly Vector2I TopDoorRightWallTile = new(10, 0);
    private static readonly Vector2I BottomDoorLeftWallTile = new(8, 14);
    private static readonly Vector2I BottomDoorRightWallTile = new(10, 14);

    private static readonly Vector2I VerticalLeftConnectionTile = new(11, 11);
    private static readonly Vector2I VerticalLeftEndTile = new(11, 12);
    private static readonly Vector2I VerticalRightConnectionTile = new(3, 11);
    private static readonly Vector2I VerticalRightEndTile = new(3, 12);
    private static readonly Vector2I HorizontalEdgeTile = new(6, 10);
    private static readonly Vector2I HorizontalCenterTile = new(7, 5);
    private static readonly Vector2I DownTurnLeftInnerTile = new(1, 8);
    private static readonly Vector2I DownTurnLeftOuterTile = new(2, 9);
    private static readonly Vector2I DownTurnRightInnerTile = new(13, 8);
    private static readonly Vector2I DownTurnRightOuterTile = new(12, 9);
    private static readonly Vector2I HorizontalFenceLeftTile = new(1, 7);
    private static readonly Vector2I HorizontalFenceLRMidTile = new(2, 7);
    private static readonly Vector2I HorizontalFenceRightTile = new(3, 7);

    private readonly TileMapLayer _groundLayer;
    private readonly TileMapLayer _enemySpawnLayer;
    private readonly TileMapLayer _playerSpawnLayer;
    private readonly TileMapLayer _doorBlockerLayer;
    private readonly TileMapLayer _doorIconLayer;
    private readonly int _recordingFloorSourceId;

    private readonly DungeonGenerationConfig _config;
    private readonly RandomNumberGenerator _random;

    public DungeonTilePainter(
        TileMapLayer groundLayer,
        TileMapLayer enemySpawnLayer,
        TileMapLayer playerSpawnLayer,
        TileMapLayer doorBlockerLayer,
        TileMapLayer doorIconLayer,
        int recordingFloorSourceId,
        DungeonGenerationConfig config,
        RandomNumberGenerator random
    )
    {
        _groundLayer = groundLayer;
        _enemySpawnLayer = enemySpawnLayer;
        _playerSpawnLayer = playerSpawnLayer;
        _doorBlockerLayer = doorBlockerLayer;
        _doorIconLayer = doorIconLayer;
        _recordingFloorSourceId = recordingFloorSourceId;
        _config = config;
        _random = random;
    }

    public bool ValidateRequiredAtlasTiles()
    {
        TileSet tileSet = _groundLayer?.TileSet;

        if (
            tileSet == null ||
            !tileSet.HasSource(_recordingFloorSourceId) ||
            tileSet.GetSource(_recordingFloorSourceId)
                is not TileSetAtlasSource atlasSource
        )
        {
            GD.PushError(
                $"GroundTileMapLayer 缺少图集 source " +
                $"{_recordingFloorSourceId}。"
            );
            return false;
        }

        Vector2I[] requiredTiles =
        {
            FloorTile,
            EnemySpawnTile,
            PlayerSpawnTile,
            TopLeftCornerTile,
            TopRightCornerTile,
            BottomLeftCornerTile,
            BottomRightCornerTile,
            TopWallTile,
            LeftWallTile,
            RightWallTile,
            BottomWallTile,
            RightDoorUpperWallTile,
            RightDoorLowerWallTile,
            LeftDoorUpperWallTile,
            LeftDoorLowerWallTile,
            TopDoorLeftWallTile,
            TopDoorRightWallTile,
            BottomDoorLeftWallTile,
            BottomDoorRightWallTile,
            VerticalLeftConnectionTile,
            VerticalLeftEndTile,
            VerticalRightConnectionTile,
            VerticalRightEndTile,
            HorizontalEdgeTile,
            HorizontalCenterTile,
            DownTurnLeftInnerTile,
            DownTurnLeftOuterTile,
            DownTurnRightInnerTile,
            DownTurnRightOuterTile,
            HorizontalFenceLeftTile,
            HorizontalFenceLRMidTile,
            HorizontalFenceRightTile
        };

        foreach (Vector2I atlasCoordinates in requiredTiles)
        {
            if (atlasSource.HasTile(atlasCoordinates))
                continue;

            GD.PushError(
                $"01_recording_floor_room 缺少图集瓦片 " +
                $"{atlasCoordinates}。"
            );
            return false;
        }

        return true;
    }

    public void Paint(DungeonLayout layout)
    {
        PrepareLayers();

        foreach (DungeonRoomData room in layout.Rooms)
            PaintRoom(room);

        foreach (DungeonCorridorData corridor in layout.Corridors)
            PaintCorridor(corridor);

        foreach (DungeonRoomData room in layout.Rooms)
        {
            foreach (DungeonDoorData door in room.Doors)
                PaintOpenDoor(door);

            foreach (Vector2I spawnCell in room.EnemySpawnCells)
            {
                SetRecordingTile(
                    _enemySpawnLayer,
                    spawnCell,
                    EnemySpawnTile
                );
            }
        }

        foreach (DungeonRoomData room in layout.Rooms)
            PaintHorizontalFence(room);

        foreach (DungeonRoomData room in layout.Rooms)
        {
            PaintHorizontalFence(room);
        }
        DungeonRoomData startRoom = layout.StartRoom;

        if (startRoom != null)
        {
            SetRecordingTile(
                _playerSpawnLayer,
                startRoom.CenterCell,
                PlayerSpawnTile
            );
        }
    }

    private void PrepareLayers()
    {
        _groundLayer.Clear();
        _enemySpawnLayer.Clear();
        _playerSpawnLayer.Clear();
        _doorBlockerLayer.Clear();
        _doorIconLayer.Clear();

        TileSet sharedTileSet = _groundLayer.TileSet;
        _enemySpawnLayer.TileSet = sharedTileSet;
        _playerSpawnLayer.TileSet = sharedTileSet;
        _doorBlockerLayer.TileSet = sharedTileSet;
        _doorIconLayer.TileSet = sharedTileSet;
    }

    private void PaintRoom(DungeonRoomData room)
    {
        Rect2I bounds = room.Bounds;
        int right = bounds.Position.X + bounds.Size.X - 1;
        int bottom = bounds.Position.Y + bounds.Size.Y - 1;

        for (int y = bounds.Position.Y; y <= bottom; y++)
        {
            for (int x = bounds.Position.X; x <= right; x++)
            {
                SetRecordingTile(
                    _groundLayer,
                    new Vector2I(x, y),
                    FloorTile
                );
            }
        }

        for (int x = bounds.Position.X + 1; x < right; x++)
        {
            SetRecordingTile(
                _groundLayer,
                new Vector2I(x, bounds.Position.Y),
                TopWallTile
            );
            SetRecordingTile(
                _groundLayer,
                new Vector2I(x, bottom),
                BottomWallTile
            );
        }

        for (int y = bounds.Position.Y + 1; y < bottom; y++)
        {
            SetRecordingTile(
                _groundLayer,
                new Vector2I(bounds.Position.X, y),
                LeftWallTile
            );
            SetRecordingTile(
                _groundLayer,
                new Vector2I(right, y),
                RightWallTile
            );
        }

        SetRecordingTile(
            _groundLayer,
            bounds.Position,
            TopLeftCornerTile
        );
        SetRecordingTile(
            _groundLayer,
            new Vector2I(right, bounds.Position.Y),
            TopRightCornerTile
        );
        SetRecordingTile(
            _groundLayer,
            new Vector2I(bounds.Position.X, bottom),
            BottomLeftCornerTile
        );
        SetRecordingTile(
            _groundLayer,
            new Vector2I(right, bottom),
            BottomRightCornerTile
        );
    }

    private void PaintOpenDoor(DungeonDoorData door)
    {
        foreach (Vector2I cell in door.GetOccupiedCells())
            SetRecordingTile(_groundLayer, cell, FloorTile);

        switch (door.Direction)
        {
            case DungeonDirection.Right:
                SetRecordingTile(
                    _groundLayer,
                    door.Cell + Vector2I.Up,
                    RightDoorUpperWallTile
                );
                SetRecordingTile(
                    _groundLayer,
                    door.Cell + Vector2I.Down,
                    RightDoorLowerWallTile
                );
                break;

            case DungeonDirection.Left:
                SetRecordingTile(
                    _groundLayer,
                    door.Cell + Vector2I.Up,
                    LeftDoorUpperWallTile
                );
                SetRecordingTile(
                    _groundLayer,
                    door.Cell + Vector2I.Down,
                    LeftDoorLowerWallTile
                );
                break;

            case DungeonDirection.Up:
                SetRecordingTile(
                    _groundLayer,
                    door.Cell + Vector2I.Left * 2,
                    TopDoorLeftWallTile
                );
                SetRecordingTile(
                    _groundLayer,
                    door.Cell + Vector2I.Right,
                    TopDoorRightWallTile
                );
                break;

            case DungeonDirection.Down:
                SetRecordingTile(
                    _groundLayer,
                    door.Cell + Vector2I.Left * 2,
                    BottomDoorLeftWallTile
                );
                SetRecordingTile(
                    _groundLayer,
                    door.Cell + Vector2I.Right,
                    BottomDoorRightWallTile
                );
                break;
        }
    }

    private void PaintCorridor(DungeonCorridorData corridor)
    {
        IReadOnlyList<Vector2I> path = corridor.PathCells;

        for (int index = 0; index < path.Count; index++)
        {
            Vector2I cell = path[index];
            Vector2I previous = index == 0
                ? corridor.FromDoor.Cell
                : path[index - 1];
            Vector2I next = index == path.Count - 1
                ? corridor.ToDoor.Cell
                : path[index + 1];
            Vector2I incoming = cell - previous;
            Vector2I outgoing = next - cell;
            bool isTurn = incoming.X != 0 && outgoing.Y != 0 ||
                incoming.Y != 0 && outgoing.X != 0;

            if (isTurn)
            {
                continue;
            }

            if (incoming.X != 0 || outgoing.X != 0)
            {
                PaintHorizontalCorridorCell(cell);
                continue;
            }

            bool reachesRoomWhileMovingDown =
                index == path.Count - 1 &&
                next.Y > cell.Y;
            PaintVerticalCorridorCell(
                cell,
                reachesRoomWhileMovingDown
            );
        }

        // Turn pieces are applied last so neighboring straight cells cannot
        // overwrite the inner-radius tile shared by both corridor footprints.
        for (int index = 0; index < path.Count; index++)
        {
            Vector2I cell = path[index];
            Vector2I previous = index == 0
                ? corridor.FromDoor.Cell
                : path[index - 1];
            Vector2I next = index == path.Count - 1
                ? corridor.ToDoor.Cell
                : path[index + 1];
            Vector2I incoming = cell - previous;
            Vector2I outgoing = next - cell;
            bool isTurn = incoming.X != 0 && outgoing.Y != 0 ||
                incoming.Y != 0 && outgoing.X != 0;

            if (isTurn)
                PaintCorridorTurn(previous, cell, next);
        }
    }

    private void PaintCorridorTurn(
        Vector2I previous,
        Vector2I cell,
        Vector2I next
    )
    {
        foreach (TurnTilePlacement placement in GetTurnTilePlacements(
            previous,
            cell,
            next
        ))
        {
            SetRecordingTile(
                _groundLayer,
                cell + placement.Offset,
                placement.AtlasCoordinates,
                placement.AlternativeTile
            );
        }
    }

    internal static TurnTilePlacement[] GetTurnTilePlacements(
        Vector2I previous,
        Vector2I cell,
        Vector2I next
    )
    {
        bool hasLeftLeg = previous.X < cell.X || next.X < cell.X;
        bool hasUpperLeg = previous.Y < cell.Y || next.Y < cell.Y;

        if (hasUpperLeg && hasLeftLeg)
        {
            return new TurnTilePlacement[]
            {
                new(Vector2I.Left + Vector2I.Up, DownTurnLeftInnerTile),
                new(Vector2I.Left, HorizontalCenterTile),
                new(Vector2I.Zero, VerticalRightConnectionTile),
                new(Vector2I.Down, DownTurnLeftOuterTile)
            };
        }

        if (hasUpperLeg)
        {
            return new TurnTilePlacement[]
            {
                new(Vector2I.Up, DownTurnRightInnerTile),
                new(Vector2I.Left, VerticalLeftConnectionTile),
                new(Vector2I.Zero, HorizontalCenterTile),
                new(Vector2I.Left + Vector2I.Down, DownTurnRightOuterTile)
            };
        }

        int flipVertically = (int)TileSetAtlasSource.TransformFlipV;

        if (hasLeftLeg)
        {
            return new TurnTilePlacement[]
            {
                new(
                    Vector2I.Left + Vector2I.Down,
                    DownTurnLeftInnerTile,
                    flipVertically
                ),
                new(Vector2I.Left, HorizontalCenterTile),
                new(Vector2I.Zero, VerticalRightConnectionTile),
                new(
                    Vector2I.Up,
                    DownTurnLeftOuterTile,
                    flipVertically
                )
            };
        }

        return new TurnTilePlacement[]
        {
            new(
                Vector2I.Down,
                DownTurnRightInnerTile,
                flipVertically
            ),
            new(Vector2I.Left, VerticalLeftConnectionTile),
            new(Vector2I.Zero, HorizontalCenterTile),
            new(
                Vector2I.Left + Vector2I.Up,
                DownTurnRightOuterTile,
                flipVertically
            )
        };
    }

    internal readonly struct TurnTilePlacement
    {
        public Vector2I Offset { get; }
        public Vector2I AtlasCoordinates { get; }
        public int AlternativeTile { get; }

        public TurnTilePlacement(
            Vector2I offset,
            Vector2I atlasCoordinates,
            int alternativeTile = 0
        )
        {
            Offset = offset;
            AtlasCoordinates = atlasCoordinates;
            AlternativeTile = alternativeTile;
        }
    }

    private void PaintHorizontalCorridorCell(Vector2I cell)
    {
        SetRecordingTile(
            _groundLayer,
            cell + Vector2I.Up,
            HorizontalEdgeTile
        );
        SetRecordingTile(
            _groundLayer,
            cell,
            HorizontalCenterTile
        );
        SetRecordingTile(
            _groundLayer,
            cell + Vector2I.Down,
            HorizontalEdgeTile
        );
    }

    private void PaintVerticalCorridorCell(
        Vector2I cell,
        bool isDownwardEnd
    )
    {
        SetRecordingTile(
            _groundLayer,
            cell + Vector2I.Left,
            isDownwardEnd
                ? VerticalLeftEndTile
                : VerticalLeftConnectionTile
        );
        SetRecordingTile(
            _groundLayer,
            cell,
            isDownwardEnd
                ? VerticalRightEndTile
                : VerticalRightConnectionTile
        );
    }

    private void SetRecordingTile(
        TileMapLayer layer,
        Vector2I cell,
        Vector2I atlasCoordinates,
        int alternativeTile = 0
    )
    {
        layer.SetCell(
            cell,
            _recordingFloorSourceId,
            atlasCoordinates,
            alternativeTile
        );
    }
    private void PaintHorizontalFence(DungeonRoomData room)
    {
        if (room.Type != DungeonRoomType.Monster)
            return;

        if (_random.Randf() > _config.HorizontalFenceChance)
            return;

        int clearance = Math.Max(_config.HorizontalFenceWallClearance, (byte)1);

        int roomLeft = room.Bounds.Position.X;
        int roomTop = room.Bounds.Position.Y;
        int roomRight = room.Bounds.End.X - 1;
        int roomBottom = room.Bounds.End.Y - 1;

        int minimumX = roomLeft + 1 + clearance;
        int maximumX = roomRight - 1 - clearance;
        int minimumY = roomTop + 1 + clearance;
        int maximumY = roomBottom - 1 - clearance;

        int availableWidth = maximumX - minimumX + 1;

        if (
                availableWidth <
                _config.MinimumHorizontalFenceLength ||
                minimumY > maximumY
            )
        {
            return;
        }

        int minimumLength = Math.Clamp(
            _config.MinimumHorizontalFenceLength,
            3,
            availableWidth
        );

        int maximumLength = Math.Clamp(
            _config.MaximumHorizontalFenceLength,
            minimumLength,
            availableWidth
        );

        int fenceLength = _random.RandiRange(
            minimumLength,
            maximumLength
        );

        // 尝试寻找一个不会覆盖重要位置的地点。
        const int MaximumPlacementAttempts = 20;

        for (
            int attempt = 0;
            attempt < MaximumPlacementAttempts;
            attempt++
        )
        {
            int startX = _random.RandiRange(
                minimumX,
                maximumX - fenceLength + 1
            );

            int y = _random.RandiRange(
                minimumY,
                maximumY
            );

            if (!CanPlaceHorizontalFence(
                room,
                startX,
                y,
                fenceLength
            ))
            {
                continue;
            }

            for (int offset = 0; offset < fenceLength; offset++)
            {
                Vector2I atlasCoordinates =
                    offset == 0
                        ? HorizontalFenceLeftTile
                        : offset == fenceLength - 1
                            ? HorizontalFenceRightTile
                            : HorizontalFenceLRMidTile;

                SetRecordingTile(
                    _groundLayer,
                    new Vector2I(startX + offset, y),
                    atlasCoordinates
                );
            }

            return;
        }
    }
    private bool CanPlaceHorizontalFence(
    DungeonRoomData room,
    int startX,
    int y,
    int length
)
    {
        for (int offset = 0; offset < length; offset++)
        {
            Vector2I cell = new(startX + offset, y);

            // 房间清理后的书包会生成在中心，所以中心附近要留空。
            if (
                Math.Abs(cell.X - room.CenterCell.X) <= 1 &&
                Math.Abs(cell.Y - room.CenterCell.Y) <= 1
            )
            {
                return false;
            }

            // 不允许压住敌人生成点。
            foreach (Vector2I spawnCell in room.EnemySpawnCells)
            {
                if (
                    Math.Abs(cell.X - spawnCell.X) <= 1 &&
                    Math.Abs(cell.Y - spawnCell.Y) <= 1
                )
                {
                    return false;
                }
            }

            // 不允许靠近门。
            foreach (DungeonDoorData door in room.Doors)
            {
                foreach (Vector2I doorCell in door.GetOccupiedCells())
                {
                    int distance =
                        Math.Abs(cell.X - doorCell.X) +
                        Math.Abs(cell.Y - doorCell.Y);

                    if (distance <= 3)
                        return false;
                }
            }
        }

        return true;
    }
}
