public enum DungeonRoomType
{
    Start,
    Monster,
    Shop,
    Extraction,
    Normal
}

public static class DungeonRoomTypeExtensions
{
    public static bool IsCombatRoom(this DungeonRoomType roomType)
    {
        return roomType == DungeonRoomType.Monster ||
            roomType == DungeonRoomType.Extraction;
    }
}
