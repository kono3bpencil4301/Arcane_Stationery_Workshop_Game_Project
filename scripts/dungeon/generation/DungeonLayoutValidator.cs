using Godot;
using System;
using System.Collections.Generic;

public static class DungeonLayoutValidator
{
    public static bool Validate(
        DungeonLayout layout,
        DungeonGenerationConfig config,
        out string error
    )
    {
        List<string> errors = new();

        if(layout == null)
        {
            error = "地牢布局为空。";
            return false;
        }

        ValidateRoomCounts(layout, config, errors);
        ValidateRoomOverlaps(layout, errors);
        ValidateBranchTopology(layout, errors);
        ValidateDoors(layout, errors);
        ValidateCorridors(layout, config, errors);
        ValidateConnectivity(layout, errors);
        ValidateSpawnCells(layout, errors);

        error = string.Join(" | ", errors);
        return errors.Count == 0;
    }

    private static void ValidateRoomCounts(
        DungeonLayout layout,
        DungeonGenerationConfig config,
        List<string> errors
    )
    {
        if(layout.Rooms.Count < 10 || layout.Rooms.Count > 16)
            errors.Add($"房间数量越界: {layout.Rooms.Count}");

        int startCount = 0;
        int monsterCount = 0;
        int shopCount = 0;
        int extractionCount = 0;
        int normalCount = 0;
        DungeonRoomData extractionRoom = null;
        int configuredMinimumLength = Math.Clamp(
            config.MinimumRoomLength,
            16,
            26
        );
        int configuredMaximumLength = Math.Clamp(
            config.MaximumRoomLength,
            16,
            26
        );
        int minimumLength = Math.Min(
            configuredMinimumLength,
            configuredMaximumLength
        );
        int maximumLength = Math.Max(
            configuredMinimumLength,
            configuredMaximumLength
        );
        int configuredMinimumWidth = Math.Clamp(
            config.MinimumRoomWidth,
            10,
            18
        );
        int configuredMaximumWidth = Math.Clamp(
            config.MaximumRoomWidth,
            10,
            18
        );
        int minimumWidth = Math.Min(
            configuredMinimumWidth,
            configuredMaximumWidth
        );
        int maximumWidth = Math.Max(
            configuredMinimumWidth,
            configuredMaximumWidth
        );

        foreach(DungeonRoomData room in layout.Rooms)
        {
            switch(room.Type)
            {
                case DungeonRoomType.Start:
                    startCount++;
                    break;
                case DungeonRoomType.Monster:
                    monsterCount++;
                    break;
                case DungeonRoomType.Shop:
                    shopCount++;
                    break;
                case DungeonRoomType.Extraction:
                    extractionCount++;
                    extractionRoom = room;
                    break;
                case DungeonRoomType.Normal:
                    normalCount++;
                    break;
            }

            if(
                room.Type == DungeonRoomType.Start &&
                room.Bounds.Size != new Vector2I(5, 5)
            )
            {
                errors.Add(
                    $"出生房尺寸应为5x5，实际为{room.Bounds.Size}"
                );
            }

            if(
                room.Type == DungeonRoomType.Shop &&
                room.Bounds.Size != new Vector2I(7, 5)
            )
            {
                errors.Add(
                    $"商品房尺寸应为7x5，实际为{room.Bounds.Size}"
                );
            }

            if(
                room.Type.IsCombatRoom() &&
                (
                    room.Bounds.Size.X < minimumLength ||
                    room.Bounds.Size.X > maximumLength ||
                    room.Bounds.Size.Y < minimumWidth ||
                    room.Bounds.Size.Y > maximumWidth
                )
            )
            {
                errors.Add(
                    $"战斗房{room.Id}尺寸越界: {room.Bounds.Size}，" +
                    $"要求长{minimumLength}-{maximumLength}、" +
                    $"宽{minimumWidth}-{maximumWidth}"
                );
            }
        }

        if(startCount != 1)
            errors.Add($"出生房数量应为1，实际为{startCount}");
        int requiredMonsterRooms = Math.Max(layout.Rooms.Count - 3, 0);

        if(monsterCount != requiredMonsterRooms)
        {
            errors.Add(
                $"怪物房数量应为{requiredMonsterRooms}，实际为" +
                $"{monsterCount}"
            );
        }
        if(shopCount != 1)
            errors.Add($"商品房数量应为1，实际为{shopCount}");
        if(extractionCount != 1)
            errors.Add($"撤离房数量应为1，实际为{extractionCount}");
        if(normalCount != 0)
            errors.Add($"现阶段不允许普通房，实际生成{normalCount}间");

        DungeonRoomData deepestCombatRoom =
            DungeonDepthFirstSearch.FindDeepestRoom(
                layout,
                layout.StartRoom,
                room => room.Type.IsCombatRoom()
            );

        if(
            extractionRoom != null &&
            deepestCombatRoom?.Id != extractionRoom.Id
        )
        {
            errors.Add(
                $"撤离房{extractionRoom.Id}不是深度优先搜索得到的" +
                $"最深战斗房{deepestCombatRoom?.Id}"
            );
        }
    }

