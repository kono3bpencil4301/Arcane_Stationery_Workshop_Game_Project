using Godot;
using System;
using System.Collections.Generic;

/// <summary>
/// 生成顺序严格分为三步：先放置全部房间，再规划走廊，最后才创建门数据。
/// 主干和生成树分支的门到门曼哈顿距离受配置约束；CrossLink 只负责补环，
/// 可以更长，但路径仍然只能是直线或一个直角弯。
/// </summary>
public sealed class DungeonLayoutGenerator
{
    private const int EnemySpawnCount = 2;
    private const int EnemyWallClearance = 2;
    private const int OuterMargin = 6;
    private const int MinimumBranchRoomCount = 3;
    private const int MaximumBranchRoomCount = 5;
    private const int RoomClearance = 1;
    private const int PlacementAttemptsPerDirection = 18;

    private readonly DungeonGenerationConfig _config;
    private readonly RandomNumberGenerator _random;
    private int _corridorDistanceMinimum;
    private int _corridorDistanceMaximum;

    private sealed class RoomPlan
    {
        public int Id;
        public int ParentRoomId = -1;
        public int GraphDepth;
        public bool IsMainPath;
        public DungeonRoomType InitialType;
        public Vector2I Size;
        public Rect2I Bounds;
        public DungeonDirection DirectionFromParent;
    }

    private sealed class ConnectionPlan
    {
        public int FromRoomId;
        public int ToRoomId;
        public DungeonCorridorKind Kind;
        public DungeonDirection FromDirection;
        public DungeonDirection ToDirection;
        public Vector2I FromDoorCell;
        public Vector2I ToDoorCell;
        public List<Vector2I> PathCells { get; } = new();
    }

    private readonly struct RoomPair
    {
        public RoomPair(int firstRoomId, int secondRoomId)
        {
            FirstRoomId = firstRoomId;
            SecondRoomId = secondRoomId;
        }

        public int FirstRoomId { get; }
        public int SecondRoomId { get; }
    }

    public DungeonLayoutGenerator(
        DungeonGenerationConfig config,
        RandomNumberGenerator random
    )
    {
        _config = config ?? new DungeonGenerationConfig();
        _random = random ?? new RandomNumberGenerator();
    }

    public DungeonLayout Generate()
    {
        ConfigureCorridorDistanceRange();
        int roomCount = ChooseRoomCount();
        int branchRoomCount = Math.Clamp(
            roomCount / 3,
            MinimumBranchRoomCount,
            MaximumBranchRoomCount
        );
        int mainPathRoomCount = roomCount - branchRoomCount;
        int shopRoomId = ChooseShopRoomId(
            roomCount,
            mainPathRoomCount - 1
        );

        // 第一步：建立生成树并放置全部房间。此时没有走廊，也没有门。
        List<RoomPlan> roomPlans = BuildRoomPlans(
            roomCount,
            mainPathRoomCount,
            shopRoomId
        );
        PlaceAllRooms(roomPlans, mainPathRoomCount);
        DungeonLayout layout = CreateLayoutWithRooms(roomPlans);

        // 第二步：根据已经固定的矩形房间规划直线/L 形走廊。
        List<ConnectionPlan> connectionPlans = PlanConnections(
            layout,
            roomPlans
        );

        // 第三步：走廊全部确认后，最后输出 Corridor 和成对的 Door。
        MaterializeCorridorsAndDoors(layout, connectionPlans);

        DungeonRoomData extractionRoom =
            DungeonDepthFirstSearch.FindDeepestRoom(
                layout,
                layout.StartRoom,
                room => room.Type == DungeonRoomType.Normal
            );

        if(extractionRoom == null)
        {
            throw new InvalidOperationException(
                "没有找到可设置为撤离房的主干末端房间。"
            );
        }

        AssignRoomTypes(layout, extractionRoom.Id);
        DungeonEncounterDifficultyStateMachine.AssignRoomStates(layout);
        PlaceEnemySpawns(layout);
        CalculateUsedBounds(layout);
        return layout;
    }

    private int ChooseRoomCount()
    {
        int configuredMinimum = Math.Clamp(
            _config.MinimumRoomCount,
            10,
            16
        );
        int configuredMaximum = Math.Clamp(
            _config.MaximumRoomCount,
            10,
            16
        );
        return _random.RandiRange(
            Math.Min(configuredMinimum, configuredMaximum),
            Math.Max(configuredMinimum, configuredMaximum)
        );
    }

    private void ConfigureCorridorDistanceRange()
    {
        int configuredMinimum = Math.Clamp(
            _config.MinimumCorridorDistance,
            DungeonGenerationConfig.CorridorDistanceLowerBound,
            DungeonGenerationConfig.CorridorDistanceUpperBound
        );
        int configuredMaximum = Math.Clamp(
            _config.MaximumCorridorDistance,
            DungeonGenerationConfig.CorridorDistanceLowerBound,
            DungeonGenerationConfig.CorridorDistanceUpperBound
        );
        _corridorDistanceMinimum = Math.Min(
            configuredMinimum,
            configuredMaximum
        );
        _corridorDistanceMaximum = Math.Max(
            configuredMinimum,
            configuredMaximum
        );
    }

