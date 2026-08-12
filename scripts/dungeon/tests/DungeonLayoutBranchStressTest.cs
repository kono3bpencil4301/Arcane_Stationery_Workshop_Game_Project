using Godot;
using System;

/// <summary>
/// 纯数据压力测试：不实例化地图场景，连续验证多种固定种子下的分支拓扑、
/// 距离、单拐角限制和上下双格门。通过命令行单独运行对应 tscn。
/// </summary>
public partial class DungeonLayoutBranchStressTest : Node
{
    private const int SeedCountPerCrossLinkSetting = 100;

    public override void _Ready()
    {
        int totalAttempts = 0;
        int layoutsWithStraightCrossLink = 0;
        int layoutsWithBentCrossLink = 0;
        int verifiedDownLeftTurns = 0;
        int verifiedDownRightTurns = 0;
        int verifiedLowerLeftTurns = 0;
        int verifiedLowerRightTurns = 0;

        for(int requestedCrossLinks = 1; requestedCrossLinks <= 3;
            requestedCrossLinks++)
        {
            DungeonGenerationConfig config = new()
            {
                MinimumRoomCount = 10,
                MaximumRoomCount = 16,
                MinimumCorridorDistance = 4,
                MaximumCorridorDistance = 10,
                ExtraCrossConnectionCount = requestedCrossLinks,
                MaximumGenerationAttempts = 20
            };

            for(
                int seed = 1;
                seed <= SeedCountPerCrossLinkSetting;
                seed++
            )
            {
                RandomNumberGenerator random = new()
                {
                    Seed = (ulong)(requestedCrossLinks * 10000 + seed)
                };
                DungeonLayout validLayout = null;
                string lastError = string.Empty;

                for(
                    int attempt = 1;
                    attempt <= config.MaximumGenerationAttempts;
                    attempt++
                )
                {
                    totalAttempts++;

                    try
                    {
                        DungeonLayoutGenerator generator = new(
                            config,
                            random
                        );
                        DungeonLayout candidate = generator.Generate();

                        if(
                            DungeonLayoutValidator.Validate(
                                candidate,
                                config,
                                out lastError
                            )
                        )
                        {
                            validLayout = candidate;
                            break;
                        }
                    }
                    catch(Exception exception)
                    {
                        lastError = exception.Message;
                    }
                }

                if(validLayout == null)
                {
                    Fail(
                        $"补边={requestedCrossLinks}、种子={seed}在" +
                        $"{config.MaximumGenerationAttempts}次内未生成" +
                        $"有效布局: {lastError}"
                    );
                    return;
                }

                int crossLinkCount = 0;
                int verticalDoorCount = 0;

                foreach(
                    DungeonCorridorData corridor in validLayout.Corridors
                )
                {
                    if(!ValidateDownTurnMappings(
                        corridor,
                        ref verifiedDownLeftTurns,
                        ref verifiedDownRightTurns,
                        ref verifiedLowerLeftTurns,
                        ref verifiedLowerRightTurns
                    ))
                    {
                        return;
                    }

                    if(corridor.Kind == DungeonCorridorKind.CrossLink)
                    {
                        crossLinkCount++;

                        if(CountTurns(corridor) == 0)
                            layoutsWithStraightCrossLink++;
                        else
                            layoutsWithBentCrossLink++;
                    }

                    DungeonDoorData[] doors =
                    {
                        corridor.FromDoor,
                        corridor.ToDoor
                    };

                    foreach(DungeonDoorData door in doors)
                    {
                        if(!door.IsTwoCellsWide)
                            continue;

                        verticalDoorCount++;
                        int occupiedCellCount = 0;

                        foreach(Vector2I ignored in door.GetOccupiedCells())
                            occupiedCellCount++;

                        if(occupiedCellCount != 2)
                        {
                            Fail(
                                $"补边={requestedCrossLinks}、种子={seed}" +
                                $"的上下门{door.Id}不是两格宽"
                            );
                            return;
                        }
                    }
                }

                if(crossLinkCount != requestedCrossLinks)
                {
                    Fail(
                        $"补边={requestedCrossLinks}、种子={seed}实际" +
                        $"生成{crossLinkCount}条跨分支连接"
                    );
                    return;
                }

                if(verticalDoorCount == 0)
                {
                    Fail(
                        $"补边={requestedCrossLinks}、种子={seed}没有" +
                        $"生成可验证的上下双格门"
                    );
                    return;
                }
            }
        }

        if(
            verifiedDownLeftTurns == 0 ||
            verifiedDownRightTurns == 0 ||
            verifiedLowerLeftTurns == 0 ||
            verifiedLowerRightTurns == 0
        )
        {
            Fail(
                "压力样本没有覆盖全部四种拐角组合：" +
                $"upper_left={verifiedDownLeftTurns}, " +
                $"upper_right={verifiedDownRightTurns}, " +
                $"lower_left={verifiedLowerLeftTurns}, " +
                $"lower_right={verifiedLowerRightTurns}"
            );
            return;
        }

        GD.Print(
            $"[DungeonLayoutBranchStressTest] PASS layouts=" +
            $"{SeedCountPerCrossLinkSetting * 3}, " +
            $"attempts={totalAttempts}, " +
            $"straight_cross_links={layoutsWithStraightCrossLink}, " +
            $"bent_cross_links={layoutsWithBentCrossLink}, " +
            $"down_left_turns={verifiedDownLeftTurns}, " +
            $"down_right_turns={verifiedDownRightTurns}, " +
            $"lower_left_turns={verifiedLowerLeftTurns}, " +
            $"lower_right_turns={verifiedLowerRightTurns}"
        );
        GetTree().Quit(0);
    }