    private static void ValidateRoomOverlaps(
        DungeonLayout layout,
        List<string> errors
    )
    {
        for(int left = 0; left < layout.Rooms.Count; left++)
        {
            for(int right = left + 1; right < layout.Rooms.Count; right++)
            {
                if(layout.Rooms[left].Bounds.Intersects(
                    layout.Rooms[right].Bounds
                ))
                {
                    errors.Add(
                        $"房间{layout.Rooms[left].Id}与" +
                        $"房间{layout.Rooms[right].Id}重叠"
                    );
                }
            }
        }
    }

    private static void ValidateBranchTopology(
        DungeonLayout layout,
        List<string> errors
    )
    {
        int mainRoomCount = 0;
        int branchRoomCount = 0;
        int mainCorridorCount = 0;
        int branchCorridorCount = 0;
        int crossLinkCount = 0;
        Dictionary<int, int> treeDegrees = new();

        foreach(DungeonRoomData room in layout.Rooms)
        {
            treeDegrees[room.Id] = 0;

            if(room.IsMainPath)
                mainRoomCount++;
            else
                branchRoomCount++;
        }

        foreach(DungeonCorridorData corridor in layout.Corridors)
        {
            switch(corridor.Kind)
            {
                case DungeonCorridorKind.MainPath:
                    mainCorridorCount++;
                    break;
                case DungeonCorridorKind.Branch:
                    branchCorridorCount++;
                    break;
                case DungeonCorridorKind.CrossLink:
                    crossLinkCount++;
                    continue;
            }

            treeDegrees[corridor.FromRoomId]++;
            treeDegrees[corridor.ToRoomId]++;
        }

        if(branchRoomCount < 3)
            errors.Add($"分支房间不足: {branchRoomCount}，至少需要3个");

        if(mainCorridorCount != Math.Max(0, mainRoomCount - 1))
        {
            errors.Add(
                $"主干连接数错误: {mainCorridorCount}，" +
                $"预期{Math.Max(0, mainRoomCount - 1)}"
            );
        }

        if(branchCorridorCount != branchRoomCount)
        {
            errors.Add(
                $"分支生成树连接数错误: {branchCorridorCount}，" +
                $"预期{branchRoomCount}"
            );
        }

        if(crossLinkCount < 1)
            errors.Add("地牢没有跨分支补边，房间图仍然是一棵树");

        bool hasBranchPoint = false;

        foreach(DungeonRoomData room in layout.Rooms)
        {
            if(room.IsMainPath && treeDegrees[room.Id] >= 3)
                hasBranchPoint = true;

            if(room.Id == layout.StartRoom?.Id)
            {
                if(room.ParentRoomId != -1 || room.GraphDepth != 0)
                    errors.Add("出生房的父房间或图深度配置错误");

                continue;
            }

            DungeonRoomData parent = layout.FindRoom(room.ParentRoomId);

            if(parent == null)
            {
                errors.Add($"房间{room.Id}没有有效的生成树父房间");
                continue;
            }

            if(room.GraphDepth != parent.GraphDepth + 1)
                errors.Add($"房间{room.Id}的生成树深度不连续");

            bool hasParentConnection = false;

            foreach(DungeonCorridorData corridor in layout.Corridors)
            {
                if(corridor.Kind == DungeonCorridorKind.CrossLink)
                    continue;

                if(
                    corridor.FromRoomId == room.Id &&
                    corridor.ToRoomId == parent.Id ||
                    corridor.FromRoomId == parent.Id &&
                    corridor.ToRoomId == room.Id
                )
                {
                    hasParentConnection = true;
                    break;
                }
            }

            if(!hasParentConnection)
                errors.Add($"房间{room.Id}没有连接到生成树父房间");
        }

        if(!hasBranchPoint)
            errors.Add("主干上没有度数至少为3的真实分支点");
    }

