using Godot;
using System.Collections.Generic;

public sealed class DungeonLayout
{
    public List<DungeonRoomData> Rooms { get; } = new();
    public List<DungeonCorridorData> Corridors { get; } = new();
    public Rect2I UsedBounds { get; set; }

    public DungeonRoomData StartRoom
    {
        get
        {
            foreach(DungeonRoomData room in Rooms)
            {
                if(room.Type == DungeonRoomType.Start)
                    return room;
            }

            return null;
        }
    }

    public DungeonRoomData FindRoom(int roomId)
    {
        foreach(DungeonRoomData room in Rooms)
        {
            if(room.Id == roomId)
                return room;
        }

        return null;
    }
}
