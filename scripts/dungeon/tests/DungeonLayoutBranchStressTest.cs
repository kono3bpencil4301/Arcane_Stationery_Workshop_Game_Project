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

        GD.Print(
            $"[DungeonLayoutBranchStressTest] PASS layouts=" +
            $"{SeedCountPerCrossLinkSetting * 3}, " +
            $"attempts={totalAttempts}, " +
            $"straight_cross_links={layoutsWithStraightCrossLink}, " +
            $"bent_cross_links={layoutsWithBentCrossLink}"
        );
        GetTree().Quit(0);
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