    private static void ValidateDoors(
        DungeonLayout layout,
        List<string> errors
    )
    {
        foreach(DungeonRoomData room in layout.Rooms)
        {
            foreach(DungeonDoorData door in room.Doors)
            {
                int occupiedCellCount = 0;

                foreach(Vector2I ignored in door.GetOccupiedCells())
                    occupiedCellCount++;

                int expectedCellCount = door.IsTwoCellsWide ? 2 : 1;

                if(occupiedCellCount != expectedCellCount)
                {
                    errors.Add(
                        $"门{door.Id}宽度为{occupiedCellCount}，" +
                        $"预期{expectedCellCount}格"
                    );
                }

                if(!IsDoorValidForRoom(door, room.Bounds))
                    errors.Add($"房间{room.Id}的门{door.Id}位置无效");

                if(layout.FindRoom(door.ConnectedRoomId) == null)
                    errors.Add($"门{door.Id}连接了不存在的房间");
            }
        }
    }

    private static bool IsDoorValidForRoom(
        DungeonDoorData door,
        Rect2I bounds
    )
    {
        int right = bounds.Position.X + bounds.Size.X - 1;
        int bottom = bounds.Position.Y + bounds.Size.Y - 1;
        int minimumX = bounds.Position.X + 2;
        int maximumX = right - 2;
        int minimumY = bounds.Position.Y + 2;
        int maximumY = bottom - 2;

        bool anchorIsValid = door.Direction switch
        {
            DungeonDirection.Up =>
                door.Cell.Y == bounds.Position.Y &&
                door.Cell.X >= minimumX && door.Cell.X <= maximumX,
            DungeonDirection.Right =>
                door.Cell.X == right &&
                door.Cell.Y >= minimumY && door.Cell.Y <= maximumY,
            DungeonDirection.Down =>
                door.Cell.Y == bottom &&
                door.Cell.X >= minimumX && door.Cell.X <= maximumX,
            DungeonDirection.Left =>
                door.Cell.X == bounds.Position.X &&
                door.Cell.Y >= minimumY && door.Cell.Y <= maximumY,
            _ => false
        };

        if(!anchorIsValid)
            return false;

        foreach(Vector2I cell in door.GetOccupiedCells())
        {
            if(!bounds.HasPoint(cell))
                return false;

            bool cellIsOnDoorWall = door.Direction switch
            {
                DungeonDirection.Up => cell.Y == bounds.Position.Y,
                DungeonDirection.Right => cell.X == right,
                DungeonDirection.Down => cell.Y == bottom,
                DungeonDirection.Left => cell.X == bounds.Position.X,
                _ => false
            };

            if(!cellIsOnDoorWall)
                return false;
        }

        return true;
    }

