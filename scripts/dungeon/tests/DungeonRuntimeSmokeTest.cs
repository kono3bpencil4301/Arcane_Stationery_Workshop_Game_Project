using Godot;
using System;
using System.Collections.Generic;

/// <summary>
/// 只供无窗口自动检查使用，不会被正式地图实例化。
/// 验证房型策略、DFS 撤离房、绘制层级，以及撤离房作为特殊怪物房时
/// 封门、计时停刷和清敌开门的完整链路。
/// </summary>
public partial class DungeonRuntimeSmokeTest : Node
{
    [Export]
    public PackedScene MapScene { get; set; }

    public override async void _Ready()
    {
        if(MapScene == null)
        {
            Fail("没有绑定地图场景。");
            return;
        }

        Node map = MapScene.Instantiate();
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        GetTree().Root.AddChild(map);
        GetTree().CurrentScene = map;

        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);

        DungeonGenerator generator = map.GetNodeOrNull<DungeonGenerator>(
            "DungeonGenerator"
        );
        EnemySpawner spawner = map.GetNodeOrNull<EnemySpawner>(
            "EnemySpawnerTileLayer"
        );
        TileMapLayer doorIconLayer = map.GetNodeOrNull<TileMapLayer>(
            "DoorIconTileMapLayer"
        );
        TileMapLayer doorDirectionLayer = map.GetNodeOrNull<TileMapLayer>(
            "DoorBlockerTileMapLayer"
        );
        TileMapLayer playerSpawnLayer = map.GetNodeOrNull<TileMapLayer>(
            "PlayerSpawnTileLayer"
        );
        Player player = map.GetNodeOrNull<Player>("Player");
        InventoryModel inventory = player?.GetNodeOrNull<InventoryModel>(
            "Inventory"
        );
        EquipmentModel equipment = player?.GetNodeOrNull<EquipmentModel>(
            "Equipment"
        );
        ConsumableUseController consumableUse =
            player?.GetNodeOrNull<ConsumableUseController>(
                "ConsumableUseController"
            );
        RoomNameView roomNameView = map.FindChild(
            "RoomNameView",
            true,
            false
        ) as RoomNameView;
        TextureProgressBar roomExplorationProgress = map.FindChild(
            "RoomExplorationProgress",
            true,
            false
        ) as TextureProgressBar;
        TextureRect roomExplorationOutline = map.FindChild(
            "RoomExplorationOutline",
            true,
            false
        ) as TextureRect;
        DungeonEncounterController encounterController = map.FindChild(
            "DungeonEncounterController",
            true,
            false
        ) as DungeonEncounterController;

        if(
            generator?.GeneratedLayout == null ||
            spawner?.Timeline == null ||
            doorIconLayer == null ||
            doorDirectionLayer == null ||
            playerSpawnLayer == null ||
            player == null ||
            inventory == null ||
            equipment == null ||
            consumableUse == null ||
            roomNameView == null ||
            roomExplorationProgress == null ||
            roomExplorationOutline?.Material is not ShaderMaterial ||
            encounterController == null
        )
        {
            Fail("地图运行依赖没有完成初始化。");
            return;
        }

        if(
            playerSpawnLayer.ZIndex > player.ZIndex ||
            playerSpawnLayer.ZIndex == player.ZIndex &&
            playerSpawnLayer.GetIndex() > player.GetIndex()
        )
        {
            Fail("玩家出生点瓦片仍会绘制在玩家上方。");
            return;
        }

        if(
            spawner.ZIndex > player.ZIndex ||
            spawner.ZIndex == player.ZIndex &&
            spawner.GetIndex() > player.GetIndex()
        )
        {
            Fail("敌人生成点瓦片仍会绘制在玩家上方。");
            return;
        }

        if(
            doorDirectionLayer.ZIndex > player.ZIndex ||
            doorDirectionLayer.ZIndex == player.ZIndex &&
            doorDirectionLayer.GetIndex() > player.GetIndex() ||
            doorIconLayer.ZIndex > player.ZIndex ||
            doorIconLayer.ZIndex == player.ZIndex &&
            doorIconLayer.GetIndex() > player.GetIndex()
        )
        {
            Fail("门口方向与封锁瓦片仍会绘制在玩家上方。");
            return;
        }

        DungeonRoomData combatRoom = null;
        DungeonRoomData shopRoom = null;
        List<DungeonRoomData> monsterRooms = new();