    private int ChooseShopRoomId(int roomCount, int extractionRoomId)
    {
        List<int> candidates = new();

        for(int roomId = 1; roomId < roomCount; roomId++)
        {
            if(roomId != extractionRoomId)
                candidates.Add(roomId);
        }

        return candidates[_random.RandiRange(0, candidates.Count - 1)];
    }

    private List<RoomPlan> BuildRoomPlans(
        int roomCount,
        int mainPathRoomCount,
        int shopRoomId
    )
    {
        List<RoomPlan> plans = new(roomCount);

        for(int roomId = 0; roomId < mainPathRoomCount; roomId++)
        {
            DungeonRoomType initialType = roomId == 0
                ? DungeonRoomType.Start
                : roomId == shopRoomId
                    ? DungeonRoomType.Shop
                    : DungeonRoomType.Normal;
            plans.Add(new RoomPlan
            {
                Id = roomId,
                ParentRoomId = roomId - 1,
                GraphDepth = roomId,
                IsMainPath = true,
                InitialType = initialType,
                Size = ChooseRoomSize(initialType)
            });
        }

        int branchRoomCount = roomCount - mainPathRoomCount;
        int firstBranchLength = (branchRoomCount + 1) / 2;
        int secondBranchLength = branchRoomCount - firstBranchLength;
        int firstAttachIndex = Math.Max(1, mainPathRoomCount / 3);
        int secondAttachLimit = Math.Max(
            firstAttachIndex + 1,
            mainPathRoomCount - 2 - secondBranchLength
        );
        int secondAttachIndex = Math.Min(
            Math.Max(firstAttachIndex + 1, mainPathRoomCount * 2 / 3),
            secondAttachLimit
        );
        int nextRoomId = mainPathRoomCount;
        nextRoomId = AddBranchChain(
            plans,
            nextRoomId,
            firstBranchLength,
            firstAttachIndex,
            shopRoomId
        );
        AddBranchChain(
            plans,
            nextRoomId,
            secondBranchLength,
            secondAttachIndex,
            shopRoomId
        );
        return plans;
    }

    private int AddBranchChain(
        List<RoomPlan> plans,
        int firstRoomId,
        int length,
        int attachRoomId,
        int shopRoomId
    )
    {
        int parentRoomId = attachRoomId;

        for(int offset = 0; offset < length; offset++)
        {
            int roomId = firstRoomId + offset;
            DungeonRoomType initialType = roomId == shopRoomId
                ? DungeonRoomType.Shop
                : DungeonRoomType.Normal;
            plans.Add(new RoomPlan
            {
                Id = roomId,
                ParentRoomId = parentRoomId,
                GraphDepth = plans[parentRoomId].GraphDepth + 1,
                IsMainPath = false,
                InitialType = initialType,
                Size = ChooseRoomSize(initialType)
            });
            parentRoomId = roomId;
        }

        return firstRoomId + length;
    }

    private Vector2I ChooseRoomSize(DungeonRoomType roomType)
    {
        if(roomType == DungeonRoomType.Start)
            return new Vector2I(5, 5);

        if(roomType == DungeonRoomType.Shop)
            return new Vector2I(7, 5);

        int configuredMinimumLength = Math.Clamp(
            _config.MinimumRoomLength,
            16,
            26
        );
        int configuredMaximumLength = Math.Clamp(
            _config.MaximumRoomLength,
            16,
            26
        );
        int configuredMinimumWidth = Math.Clamp(
            _config.MinimumRoomWidth,
            10,
            18
        );
        int configuredMaximumWidth = Math.Clamp(
            _config.MaximumRoomWidth,
            10,
            18
        );
        return new Vector2I(
            _random.RandiRange(
                Math.Min(
                    configuredMinimumLength,
                    configuredMaximumLength
                ),
                Math.Max(
                    configuredMinimumLength,
                    configuredMaximumLength
                )
            ),
            _random.RandiRange(
                Math.Min(
                    configuredMinimumWidth,
                    configuredMaximumWidth
                ),
                Math.Max(
                    configuredMinimumWidth,
                    configuredMaximumWidth
                )
            )
        );
    }

    private void PlaceAllRooms(
        List<RoomPlan> plans,
        int mainPathRoomCount
    )
    {
        plans[0].Bounds = new Rect2I(
            new Vector2I(OuterMargin, OuterMargin),
            plans[0].Size
        );
        List<RoomPlan> placedRooms = new() { plans[0] };
        Dictionary<int, HashSet<DungeonDirection>> usedDirections = new();

        foreach(RoomPlan plan in plans)
            usedDirections[plan.Id] = new HashSet<DungeonDirection>();

        DungeonDirection firstMainDirection =
            (DungeonDirection)_random.RandiRange(0, 3);
        bool turnClockwise = _random.RandiRange(0, 1) == 1;
        DungeonDirection secondMainDirection = RotateDirection(
            firstMainDirection,
            turnClockwise ? 1 : -1
        );

        for(int index = 1; index < plans.Count; index++)
        {
            RoomPlan plan = plans[index];
            RoomPlan parent = plans[plan.ParentRoomId];
            DungeonDirection? preferredDirection = null;

            if(plan.IsMainPath)
            {
                preferredDirection = (index - 1) % 2 == 0
                    ? firstMainDirection
                    : secondMainDirection;
            }
            else if(!parent.IsMainPath)
            {
                // 分支链优先继续向外延伸，减少折回主干造成的拥挤。
                preferredDirection = parent.DirectionFromParent;
            }

            List<DungeonDirection> directions =
                BuildDirectionPreference(preferredDirection);

            if(
                !TryPlaceRoom(
                    plan,
                    parent,
                    placedRooms,
                    usedDirections,
                    directions
                )
            )
            {
                throw new InvalidOperationException(
                    $"无法为房间 {plan.Id} 找到无重叠的分支位置。"
                );
            }

            placedRooms.Add(plan);
        }
    }