    private bool ValidateDownTurnMappings(
        DungeonCorridorData corridor,
        ref int downLeftTurns,
        ref int downRightTurns,
        ref int lowerLeftTurns,
        ref int lowerRightTurns
    )
    {
        for(int index = 0; index < corridor.PathCells.Count; index++)
        {
            Vector2I cell = corridor.PathCells[index];
            Vector2I previous = index == 0
                ? corridor.FromDoor.Cell
                : corridor.PathCells[index - 1];
            Vector2I next = index == corridor.PathCells.Count - 1
                ? corridor.ToDoor.Cell
                : corridor.PathCells[index + 1];
            Vector2I incoming = cell - previous;
            Vector2I outgoing = next - cell;
            bool isTurn = incoming.X != 0 && outgoing.Y != 0 ||
                incoming.Y != 0 && outgoing.X != 0;

            if(!isTurn)
                continue;

            bool hasUpperLeg = previous.Y < cell.Y || next.Y < cell.Y;
            bool hasLeftLeg = previous.X < cell.X || next.X < cell.X;
            DungeonTilePainter.TurnTilePlacement[] placements =
                DungeonTilePainter.GetTurnTilePlacements(
                    previous,
                    cell,
                    next
                );

            Vector2I expectedInnerOffset = hasUpperLeg
                ? hasLeftLeg
                    ? Vector2I.Left + Vector2I.Up
                    : Vector2I.Up
                : hasLeftLeg
                    ? Vector2I.Left + Vector2I.Down
                    : Vector2I.Down;
            Vector2I expectedInnerTile = hasLeftLeg
                ? new Vector2I(1, 8)
                : new Vector2I(13, 8);
            Vector2I expectedOuterOffset = hasUpperLeg
                ? hasLeftLeg
                    ? Vector2I.Down
                    : Vector2I.Left + Vector2I.Down
                : hasLeftLeg
                    ? Vector2I.Up
                    : Vector2I.Left + Vector2I.Up;
            Vector2I expectedOuterTile = hasLeftLeg
                ? new Vector2I(2, 9)
                : new Vector2I(12, 9);
            Vector2I expectedSideOffset = Vector2I.Left;
            Vector2I expectedSideTile = hasLeftLeg
                ? new Vector2I(7, 5)
                : new Vector2I(11, 11);
            Vector2I expectedCenterTile = hasLeftLeg
                ? new Vector2I(3, 11)
                : new Vector2I(7, 5);
            int expectedRadiusAlternative = hasUpperLeg
                ? 0
                : (int)TileSetAtlasSource.TransformFlipV;

            if(
                placements.Length != 4 ||
                !ContainsPlacement(
                    placements,
                    expectedInnerOffset,
                    expectedInnerTile,
                    expectedRadiusAlternative
                ) ||
                !ContainsPlacement(
                    placements,
                    expectedOuterOffset,
                    expectedOuterTile,
                    expectedRadiusAlternative
                ) ||
                !ContainsPlacement(
                    placements,
                    expectedSideOffset,
                    expectedSideTile
                ) ||
                !ContainsPlacement(
                    placements,
                    Vector2I.Zero,
                    expectedCenterTile
                )
            )
            {
                Fail(
                    $"走廊{corridor.Id}的" +
                    $"下拐{(hasLeftLeg ? "左" : "右")}瓦片映射错误。"
                );
                return false;
            }

            if(hasLeftLeg)
            {
                if(hasUpperLeg)
                    downLeftTurns++;
                else
                    lowerLeftTurns++;
            }
            else
            {
                if(hasUpperLeg)
                    downRightTurns++;
                else
                    lowerRightTurns++;
            }
        }

        return true;
    }

    private static bool ContainsPlacement(
        DungeonTilePainter.TurnTilePlacement[] placements,
        Vector2I offset,
        Vector2I atlasCoordinates,
        int alternativeTile = 0
    )
    {
        foreach(DungeonTilePainter.TurnTilePlacement placement in placements)
        {
            if(
                placement.Offset == offset &&
                placement.AtlasCoordinates == atlasCoordinates &&
                placement.AlternativeTile == alternativeTile
            )
            {
                return true;
            }
        }

        return false;
    }

    private static int CountTurns(DungeonCorridorData corridor)
    {
        Vector2I previousDirection = Vector2I.Zero;
        Vector2I previousCell = corridor.FromDoor.Cell;
        int turnCount = 0;

        foreach(Vector2I cell in corridor.PathCells)
        {
            Vector2I direction = cell - previousCell;

            if(
                previousDirection != Vector2I.Zero &&
                direction != previousDirection
            )
            {
                turnCount++;
            }

            previousDirection = direction;
            previousCell = cell;
        }

        Vector2I finalDirection = corridor.ToDoor.Cell - previousCell;

        if(
            previousDirection != Vector2I.Zero &&
            finalDirection != previousDirection
        )
        {
            turnCount++;
        }

        return turnCount;
    }

    private void Fail(string message)
    {
        GD.PushError($"[DungeonLayoutBranchStressTest] {message}");
        GetTree().Quit(1);
    }
}
