using Godot;
using System.Collections.Generic;

public partial class DungeonRoomController : Area2D
{
    public DungeonRoomData Room { get; private set; }

    private readonly List<DungeonDoorController> _doors = new();
    private DungeonEncounterController _encounterController;
    private DungeonGenerator _dungeonGenerator;

    public override void _Ready()
    {
        Monitoring = true;
        CollisionLayer = 0;
        CollisionMask = 0;
        SetCollisionMaskValue(2, true);
        BodyEntered += OnBodyEntered;
        BodyExited += OnBodyExited;
        AddToGroup("dungeon_rooms");
    }

    public void Configure(
        DungeonRoomData room,
        TileMapLayer groundLayer,
        DungeonGenerator dungeonGenerator,
        DungeonEncounterController encounterController,
        IReadOnlyList<DungeonDoorController> doors
    )
    {
        Room = room;
        _dungeonGenerator = dungeonGenerator;
        _encounterController = encounterController;
        Name = $"Room_{room.Id}_{room.Type}";
        _doors.Clear();

        foreach(DungeonDoorController door in doors)
            _doors.Add(door);

        Vector2I tileSize = groundLayer.TileSet.TileSize;
        Vector2I firstInteriorCell = room.Bounds.Position + Vector2I.One;
        Vector2I lastInteriorCell = room.Bounds.Position +
            room.Bounds.Size - new Vector2I(2, 2);
        Vector2 firstCenter = groundLayer.MapToLocal(firstInteriorCell);
        Vector2 lastCenter = groundLayer.MapToLocal(lastInteriorCell);
        GlobalPosition = groundLayer.ToGlobal(
            (firstCenter + lastCenter) * 0.5f
        );

        RectangleShape2D shape = new()
        {
            Size = new Vector2(
                (room.Bounds.Size.X - 2) * tileSize.X,
                (room.Bounds.Size.Y - 2) * tileSize.Y
            )
        };
        CollisionShape2D collisionShape = new()
        {
            Name = "RoomInterior",
            Shape = shape
        };
        AddChild(collisionShape);
    }

    private void OnBodyEntered(Node2D body)
    {
        if(body is not Player || Room == null)
            return;

        Room.Visited = true;
        _dungeonGenerator?.NotifyPlayerEnteredRoom(Room);
        _dungeonGenerator?.ConfigurePaintCanvasForRoom(Room);

        if(Room.Type.IsCombatRoom() && !Room.Cleared)
            _encounterController?.StartEncounter(Room, _doors);
    }

    private void OnBodyExited(Node2D body)
    {
        if(body is Player && Room != null)
            _dungeonGenerator?.NotifyPlayerExitedRoom(Room);
    }

    public override void _ExitTree()
    {
        BodyEntered -= OnBodyEntered;
        BodyExited -= OnBodyExited;
    }
}
