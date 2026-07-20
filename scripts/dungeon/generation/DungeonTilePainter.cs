using Godot;
using System.Collections.Generic;

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
    private static readonly Vector2I TurnWithLeftLegTile = new(3, 10);
    private static readonly Vector2I TurnWithRightLegTile = new(11, 10);

    private readonly TileMapLayer _groundLayer;
    private readonly TileMapLayer _enemySpawnLayer;
    private readonly TileMapLayer _playerSpawnLayer;
    private readonly TileMapLayer _doorBlockerLayer;
    private readonly TileMapLayer _doorIconLayer;
    private readonly int _recordingFloorSourceId;

    public DungeonTilePainter(
        TileMapLayer groundLayer,
        TileMapLayer enemySpawnLayer,
        TileMapLayer playerSpawnLayer,
        TileMapLayer doorBlockerLayer,
        TileMapLayer doorIconLayer,
        int recordingFloorSourceId
    )
    {
        _groundLayer = groundLayer;
        _enemySpawnLayer = enemySpawnLayer;
        _playerSpawnLayer = playerSpawnLayer;
        _doorBlockerLayer = doorBlockerLayer;
        _doorIconLayer = doorIconLayer;
        _recordingFloorSourceId = recordingFloorSourceId;
    }

    public bool ValidateRequiredAtlasTiles()
    {
        TileSet tileSet = _groundLayer?.TileSet;

        if(
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
            TurnWithLeftLegTile,
            TurnWithRightLegTile
        };

        foreach(Vector2I atlasCoordinates in requiredTiles)
        {
            if(atlasSource.HasTile(atlasCoordinates))
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

        foreach(DungeonRoomData room in layout.Rooms)
            PaintRoom(room);

        foreach(DungeonCorridorData corridor in layout.Corridors)
            PaintCorridor(corridor);

        foreach(DungeonRoomData room in layout.Rooms)
        {
            foreach(DungeonDoorData door in room.Doors)
                PaintOpenDoor(door);

            foreach(Vector2I spawnCell in room.EnemySpawnCells)
            {
                SetRecordingTile(
                    _enemySpawnLayer,
                    spawnCell,
                    EnemySpawnTile
                );
            }
        }

        DungeonRoomData startRoom = layout.StartRoom;

        if(startRoom != null)
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

        for(int y = bounds.Position.Y; y <= bottom; y++)
        {
            for(int x = bounds.Position.X; x <= right; x++)
            {
                SetRecordingTile(
                    _groundLayer,
                    new Vector2I(x, y),
                    FloorTile
                );
            }
        }

        for(int x = bounds.Position.X + 1; x < right; x++)
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

        for(int y = bounds.Position.Y + 1; y < bottom; y++)
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
        foreach(Vector2I cell in door.GetOccupiedCells())
            SetRecordingTile(_groundLayer, cell, FloorTile);

        switch(door.Direction)
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

        for(int index = 0; index < path.Count; index++)
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

            if(isTurn)
            {
                bool hasLeftLeg = previous.X < cell.X || next.X < cell.X;
                SetRecordingTile(
                    _groundLayer,
                    cell,
                    hasLeftLeg
                        ? TurnWithLeftLegTile
                        : TurnWithRightLegTile
                );
                continue;
            }

            if(incoming.X != 0 || outgoing.X != 0)
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
        Vector2I atlasCoordinates
    )
    {
        layer.SetCell(
            cell,
            _recordingFloorSourceId,
            atlasCoordinates,
            0
        );
    }
}