        foreach(DungeonRoomData room in generator.GeneratedLayout.Rooms)
        {
            if(room.Type == DungeonRoomType.Normal)
            {
                Fail("地牢仍然生成了普通房。");
                return;
            }

            if(room.Type == DungeonRoomType.Extraction)
                combatRoom = room;
            else if(room.Type == DungeonRoomType.Shop)
                shopRoom = room;
            else if(room.Type == DungeonRoomType.Monster)
                monsterRooms.Add(room);
        }

        if(combatRoom == null || shopRoom == null)
        {
            Fail("没有生成撤离房或商店房。");
            return;
        }

        float initialExplorationPercent =
            100.0f / generator.TotalRoomCount;
        if(
            generator.UnlockedRoomCount != 1 ||
            Mathf.Abs(
                (float)roomExplorationProgress.Value -
                initialExplorationPercent
            ) > 0.1f
        )
        {
            Fail("出生房没有作为总地图探索进度的初始解锁房间。");
            return;
        }

        monsterRooms.Sort((left, right) => left.Id.CompareTo(right.Id));
        int edgeRoomCount = (int)Math.Round(
            monsterRooms.Count * 0.3,
            MidpointRounding.AwayFromZero
        );

        for(int index = 0; index < monsterRooms.Count; index++)
        {
            DungeonEncounterDifficulty expectedDifficulty =
                index < edgeRoomCount
                    ? DungeonEncounterDifficulty.Early
                    : index < monsterRooms.Count - edgeRoomCount
                        ? DungeonEncounterDifficulty.Middle
                        : DungeonEncounterDifficulty.Late;

            if(monsterRooms[index].EncounterDifficulty != expectedDifficulty)
            {
                Fail("怪物房没有按 30% / 40% / 30% 分配难度状态。");
                return;
            }
        }

        if(combatRoom.EncounterDifficulty != DungeonEncounterDifficulty.Late)
        {
            Fail("撤离房没有使用后期难度状态。");
            return;
        }

        DungeonEncounterDifficultyStateMachine difficultyMachine = new();
        if(
            !Mathf.IsEqualApprox(
                difficultyMachine.TransitionTo(
                    DungeonEncounterDifficulty.Early
                ).DurationSeconds,
                60.0f
            ) ||
            !Mathf.IsEqualApprox(
                difficultyMachine.TransitionTo(
                    DungeonEncounterDifficulty.Middle
                ).DurationSeconds,
                75.0f
            ) ||
            !Mathf.IsEqualApprox(
                difficultyMachine.TransitionTo(
                    DungeonEncounterDifficulty.Late
                ).DurationSeconds,
                90.0f
            )
        )
        {
            Fail("三档难度状态没有对应 60 / 75 / 90 秒。");
            return;
        }

        DungeonRoomData deepestCombatRoom =
            DungeonDepthFirstSearch.FindDeepestRoom(
                generator.GeneratedLayout,
                generator.GeneratedLayout.StartRoom,
                room => room.Type.IsCombatRoom()
            );

        if(deepestCombatRoom?.Id != combatRoom.Id)
        {
            Fail("撤离房不是深度优先搜索确认的最深房间。");
            return;
        }

        spawner.Timeline.LoopTimeline = false;
        TileMapLayer groundLayer = map.GetNode<TileMapLayer>(
            "GroundTileMapLayer"
        );

        foreach(DungeonRoomData room in generator.GeneratedLayout.Rooms)
        {
            foreach(DungeonDoorData door in room.Doors)
            {
                int occupiedCellCount = 0;

                foreach(Vector2I doorwayCell in door.GetOccupiedCells())
                {
                    occupiedCellCount++;

                    if(
                        groundLayer.GetCellAtlasCoords(doorwayCell) !=
                        DungeonTilePainter.FloorTile
                    )
                    {
                        Fail($"门 {door.Id} 的开放格没有铺设为地面。");
                        return;
                    }
                }

                int expectedWidth = door.IsTwoCellsWide ? 2 : 1;
                if(occupiedCellCount != expectedWidth)
                {
                    Fail($"门 {door.Id} 的宽度不是 {expectedWidth} 格。");
                    return;
                }

                if(!door.IsTwoCellsWide)
                    continue;

                DungeonCorridorData corridor =
                    generator.GeneratedLayout.Corridors[door.CorridorId];
                Vector2I corridorAnchor = corridor.FromDoor.Id == door.Id
                    ? corridor.PathCells[0]
                    : corridor.PathCells[^1];

                if(
                    groundLayer.GetCellSourceId(corridorAnchor) < 0 ||
                    groundLayer.GetCellSourceId(
                        corridorAnchor + Vector2I.Left
                    ) < 0
                )
                {
                    Fail($"上下门 {door.Id} 没有与两格宽走廊紧密贴合。");
                    return;
                }
            }
        }

