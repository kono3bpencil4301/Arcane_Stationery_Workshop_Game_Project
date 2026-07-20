using System;
using System.Collections.Generic;

/// <summary>
/// 在房间连接图中从出生房执行深度优先搜索，并返回符合条件的最深房间。
/// </summary>
public static class DungeonDepthFirstSearch
{
    public static DungeonRoomData FindDeepestRoom(
        DungeonLayout layout,
        DungeonRoomData startRoom,
        Func<DungeonRoomData, bool> isCandidate
    )
    {
        if(layout == null || startRoom == null || isCandidate == null)
            return null;

        HashSet<int> visited = new();
        DungeonRoomData deepestRoom = null;
        int deepestDepth = -1;

        void Visit(DungeonRoomData room, int depth)
        {
            if(room == null || !visited.Add(room.Id))
                return;

            if(
                isCandidate(room) &&
                (
                    depth > deepestDepth ||
                    depth == deepestDepth &&
                    (deepestRoom == null || room.Id > deepestRoom.Id)
                )
            )
            {
                deepestRoom = room;
                deepestDepth = depth;
            }

            foreach(DungeonDoorData door in room.Doors)
            {
                DungeonCorridorData corridor = null;

                foreach(DungeonCorridorData candidate in layout.Corridors)
                {
                    if(candidate.Id != door.CorridorId)
                        continue;

                    corridor = candidate;
                    break;
                }

                // CrossLink 只负责增加环路，不改变主干/分支生成树的深度。
                if(corridor?.Kind == DungeonCorridorKind.CrossLink)
                    continue;

                Visit(layout.FindRoom(door.ConnectedRoomId), depth + 1);
            }
        }

        Visit(startRoom, 0);
        return deepestRoom;
    }
}