    private bool TryPlaceRoom(
        RoomPlan plan,
        RoomPlan parent,
        IReadOnlyList<RoomPlan> placedRooms,
        Dictionary<int, HashSet<DungeonDirection>> usedDirections,
        IReadOnlyList<DungeonDirection> directions
    )
    {
        foreach(DungeonDirection direction in directions)
        {
            if(usedDirections[parent.Id].Contains(direction))
                continue;

            for(
                int attempt = 0;
                attempt < PlacementAttemptsPerDirection;
                attempt++
            )
            {
                int corridorDistance = _random.RandiRange(
                    _corridorDistanceMinimum,
                    _corridorDistanceMaximum
                );
                Rect2I candidateBounds = CreateAdjacentBounds(
                    parent.Bounds,
                    plan.Size,
                    direction,
                    corridorDistance - 1
                );

                if(OverlapsPlacedRoom(candidateBounds, placedRooms))
                    continue;

                if(
                    !HasClearStraightRoute(
                        parent,
                        candidateBounds,
                        direction,
                        placedRooms
                    )
                )
                {
                    continue;
                }

                plan.Bounds = candidateBounds;
                plan.DirectionFromParent = direction;
                usedDirections[parent.Id].Add(direction);
                usedDirections[plan.Id].Add(direction.Opposite());
                return true;
            }
        }

        return false;
    }

    private Rect2I CreateAdjacentBounds(
        Rect2I parent,
        Vector2I childSize,
        DungeonDirection direction,
        int gap
    )
    {
        if(
            direction == DungeonDirection.Left ||
            direction == DungeonDirection.Right
        )
        {
            int minimumY = parent.Position.Y - childSize.Y + 5;
            int maximumY = parent.End.Y - 5;
            int childY = _random.RandiRange(minimumY, maximumY);
            int childX = direction == DungeonDirection.Right
                ? parent.End.X + gap
                : parent.Position.X - gap - childSize.X;
            return new Rect2I(
                new Vector2I(childX, childY),
                childSize
            );
        }

        int minimumX = parent.Position.X - childSize.X + 5;
        int maximumX = parent.End.X - 5;
        int positionedX = _random.RandiRange(minimumX, maximumX);
        int positionedY = direction == DungeonDirection.Down
            ? parent.End.Y + gap
            : parent.Position.Y - gap - childSize.Y;
        return new Rect2I(
            new Vector2I(positionedX, positionedY),
            childSize
        );
    }

    private static bool OverlapsPlacedRoom(
        Rect2I candidate,
        IReadOnlyList<RoomPlan> placedRooms
    )
    {
        Rect2I expandedCandidate = new(
            candidate.Position - Vector2I.One * RoomClearance,
            candidate.Size + Vector2I.One * RoomClearance * 2
        );

        foreach(RoomPlan room in placedRooms)
        {
            if(expandedCandidate.Intersects(room.Bounds))
                return true;
        }

        return false;
    }

    private bool HasClearStraightRoute(
        RoomPlan parent,
        Rect2I childBounds,
        DungeonDirection direction,
        IReadOnlyList<RoomPlan> placedRooms
    )
    {
        List<int> sharedCoordinates = GetSharedDoorCoordinates(
            parent.Bounds,
            childBounds,
            direction
        );

        foreach(int coordinate in sharedCoordinates)
        {
            Vector2I fromDoor = CreateDoorCell(
                parent.Bounds,
                direction,
                coordinate
            );
            Vector2I toDoor = CreateDoorCell(
                childBounds,
                direction.Opposite(),
                coordinate
            );
            List<Vector2I> path = new();
            AppendAxisLine(
                path,
                fromDoor + direction.ToCellOffset(),
                toDoor + direction.Opposite().ToCellOffset()
            );
            bool blocked = false;

            foreach(Vector2I cell in path)
            {
                foreach(RoomPlan otherRoom in placedRooms)
                {
                    if(
                        otherRoom.Id != parent.Id &&
                        otherRoom.Bounds.HasPoint(cell)
                    )
                    {
                        blocked = true;
                        break;
                    }
                }

                if(blocked)
                    break;
            }

            if(!blocked)
                return true;
        }

        return false;
    }

    private DungeonLayout CreateLayoutWithRooms(
        IReadOnlyList<RoomPlan> plans
    )
    {
        DungeonLayout layout = new();

        foreach(RoomPlan plan in plans)
        {
            layout.Rooms.Add(new DungeonRoomData
            {
                Id = plan.Id,
                Bounds = plan.Bounds,
                Type = plan.InitialType,
                ParentRoomId = plan.ParentRoomId,
                GraphDepth = plan.GraphDepth,
                IsMainPath = plan.IsMainPath
            });
        }

        return layout;
    }