        player.GlobalPosition = groundLayer.ToGlobal(
            groundLayer.MapToLocal(combatRoom.CenterCell)
        );

        for(int frame = 0; frame < 5; frame++)
        {
            await ToSignal(
                GetTree(),
                SceneTree.SignalName.PhysicsFrame
            );
        }

        if(!spawner.EncounterActive)
        {
            Fail("进入撤离房后怪潮没有启动。");
            return;
        }

        if(
            !Mathf.IsEqualApprox((float)spawner.CycleDurationSeconds, 90.0f) ||
            encounterController.CurrentDifficultyState?.Difficulty !=
                DungeonEncounterDifficulty.Late
        )
        {
            Fail("后期难度状态没有把战斗房计时设置为 90 秒。");
            return;
        }

        if(!roomNameView.Visible || roomNameView.Text != "撤离房")
        {
            Fail("进入撤离房后没有显示正确的房间名称。");
            return;
        }

        foreach(DungeonDoorData door in combatRoom.Doors)
        {
            foreach(Vector2I doorwayCell in door.GetOccupiedCells())
            {
                if(
                    doorIconLayer.GetCellSourceId(doorwayCell) >= 0 &&
                    doorDirectionLayer.GetCellSourceId(doorwayCell) < 0
                )
                {
                    continue;
                }

                Fail($"门 {door.Id} 的封锁与通行瓦片发生重叠。");
                return;
            }
        }