    private static void ValidateCorridors(
        DungeonLayout layout,
        DungeonGenerationConfig config,
        List<string> errors
    )
    {
        int configuredMinimum = Math.Clamp(
            config.MinimumCorridorDistance,
            DungeonGenerationConfig.CorridorDistanceLowerBound,
            DungeonGenerationConfig.CorridorDistanceUpperBound
        );
        int configuredMaximum = Math.Clamp(
            config.MaximumCorridorDistance,
            DungeonGenerationConfig.CorridorDistanceLowerBound,
            DungeonGenerationConfig.CorridorDistanceUpperBound
        );
        int minimumDistance = Math.Min(
            configuredMinimum,
            configuredMaximum
        );
        int maximumDistance = Math.Max(
            configuredMinimum,
            configuredMaximum
        );

        foreach(DungeonCorridorData corridor in layout.Corridors)
        {
            if(corridor.PathCells.Count == 0)
            {
                errors.Add($"走廊{corridor.Id}没有连接格");
                continue;
            }

            int manhattanDistance =
                Math.Abs(
                    corridor.FromDoor.Cell.X - corridor.ToDoor.Cell.X
                ) +
                Math.Abs(
                    corridor.FromDoor.Cell.Y - corridor.ToDoor.Cell.Y
                );

            if(
                corridor.Kind != DungeonCorridorKind.CrossLink &&
                (
                    manhattanDistance < minimumDistance ||
                    manhattanDistance > maximumDistance
                )
            )
            {
                errors.Add(
                    $"走廊{corridor.Id}曼哈顿距离越界: " +
                    $"{manhattanDistance}，要求{minimumDistance}-" +
                    $"{maximumDistance}"
                );
            }

            if(
                corridor.PathCells[0] !=
                corridor.FromDoor.Cell +
                corridor.FromDoor.Direction.ToCellOffset()
            )
            {
                errors.Add($"走廊{corridor.Id}起点未贴合门");
            }

            if(
                corridor.PathCells[^1] !=
                corridor.ToDoor.Cell +
                corridor.ToDoor.Direction.ToCellOffset()
            )
            {
                errors.Add($"走廊{corridor.Id}终点未贴合门");
            }

            for(int index = 1; index < corridor.PathCells.Count; index++)
            {
                Vector2I difference = corridor.PathCells[index] -
                    corridor.PathCells[index - 1];

                if(Math.Abs(difference.X) + Math.Abs(difference.Y) != 1)
                    errors.Add($"走廊{corridor.Id}存在不连续路径");
            }

            int turnCount = CountCorridorTurns(corridor);

            if(turnCount > 1)
            {
                errors.Add(
                    $"走廊{corridor.Id}有{turnCount}个拐角，最多允许1个"
                );
            }

            foreach(Vector2I cell in corridor.PathCells)
            {
                foreach(DungeonRoomData room in layout.Rooms)
                {
                    if(
                        room.Id != corridor.FromRoomId &&
                        room.Id != corridor.ToRoomId &&
                        room.ContainsCell(cell)
                    )
                    {
                        errors.Add(
                            $"走廊{corridor.Id}穿过无关房间{room.Id}"
                        );
                    }
                }
            }
        }
    }

    private static int CountCorridorTurns(DungeonCorridorData corridor)
    {
        List<Vector2I> route = new(corridor.PathCells.Count + 2)
        {
            corridor.FromDoor.Cell
        };
        route.AddRange(corridor.PathCells);
        route.Add(corridor.ToDoor.Cell);
        Vector2I previousDirection = Vector2I.Zero;
        int turnCount = 0;

        for(int index = 1; index < route.Count; index++)
        {
            Vector2I direction = route[index] - route[index - 1];

            if(
                previousDirection != Vector2I.Zero &&
                direction != previousDirection
            )
            {
                turnCount++;
            }

            previousDirection = direction;
        }

        return turnCount;
    }

    private static void ValidateConnectivity(
        DungeonLayout layout,
        List<string> errors
    )
    {
        DungeonRoomData start = layout.StartRoom;

        if(start == null)
            return;

        HashSet<int> visited = new() { start.Id };
        Queue<int> pending = new();
        pending.Enqueue(start.Id);

        while(pending.Count > 0)
        {
            DungeonRoomData room = layout.FindRoom(pending.Dequeue());

            foreach(DungeonDoorData door in room.Doors)
            {
                if(visited.Add(door.ConnectedRoomId))
                    pending.Enqueue(door.ConnectedRoomId);
            }
        }

        if(visited.Count != layout.Rooms.Count)
        {
            errors.Add(
                $"地牢不连通，只能抵达{visited.Count}/" +
                $"{layout.Rooms.Count}个房间"
            );
        }
    }

    private static void ValidateSpawnCells(
        DungeonLayout layout,
        List<string> errors
    )
    {
        foreach(DungeonRoomData room in layout.Rooms)
        {
            if(
                room.Type.IsCombatRoom() &&
                room.EnemySpawnCells.Count != 2
            )
            {
                errors.Add($"战斗房{room.Id}的生成点不是2个");
            }

            if(
                !room.Type.IsCombatRoom() &&
                room.EnemySpawnCells.Count != 0
            )
            {
                errors.Add($"安全房{room.Id}不应有敌人生成点");
            }

            foreach(Vector2I cell in room.EnemySpawnCells)
            {
                int localX = cell.X - room.Bounds.Position.X;
                int localY = cell.Y - room.Bounds.Position.Y;

                if(
                    localX < 3 ||
                    localY < 3 ||
                    localX > room.Bounds.Size.X - 4 ||
                    localY > room.Bounds.Size.Y - 4
                )
                {
                    errors.Add($"战斗房{room.Id}的生成点离墙过近");
                }
            }
        }
    }
}