    private List<ConnectionPlan> PlanConnections(
        DungeonLayout layout,
        IReadOnlyList<RoomPlan> roomPlans
    )
    {
        List<ConnectionPlan> plans = new();
        Dictionary<int, HashSet<Vector2I>> reservedDoorWallCells = new();

        foreach(DungeonRoomData room in layout.Rooms)
        {
            reservedDoorWallCells[room.Id] = new HashSet<Vector2I>();
        }

        // 先确认生成树边：主干与分支都使用 4-10 的直线连接。
        for(int roomId = 1; roomId < roomPlans.Count; roomId++)
        {
            RoomPlan roomPlan = roomPlans[roomId];
            DungeonRoomData parent = layout.FindRoom(
                roomPlan.ParentRoomId
            );
            DungeonRoomData child = layout.FindRoom(roomPlan.Id);
            DungeonCorridorKind kind = roomPlan.IsMainPath
                ? DungeonCorridorKind.MainPath
                : DungeonCorridorKind.Branch;

            if(
                !TryCreateStraightConnection(
                    layout,
                    parent,
                    child,
                    roomPlan.DirectionFromParent,
                    kind,
                    plans,
                    reservedDoorWallCells,
                    out ConnectionPlan connection
                )
            )
            {
                throw new InvalidOperationException(
                    $"无法在房间 {parent.Id} 与 {child.Id} 之间规划直线走廊。"
                );
            }

            AcceptConnectionPlan(
                connection,
                plans,
                reservedDoorWallCells
            );
        }

        int requestedCrossLinks = Math.Clamp(
            _config.ExtraCrossConnectionCount,
            1,
            3
        );
        int createdCrossLinks = AddCrossConnections(
            layout,
            requestedCrossLinks,
            plans,
            reservedDoorWallCells
        );

        if(createdCrossLinks < requestedCrossLinks)
        {
            throw new InvalidOperationException(
                $"只找到 {createdCrossLinks}/{requestedCrossLinks} 条不穿房间的跨分支走廊。"
            );
        }

        return plans;
    }

    private int AddCrossConnections(
        DungeonLayout layout,
        int requestedCount,
        List<ConnectionPlan> plans,
        Dictionary<int, HashSet<Vector2I>> reservedDoorWallCells
    )
    {
        List<RoomPair> candidates = new();

        for(int left = 0; left < layout.Rooms.Count; left++)
        {
            for(int right = left + 1; right < layout.Rooms.Count; right++)
            {
                DungeonRoomData first = layout.Rooms[left];
                DungeonRoomData second = layout.Rooms[right];

                if(first.IsMainPath && second.IsMainPath)
                    continue;

                if(AreRoomsAlreadyConnected(first.Id, second.Id, plans))
                    continue;

                candidates.Add(new RoomPair(first.Id, second.Id));
            }
        }

        Shuffle(candidates);
        // 深度相近的房间优先补边，避免跨层捷径破坏主干推进感。
        candidates.Sort((left, right) =>
        {
            int leftDifference = Math.Abs(
                layout.FindRoom(left.FirstRoomId).GraphDepth -
                layout.FindRoom(left.SecondRoomId).GraphDepth
            );
            int rightDifference = Math.Abs(
                layout.FindRoom(right.FirstRoomId).GraphDepth -
                layout.FindRoom(right.SecondRoomId).GraphDepth
            );
            return leftDifference.CompareTo(rightDifference);
        });
        int createdCount = 0;

        foreach(RoomPair pair in candidates)
        {
            if(createdCount >= requestedCount)
                break;

            DungeonRoomData first = layout.FindRoom(pair.FirstRoomId);
            DungeonRoomData second = layout.FindRoom(pair.SecondRoomId);

            if(
                !TryCreateFlexibleConnection(
                    layout,
                    first,
                    second,
                    plans,
                    reservedDoorWallCells,
                    out ConnectionPlan connection
                )
            )
            {
                continue;
            }

            AcceptConnectionPlan(
                connection,
                plans,
                reservedDoorWallCells
            );
            createdCount++;
        }

        return createdCount;
    }

