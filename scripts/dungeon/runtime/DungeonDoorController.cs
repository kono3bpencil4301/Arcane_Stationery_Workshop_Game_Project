using Godot;

public partial class DungeonDoorController : Node
{
    private static readonly Vector2I LockedIconTile = new(0, 0);

    public DungeonDoorData Door { get; private set; }
    public bool IsLocked { get; private set; }

    private TileMapLayer _directionLayer;
    private TileMapLayer _iconLayer;
    private int _overlaySourceId;

    public void Configure(
        DungeonDoorData door,
        TileMapLayer directionLayer,
        TileMapLayer iconLayer,
        int overlaySourceId
    )
    {
        Door = door;
        _directionLayer = directionLayer;
        _iconLayer = iconLayer;
        _overlaySourceId = overlaySourceId;
        Name = $"Door_{door.Id}_{door.Direction}";
        SetLocked(false);
    }

    public void SetLocked(bool locked)
    {
        if(
            Door == null ||
            !GodotObject.IsInstanceValid(_directionLayer) ||
            !GodotObject.IsInstanceValid(_iconLayer)
        )
        {
            return;
        }

        IsLocked = locked;

        foreach(Vector2I cell in Door.GetOccupiedCells())
        {
            if(locked)
            {
                _directionLayer.EraseCell(cell);
                _iconLayer.SetCell(
                    cell,
                    _overlaySourceId,
                    LockedIconTile,
                    0
                );
                continue;
            }

            _iconLayer.EraseCell(cell);
            _directionLayer.SetCell(
                cell,
                _overlaySourceId,
                GetDirectionTile(Door.Direction),
                0
            );
        }
    }

    private static Vector2I GetDirectionTile(
        DungeonDirection direction
    )
    {
        return direction switch
        {
            DungeonDirection.Up => new Vector2I(0, 1),
            DungeonDirection.Right => new Vector2I(0, 2),
            DungeonDirection.Down => new Vector2I(0, 3),
            DungeonDirection.Left => new Vector2I(0, 4),
            _ => LockedIconTile
        };
    }
}