        spawner._Process(spawner.CycleDurationSeconds * 0.5);
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);

        if(
            roomExplorationProgress.FillMode !=
                (int)TextureProgressBar.FillModeEnum.BottomToTop ||
            roomExplorationProgress.TextureUnder == null ||
            roomExplorationProgress.TextureProgress == null ||
            !Mathf.IsEqualApprox(
                roomExplorationProgress.TintUnder.A,
                1.0f
            ) ||
            !Mathf.IsEqualApprox(
                roomExplorationProgress.TintProgress.A,
                1.0f
            ) ||
            roomExplorationProgress.TintProgress.B <=
                roomExplorationProgress.TintUnder.B ||
            Mathf.Abs(
                (float)roomExplorationProgress.Value -
                initialExplorationPercent
            ) > 0.1f ||
            generator.UnlockedRoomCount != 1
        )
        {
            Fail(
                "怪物房清理前错误增加了总地图探索进度：" +
                $"fill={roomExplorationProgress.FillMode}, " +
                $"value={roomExplorationProgress.Value:0.###}, " +
                $"unlocked={generator.UnlockedRoomCount}。"
            );
            return;
        }

        spawner._Process(spawner.CycleDurationSeconds);
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);

        if(spawner.IsRunning)
        {
            Fail("计时结束后敌人生成器仍在运行。");
            return;
        }

        foreach(Node enemy in GetTree().GetNodesInGroup("enemy"))
            enemy.QueueFree();

        for(int frame = 0; frame < 5; frame++)
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);

        if(spawner.EncounterActive || !combatRoom.Cleared)
        {
            Fail("清除现存敌人后撤离房没有完成。");
            return;
        }

        float clearedExplorationPercent =
            200.0f / generator.TotalRoomCount;
        if(
            generator.UnlockedRoomCount != 2 ||
            Mathf.Abs(
                (float)roomExplorationProgress.Value -
                clearedExplorationPercent
            ) > 0.1f
        )
        {
            Fail("怪物房清理后没有解锁该房间并增加总地图探索进度。");
            return;
        }

        SchoolBagSearchPoint schoolBag = map.FindChild(
            $"SchoolBag_Room_{combatRoom.Id}",
            true,
            false
        ) as SchoolBagSearchPoint;

        if(schoolBag == null)
        {
            Fail("战斗房清理后没有在房间中央生成书包。");
            return;
        }

        Vector2 expectedBagPosition = groundLayer.ToGlobal(
            groundLayer.MapToLocal(combatRoom.CenterCell)
        );

        if(!schoolBag.GlobalPosition.IsEqualApprox(expectedBagPosition))
        {
            Fail("书包没有生成在房间中央。");
            return;
        }

        if(schoolBag.MaterialItems.Count != 10)
        {
            Fail("书包材料池没有包含assets/items/materials中的全部资产。");
            return;
        }

        foreach(InventoryItemData material in schoolBag.MaterialItems)
        {
            if(
                material == null ||
                material.Category != InventoryItemCategory.Material ||
                !Mathf.IsEqualApprox(material.Weight, 0.1f)
            )
            {
                Fail("书包材料池存在未配置为0.1kg的材料。");
                return;
            }
        }

        if(!schoolBag.Search(player))
        {
            Fail("搜索书包没有获得物资。");
            return;
        }

        InventoryEntry bandageEntry = null;
        int materialEntryCount = 0;

        foreach(InventoryEntry entry in inventory.Entries)
        {
            if(entry.Data.ItemId == "bandage")
            {
                bandageEntry = entry;

                if(!Mathf.IsEqualApprox(entry.Data.Weight, 0.2f))
                {
                    Fail("创口贴重量不是0.2kg。");
                    return;
                }

                continue;
            }

            if(entry.Data.Category != InventoryItemCategory.Material)
                continue;

            materialEntryCount++;

            if(!Mathf.IsEqualApprox(entry.Data.Weight, 0.1f))
            {
                Fail($"材料{entry.Data.DisplayName}重量不是0.1kg。");
                return;
            }
        }

        if(bandageEntry == null || materialEntryCount == 0)
        {
            Fail("书包没有同时提供创口贴和材料。");
            return;
        }

        InventoryItemView quantityView = new()
        {
            Name = "QuantityViewTest"
        };
        map.AddChild(quantityView);
        quantityView.SetEntry(
            new InventoryEntry(schoolBag.BandageItem, 6)
        );
        Label quantityLabel = quantityView.GetNodeOrNull<Label>(
            "QuantityLabel"
        );

        if(
            quantityLabel == null ||
            !quantityLabel.Visible ||
            quantityLabel.Text != "6" ||
            quantityLabel.HorizontalAlignment != HorizontalAlignment.Right ||
            quantityLabel.VerticalAlignment != VerticalAlignment.Bottom
        )
        {
            Fail("背包物资格右下角没有显示堆叠数量。");
            return;
        }

        quantityView.QueueFree();

        if(!equipment.TryEquip(bandageEntry, 0, inventory))
        {
            Fail("创口贴无法装备到下方快捷槽。");
            return;
        }

        player.GlobalPosition = groundLayer.ToGlobal(
            groundLayer.MapToLocal(shopRoom.CenterCell)
        );

        for(int frame = 0; frame < 5; frame++)
            await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);

        float shopExplorationPercent =
            300.0f / generator.TotalRoomCount;
        if(
            generator.UnlockedRoomCount != 3 ||
            Mathf.Abs(
                (float)roomExplorationProgress.Value -
                shopExplorationPercent
            ) > 0.1f
        )
        {
            Fail("首次抵达非战斗房后没有增加总地图探索进度。");
            return;
        }

        player.GlobalPosition = groundLayer.ToGlobal(
            groundLayer.MapToLocal(
                generator.GeneratedLayout.StartRoom.CenterCell
            )
        );

        foreach(Node projectile in GetTree().GetNodesInGroup(
            "enemy_projectile"
        ))
        {
            projectile.QueueFree();
        }

        player.GetNodeOrNull<Node>(
            "TheMoldSpotFungusPoison"
        )?.QueueFree();

        await ToSignal(
            GetTree(),
            SceneTree.SignalName.ProcessFrame
        );

        player.TakeContinuousDamage(50.0f, this);
        float damagedHealth = player.CurrentHealth;

        EquipmentSlotView quickSlot = null;

        foreach(Node node in map.FindChildren("*", "", true, false))
        {
            if(node is EquipmentSlotView slot)
            {
                quickSlot = slot;
                break;
            }
        }

        if(quickSlot == null)
        {
            Fail("没有找到下方快捷槽。");
            return;
        }

        quickSlot._GuiInput(new InputEventMouseButton
        {
            ButtonIndex = MouseButton.Left,
            Pressed = true
        });

        if(!consumableUse.IsUsing || !player.MovementLocked)
        {
            Fail("左键使用创口贴后没有锁定玩家移动。");
            return;
        }

        await ToSignal(
            GetTree().CreateTimer(0.75),
            SceneTreeTimer.SignalName.Timeout
        );

        TextureProgressBar useProgress = player.FindChild(
            "UseProgressRing",
            true,
            false
        ) as TextureProgressBar;

        if(
            useProgress == null ||
            !useProgress.Visible ||
            useProgress.Value <= 0.0 ||
            useProgress.Value >= 100.0 ||
            Mathf.Abs(useProgress.Rotation) > 0.01f ||
            useProgress.FillMode !=
                (int)TextureProgressBar.FillModeEnum.Clockwise ||
            !Mathf.IsEqualApprox(useProgress.RadialFillDegrees, 360.0f)
        )
        {
            Fail("创口贴进度环没有以固定控件顺时针填充。");
            return;
        }

        int quantityBeforeCancel = equipment.GetSlot(0).Quantity;
        GetViewport().PushInput(new InputEventMouseButton
        {
            ButtonIndex = MouseButton.Right,
            Pressed = true
        });
        await ToSignal(
            GetTree(),
            SceneTree.SignalName.ProcessFrame
        );

        if(
            consumableUse.IsUsing ||
            player.MovementLocked ||
            !Mathf.IsEqualApprox(player.CurrentHealth, damagedHealth) ||
            equipment.GetSlot(0)?.Quantity != quantityBeforeCancel ||
            useProgress.Visible ||
            useProgress.Value > 0.0
        )
        {
            Fail("右键中断创口贴后仍然回血、耗材或锁定移动。");
            return;
        }

        quickSlot._GuiInput(new InputEventMouseButton
        {
            ButtonIndex = MouseButton.Left,
            Pressed = true
        });

        if(!consumableUse.IsUsing || !player.MovementLocked)
        {
            Fail("中断后无法再次开始使用创口贴。");
            return;
        }

        await ToSignal(
            GetTree().CreateTimer(1.6),
            SceneTreeTimer.SignalName.Timeout
        );

        float expectedHealth = Mathf.Min(
            damagedHealth + 35.0f,
            player.MaxHealth
        );

        if(
            !Mathf.IsEqualApprox(player.CurrentHealth, expectedHealth) ||
            player.MovementLocked ||
            consumableUse.IsUsing ||
            equipment.GetSlot(0) != null
        )
        {
            Fail("创口贴没有在1.5秒后回复35点并完成消耗。");
            return;
        }

        if(!inventory.TryAddItem(schoolBag.BandageItem, 1))
        {
            Fail("无法准备满血使用创口贴的测试物品。");
            return;
        }

        InventoryEntry fullHealthBandage = null;

        foreach(InventoryEntry entry in inventory.Entries)
        {
            if(entry.Data.ItemId == "bandage")
            {
                fullHealthBandage = entry;
                break;
            }
        }

        if(
            fullHealthBandage == null ||
            !equipment.TryEquip(fullHealthBandage, 0, inventory)
        )
        {
            Fail("无法把满血测试用创口贴放入快捷槽。");
            return;
        }

        player.RestoreHealth(player.MaxHealth);
        int fullHealthQuantity = equipment.GetSlot(0).Quantity;

        if(
            consumableUse.TryUseSlot(0) ||
            equipment.GetSlot(0)?.Quantity != fullHealthQuantity
        )
        {
            Fail("满血时仍然开始使用或消耗了创口贴。");
            return;
        }

        bool foundFullHealthMessage = false;

        foreach(Node node in map.FindChildren(
            "*",
            "",
            true,
            false
        ))
        {
            if(
                node is FloatingDamageNumber label &&
                label.Text == "当前血量已满，无法使用"
            )
            {
                foundFullHealthMessage = true;
                break;
            }
        }

        if(!foundFullHealthMessage)
        {
            Fail("满血使用创口贴时没有显示指定飞字。");
            return;
        }

        foreach(DungeonDoorData door in combatRoom.Doors)
        {
            foreach(Vector2I doorwayCell in door.GetOccupiedCells())
            {
                if(
                    doorIconLayer.GetCellSourceId(doorwayCell) < 0 &&
                    doorDirectionLayer.GetCellSourceId(doorwayCell) >= 0
                )
                {
                    continue;
                }

                Fail($"撤离房完成后门 {door.Id} 没有显示通行瓦片。");
                return;
            }
        }

        DungeonCorridorData exitCorridor =
            generator.GeneratedLayout.Corridors[
                combatRoom.Doors[0].CorridorId
            ];
        Vector2I corridorCell = exitCorridor.PathCells[
            exitCorridor.PathCells.Count / 2
        ];
        player.GlobalPosition = groundLayer.ToGlobal(
            groundLayer.MapToLocal(corridorCell)
        );

        for(int frame = 0; frame < 5; frame++)
        {
            await ToSignal(
                GetTree(),
                SceneTree.SignalName.PhysicsFrame
            );
        }

        if(roomNameView.Visible)
        {
            Fail("玩家进入走廊后房间名称没有隐藏。");
            return;
        }

        GD.Print("[DungeonRuntimeSmokeTest] PASS");
        GetTree().Quit(0);
    }

    private void Fail(string message)
    {
        GD.PushError($"[DungeonRuntimeSmokeTest] {message}");
        GetTree().Quit(1);
    }
}