    private bool TryCreateFlexibleConnection(
        DungeonLayout layout,
        DungeonRoomData fromRoom,
        DungeonRoomData toRoom,
        IReadOnlyList<ConnectionPlan> existingPlans,
        Dictionary<int, HashSet<Vector2I>> reservedDoorWallCells,
        out ConnectionPlan connection
    )
    {
        connection = null;
        bool toRight = fromRoom.Bounds.End.X <= toRoom.Bounds.Position.X;
        bool toLeft = toRoom.Bounds.End.X <= fromRoom.Bounds.Position.X;
        bool toBelow = fromRoom.Bounds.End.Y <= toRoom.Bounds.Position.Y;
        bool toAbove = toRoom.Bounds.End.Y <= fromRoom.Bounds.Position.Y;

        if(toRight)
        {
            if(
                TryCreateStraightConnection(
                    layout,
                    fromRoom,
                    toRoom,
                    DungeonDirection.Right,
                    DungeonCorridorKind.CrossLink,
                    existingPlans,
                    reservedDoorWallCells,
                    out connection
                )
            )
            {
                return true;
            }

            if(
                toBelow &&
                (
                    TryCreateOneTurnConnection(
                        layout,
                        fromRoom,
                        toRoom,
                        DungeonDirection.Right,
                        DungeonDirection.Up,
                        true,
                        existingPlans,
                        reservedDoorWallCells,
                        out connection
                    ) ||
                    TryCreateOneTurnConnection(
                        layout,
                        fromRoom,
                        toRoom,
                        DungeonDirection.Down,
                        DungeonDirection.Left,
                        false,
                        existingPlans,
                        reservedDoorWallCells,
                        out connection
                    )
                )
            )
            {
                return true;
            }

            if(
                toAbove &&
                (
                    TryCreateOneTurnConnection(
                        layout,
                        fromRoom,
                        toRoom,
                        DungeonDirection.Right,
                        DungeonDirection.Down,
                        true,
                        existingPlans,
                        reservedDoorWallCells,
                        out connection
                    ) ||
                    TryCreateOneTurnConnection(
                        layout,
                        fromRoom,
                        toRoom,
                        DungeonDirection.Up,
                        DungeonDirection.Left,
                        false,
                        existingPlans,
                        reservedDoorWallCells,
                        out connection
                    )
                )
            )
            {
                return true;
            }
        }

        if(toLeft)
        {
            if(
                TryCreateStraightConnection(
                    layout,
                    fromRoom,
                    toRoom,
                    DungeonDirection.Left,
                    DungeonCorridorKind.CrossLink,
                    existingPlans,
                    reservedDoorWallCells,
                    out connection
                )
            )
            {
                return true;
            }

            if(
                toBelow &&
                (
                    TryCreateOneTurnConnection(
                        layout,
                        fromRoom,
                        toRoom,
                        DungeonDirection.Left,
                        DungeonDirection.Up,
                        true,
                        existingPlans,
                        reservedDoorWallCells,
                        out connection
                    ) ||
                    TryCreateOneTurnConnection(
                        layout,
                        fromRoom,
                        toRoom,
                        DungeonDirection.Down,
                        DungeonDirection.Right,
                        false,
                        existingPlans,
                        reservedDoorWallCells,
                        out connection
                    )
                )
            )
            {
                return true;
            }

            if(
                toAbove &&
                (
                    TryCreateOneTurnConnection(
                        layout,
                        fromRoom,
                        toRoom,
                        DungeonDirection.Left,
                        DungeonDirection.Down,
                        true,
                        existingPlans,
                        reservedDoorWallCells,
                        out connection
                    ) ||
                    TryCreateOneTurnConnection(
                        layout,
                        fromRoom,
                        toRoom,
                        DungeonDirection.Up,
                        DungeonDirection.Right,
                        false,
                        existingPlans,
                        reservedDoorWallCells,
                        out connection
                    )
                )
            )
            {
                return true;
            }
        }

        if(toBelow)
        {
            return TryCreateStraightConnection(
                layout,
                fromRoom,
                toRoom,
                DungeonDirection.Down,
                DungeonCorridorKind.CrossLink,
                existingPlans,
                reservedDoorWallCells,
                out connection
            );
        }

        if(toAbove)
        {
            return TryCreateStraightConnection(
                layout,
                fromRoom,
                toRoom,
                DungeonDirection.Up,
                DungeonCorridorKind.CrossLink,
                existingPlans,
                reservedDoorWallCells,
                out connection
            );
        }

        return false;
    }

    private bool TryCreateStraightConnection(
        DungeonLayout layout,
        DungeonRoomData fromRoom,
        DungeonRoomData toRoom,
        DungeonDirection fromDirection,
        DungeonCorridorKind kind,
        IReadOnlyList<ConnectionPlan> existingPlans,
        Dictionary<int, HashSet<Vector2I>> reservedDoorWallCells,
        out ConnectionPlan connection
    )
    {
        connection = null;
        DungeonDirection toDirection = fromDirection.Opposite();
        List<int> sharedCoordinates = GetSharedDoorCoordinates(
            fromRoom.Bounds,
            toRoom.Bounds,
            fromDirection
        );
        Shuffle(sharedCoordinates);

        foreach(int coordinate in sharedCoordinates)
        {
            ConnectionPlan candidate = new()
            {
                FromRoomId = fromRoom.Id,
                ToRoomId = toRoom.Id,
                Kind = kind,
                FromDirection = fromDirection,
                ToDirection = toDirection,
                FromDoorCell = CreateDoorCell(
                    fromRoom.Bounds,
                    fromDirection,
                    coordinate
                ),
                ToDoorCell = CreateDoorCell(
                    toRoom.Bounds,
                    toDirection,
                    coordinate
                )
            };
            AppendAxisLine(
                candidate.PathCells,
                candidate.FromDoorCell + fromDirection.ToCellOffset(),
                candidate.ToDoorCell + toDirection.ToCellOffset()
            );

            if(
                IsConnectionPlanClear(
                    candidate,
                    layout,
                    existingPlans,
                    reservedDoorWallCells
                )
            )
            {
                connection = candidate;
                return true;
            }
        }

        return false;
    }

