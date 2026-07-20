using Godot;

public enum DungeonDirection
{
    Up,
    Right,
    Down,
    Left
}

public static class DungeonDirectionExtensions
{
    public static Vector2I ToCellOffset(this DungeonDirection direction)
    {
        return direction switch
        {
            DungeonDirection.Up => Vector2I.Up,
            DungeonDirection.Right => Vector2I.Right,
            DungeonDirection.Down => Vector2I.Down,
            DungeonDirection.Left => Vector2I.Left,
            _ => Vector2I.Zero
        };
    }

    public static DungeonDirection Opposite(this DungeonDirection direction)
    {
        return direction switch
        {
            DungeonDirection.Up => DungeonDirection.Down,
            DungeonDirection.Right => DungeonDirection.Left,
            DungeonDirection.Down => DungeonDirection.Up,
            DungeonDirection.Left => DungeonDirection.Right,
            _ => direction
        };
    }
}