    private bool TryCreateOneTurnConnection(
        DungeonLayout layout,
        DungeonRoomData fromRoom,
        DungeonRoomData toRoom,
        DungeonDirection fromDirection,
        DungeonDirection toDirection,
        bool horizontalFirst,
        IReadOnlyList<ConnectionPlan> existingPlans,
        Dictionary<int, HashSet<Vector2I>> reservedDoorWallCells,
        out ConnectionPlan connection
    )
    {
        connection = null;
        List<int> fromCoordinates = GetDoorCoordinates(
            fromRoom.Bounds,
            fromDirection
        );
        List<int> toCoordinates = GetDoorCoordinates(
            toRoom.Bounds,
            toDirection
        );
        Shuffle(fromCoordinates);
        Shuffle(toCoordinates);

        foreach(int fromCoordinate in fromCoordinates)
        {
            foreach(int toCoordinate in toCoordinates)
            {
                ConnectionPlan candidate = new()
                {
                    FromRoomId = fromRoom.Id,
                    ToRoomId = toRoom.Id,
                    Kind = DungeonCorridorKind.CrossLink,
                    FromDirection = fromDirection,
                    ToDirection = toDirection,
                    FromDoorCell = CreateDoorCell(
                        fromRoom.Bounds,
                        fromDirection,
                        fromCoordinate
                    ),
                    ToDoorCell = CreateDoorCell(
                        toRoom.Bounds,
                        toDirection,
                        toCoordinate
                    )
                };
                Vector2I start = candidate.FromDoorCell +
                    fromDirection.ToCellOffset();
                Vector2I end = candidate.ToDoorCell +
                    toDirection.ToCellOffset();
                Vector2I corner = horizontalFirst
                    ? new Vector2I(end.X, start.Y)
                    : new Vector2I(start.X, end.Y);
                AppendAxisLine(candidate.PathCells, start, corner);
                AppendAxisLine(candidate.PathCells, corner, end);

                if(
                    IsConnectionPlanClear(
                        candidate,
                        layout,
                        existingPlans,
                        reservedDoorWallCells
                    )
                )
                {
                    connection = candidate;
                    return true;
                }
            }
        }

        return false;
    }

    private static List<int> GetSharedDoorCoordinates(
        Rect2I fromBounds,
        Rect2I toBounds,
        DungeonDirection direction
    )
    {
        bool horizontal =
            direction == DungeonDirection.Left ||
            direction == DungeonDirection.Right;
        int fromMinimum = horizontal
            ? fromBounds.Position.Y + 2
            : fromBounds.Position.X + 2;
        int fromMaximum = horizontal
            ? fromBounds.End.Y - 3
            : fromBounds.End.X - 3;
        int toMinimum = horizontal
            ? toBounds.Position.Y + 2
            : toBounds.Position.X + 2;
        int toMaximum = horizontal
            ? toBounds.End.Y - 3
            : toBounds.End.X - 3;
        int minimum = Math.Max(fromMinimum, toMinimum);
        int maximum = Math.Min(fromMaximum, toMaximum);
        List<int> coordinates = new();

        for(int coordinate = minimum; coordinate <= maximum; coordinate++)
            coordinates.Add(coordinate);

        return coordinates;
    }

    private static List<int> GetDoorCoordinates(
        Rect2I bounds,
        DungeonDirection direction
    )
    {
        bool horizontalWall =
            direction == DungeonDirection.Up ||
            direction == DungeonDirection.Down;
        int minimum = horizontalWall
            ? bounds.Position.X + 2
            : bounds.Position.Y + 2;
        int maximum = horizontalWall
            ? bounds.End.X - 3
            : bounds.End.Y - 3;
        List<int> coordinates = new();

        for(int coordinate = minimum; coordinate <= maximum; coordinate++)
            coordinates.Add(coordinate);

        return coordinates;
    }

    private static Vector2I CreateDoorCell(
        Rect2I bounds,
        DungeonDirection direction,
        int coordinate
    )
    {
        return direction switch
        {
            DungeonDirection.Up =>
                new Vector2I(coordinate, bounds.Position.Y),
            DungeonDirection.Right =>
                new Vector2I(bounds.End.X - 1, coordinate),
            DungeonDirection.Down =>
                new Vector2I(coordinate, bounds.End.Y - 1),
            DungeonDirection.Left =>
                new Vector2I(bounds.Position.X, coordinate),
            _ => bounds.Position
        };
    }

    private static bool IsConnectionPlanClear(
        ConnectionPlan candidate,
        DungeonLayout layout,
        IReadOnlyList<ConnectionPlan> existingPlans,
        Dictionary<int, HashSet<Vector2I>> reservedDoorWallCells
    )
    {
        if(candidate.PathCells.Count == 0 || CountTurns(candidate) > 1)
            return false;

        if(
            !IsDoorWallAvailable(
                candidate.FromRoomId,
                candidate.FromDoorCell,
                candidate.FromDirection,
                reservedDoorWallCells
            ) ||
            !IsDoorWallAvailable(
                candidate.ToRoomId,
                candidate.ToDoorCell,
                candidate.ToDirection,
                reservedDoorWallCells
            )
        )
        {
            return false;
        }

        HashSet<Vector2I> footprint = GetCorridorFootprint(candidate);

        foreach(DungeonRoomData room in layout.Rooms)
        {
            if(
                room.Id == candidate.FromRoomId ||
                room.Id == candidate.ToRoomId
            )
            {
                continue;
            }

            foreach(Vector2I cell in footprint)
            {
                if(room.Bounds.HasPoint(cell))
                    return false;
            }
        }

        foreach(ConnectionPlan existing in existingPlans)
        {
            HashSet<Vector2I> existingFootprint =
                GetCorridorFootprint(existing);

            foreach(Vector2I cell in footprint)
            {
                if(existingFootprint.Contains(cell))
                    return false;
            }
        }

        return true;
    }

    private static bool IsDoorWallAvailable(
        int roomId,
        Vector2I doorCell,
        DungeonDirection direction,
        Dictionary<int, HashSet<Vector2I>> reservedDoorWallCells
    )
    {
        foreach(Vector2I wallCell in GetReservedDoorWallCells(
            doorCell,
            direction
        ))
        {
            if(reservedDoorWallCells[roomId].Contains(wallCell))
                return false;
        }

        return true;
    }

    private static IEnumerable<Vector2I> GetReservedDoorWallCells(
        Vector2I doorCell,
        DungeonDirection direction
    )
    {
        if(
            direction == DungeonDirection.Up ||
            direction == DungeonDirection.Down
        )
        {
            yield return doorCell + Vector2I.Left * 2;
            yield return doorCell + Vector2I.Left;
            yield return doorCell;
            yield return doorCell + Vector2I.Right;
            yield break;
        }

        yield return doorCell + Vector2I.Up;
        yield return doorCell;
        yield return doorCell + Vector2I.Down;
    }

    private static HashSet<Vector2I> GetCorridorFootprint(
        ConnectionPlan plan
    )
    {
        HashSet<Vector2I> footprint = new();

        for(int index = 0; index < plan.PathCells.Count; index++)
        {
            Vector2I cell = plan.PathCells[index];
            Vector2I previous = index == 0
                ? plan.FromDoorCell
                : plan.PathCells[index - 1];
            Vector2I next = index == plan.PathCells.Count - 1
                ? plan.ToDoorCell
                : plan.PathCells[index + 1];
            Vector2I incoming = cell - previous;
            Vector2I outgoing = next - cell;
            footprint.Add(cell);

            if(incoming.X != 0 || outgoing.X != 0)
            {
                footprint.Add(cell + Vector2I.Up);
                footprint.Add(cell + Vector2I.Down);
            }

            if(incoming.Y != 0 || outgoing.Y != 0)
                footprint.Add(cell + Vector2I.Left);
        }

        return footprint;
    }

    private static int CountTurns(ConnectionPlan plan)
    {
        int turnCount = 0;
        Vector2I previousDirection = Vector2I.Zero;

        for(int index = 1; index < plan.PathCells.Count; index++)
        {
            Vector2I direction = plan.PathCells[index] -
                plan.PathCells[index - 1];

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

    private static void AcceptConnectionPlan(
        ConnectionPlan connection,
        List<ConnectionPlan> plans,
        Dictionary<int, HashSet<Vector2I>> reservedDoorWallCells
    )
    {
        plans.Add(connection);

        foreach(Vector2I wallCell in GetReservedDoorWallCells(
            connection.FromDoorCell,
            connection.FromDirection
        ))
        {
            reservedDoorWallCells[connection.FromRoomId].Add(wallCell);
        }

        foreach(Vector2I wallCell in GetReservedDoorWallCells(
            connection.ToDoorCell,
            connection.ToDirection
        ))
        {
            reservedDoorWallCells[connection.ToRoomId].Add(wallCell);
        }
    }

    private static bool AreRoomsAlreadyConnected(
        int firstRoomId,
        int secondRoomId,
        IReadOnlyList<ConnectionPlan> plans
    )
    {
        foreach(ConnectionPlan plan in plans)
        {
            if(
                plan.FromRoomId == firstRoomId &&
                plan.ToRoomId == secondRoomId ||
                plan.FromRoomId == secondRoomId &&
                plan.ToRoomId == firstRoomId
            )
            {
                return true;
            }
        }

        return false;
    }

    private static void MaterializeCorridorsAndDoors(
        DungeonLayout layout,
        IReadOnlyList<ConnectionPlan> plans
    )
    {
        layout.Corridors.Clear();

        foreach(DungeonRoomData room in layout.Rooms)
            room.Doors.Clear();

        int nextDoorId = 0;

        for(int corridorId = 0; corridorId < plans.Count; corridorId++)
        {
            ConnectionPlan plan = plans[corridorId];
            DungeonDoorData fromDoor = new()
            {
                Id = nextDoorId++,
                RoomId = plan.FromRoomId,
                ConnectedRoomId = plan.ToRoomId,
                CorridorId = corridorId,
                Cell = plan.FromDoorCell,
                Direction = plan.FromDirection
            };
            DungeonDoorData toDoor = new()
            {
                Id = nextDoorId++,
                RoomId = plan.ToRoomId,
                ConnectedRoomId = plan.FromRoomId,
                CorridorId = corridorId,
                Cell = plan.ToDoorCell,
                Direction = plan.ToDirection
            };
            DungeonCorridorData corridor = new()
            {
                Id = corridorId,
                FromRoomId = plan.FromRoomId,
                ToRoomId = plan.ToRoomId,
                Kind = plan.Kind,
                FromDoor = fromDoor,
                ToDoor = toDoor
            };

            foreach(Vector2I cell in plan.PathCells)
                corridor.PathCells.Add(cell);

            layout.Corridors.Add(corridor);
            layout.FindRoom(plan.FromRoomId).Doors.Add(fromDoor);
            layout.FindRoom(plan.ToRoomId).Doors.Add(toDoor);
        }
    }

    private static void AppendAxisLine(
        List<Vector2I> path,
        Vector2I from,
        Vector2I to
    )
    {
        Vector2I difference = to - from;
        Vector2I step = new(
            Math.Sign(difference.X),
            Math.Sign(difference.Y)
        );

        if(step.X != 0 && step.Y != 0)
        {
            throw new InvalidOperationException(
                "走廊线段必须保持水平或垂直。"
            );
        }

        Vector2I cell = from;

        if(path.Count == 0 || path[^1] != cell)
            path.Add(cell);

        while(cell != to)
        {
            cell += step;

            if(path[^1] != cell)
                path.Add(cell);
        }
    }

    private List<DungeonDirection> BuildDirectionPreference(
        DungeonDirection? preferredDirection
    )
    {
        List<DungeonDirection> directions = new()
        {
            DungeonDirection.Up,
            DungeonDirection.Right,
            DungeonDirection.Down,
            DungeonDirection.Left
        };
        Shuffle(directions);

        if(preferredDirection.HasValue)
        {
            directions.Remove(preferredDirection.Value);
            directions.Insert(0, preferredDirection.Value);
        }

        return directions;
    }

    private static DungeonDirection RotateDirection(
        DungeonDirection direction,
        int quarterTurns
    )
    {
        int value = ((int)direction + quarterTurns) % 4;

        if(value < 0)
            value += 4;

        return (DungeonDirection)value;
    }

    private void Shuffle<T>(IList<T> values)
    {
        for(int index = values.Count - 1; index > 0; index--)
        {
            int swapIndex = _random.RandiRange(0, index);
            (values[index], values[swapIndex]) =
                (values[swapIndex], values[index]);
        }
    }

    private static void AssignRoomTypes(
        DungeonLayout layout,
        int extractionRoomId
    )
    {
        foreach(DungeonRoomData room in layout.Rooms)
        {
            if(room.Type != DungeonRoomType.Normal)
                continue;

            room.Type = room.Id == extractionRoomId
                ? DungeonRoomType.Extraction
                : DungeonRoomType.Monster;
        }
    }

    private void PlaceEnemySpawns(DungeonLayout layout)
    {
        foreach(DungeonRoomData room in layout.Rooms)
        {
            room.EnemySpawnCells.Clear();

            if(!room.Type.IsCombatRoom())
                continue;

            int minimumX = room.Bounds.Position.X +
                EnemyWallClearance + 1;
            int maximumX = room.Bounds.End.X - EnemyWallClearance - 2;
            int minimumY = room.Bounds.Position.Y +
                EnemyWallClearance + 1;
            int maximumY = room.Bounds.End.Y - EnemyWallClearance - 2;
            HashSet<Vector2I> occupied = new();

            while(room.EnemySpawnCells.Count < EnemySpawnCount)
            {
                Vector2I spawnCell = new(
                    _random.RandiRange(minimumX, maximumX),
                    _random.RandiRange(minimumY, maximumY)
                );

                if(occupied.Add(spawnCell))
                    room.EnemySpawnCells.Add(spawnCell);
            }
        }
    }

    private static void CalculateUsedBounds(DungeonLayout layout)
    {
        int minimumX = int.MaxValue;
        int minimumY = int.MaxValue;
        int maximumX = int.MinValue;
        int maximumY = int.MinValue;

        foreach(DungeonRoomData room in layout.Rooms)
        {
            minimumX = Math.Min(minimumX, room.Bounds.Position.X);
            minimumY = Math.Min(minimumY, room.Bounds.Position.Y);
            maximumX = Math.Max(maximumX, room.Bounds.End.X - 1);
            maximumY = Math.Max(maximumY, room.Bounds.End.Y - 1);
        }

        foreach(DungeonCorridorData corridor in layout.Corridors)
        {
            foreach(Vector2I cell in corridor.PathCells)
            {
                minimumX = Math.Min(minimumX, cell.X - 1);
                minimumY = Math.Min(minimumY, cell.Y - 1);
                maximumX = Math.Max(maximumX, cell.X + 1);
                maximumY = Math.Max(maximumY, cell.Y + 1);
            }
        }

        Vector2I position = new(minimumX - 2, minimumY - 2);
        Vector2I end = new(maximumX + 3, maximumY + 3);
        layout.UsedBounds = new Rect2I(position, end - position);
    }
}
