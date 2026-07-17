using Godot;


public partial class WeaponManager : Node
{
    // ASW固定三个武器槽
    private const int MaxWeaponSlot = 3;
    public int SlotCount => MaxWeaponSlot;




    [Signal]
    public delegate void WeaponChangedEventHandler(
        int slotIndex,
        Resource weapon
    );


    [Signal]
    public delegate void SelectedWeaponChangedEventHandler(
        int slotIndex
    );


    [Signal]
    public delegate void WeaponRemovedEventHandler(
        int slotIndex
    );


    // 武器挂载点
    [Export]
    public Node2D WeaponMount { get; set; } = null!;


    // 玩家节点
    [Export]
    public Node2D Player { get; set; } = null!;

    [Export]
    public InventoryModel Inventory { get; set; }

    // 武器槽位配置（在检查器中分配，槽位0为默认武器）
    [ExportGroup("武器槽位配置")]
    [Export] public WeaponSlotConfig[] WeaponSlots { get; set; } = new WeaponSlotConfig[MaxWeaponSlot];

    [Export]
    private PaintStrokeController _paintStrokeController;


    // 三个武器槽
    private readonly WeaponSlot[] _slots =
    {
        new WeaponSlot(),
        new WeaponSlot(),
        new WeaponSlot()
    };


    // 当前选中槽位
    private int _selectedSlot = -1;



    public int SelectedSlot
    {
        get
        {
            return _selectedSlot;
        }
    }



    /// <summary>
    /// 当前主动武器
    /// </summary>
    public WeaponBase? CurrentWeapon
    {
        get
        {
            if (!IsValidSlot(_selectedSlot))
                return null;


            return _slots[_selectedSlot].Controller;
        }
    }

    // ============================================================
    // 初始化
    // ============================================================

    public override void _Ready()
    {
        GD.Print("WeaponManager Ready");

        // [Export] NodePath 自动解析可能失败（WeaponManager 继承 Node 但场景中是 Node2D）
        // 手动 fallback：确保 Player 和 WeaponMount 引用有效
        if (!GodotObject.IsInstanceValid(Player))
        {
            Player = GetParent<Node2D>();
            GD.Print($"[WeaponManager] Player fallback 解析: {Player?.Name}");
        }
        if (!GodotObject.IsInstanceValid(WeaponMount))
        {
            WeaponMount = GetNodeOrNull<Node2D>("WeaponMount");
            GD.Print($"[WeaponManager] WeaponMount fallback 解析: {WeaponMount?.Name}");
        }
        if (!GodotObject.IsInstanceValid(Inventory))
            Inventory = GetParent()?.GetNodeOrNull<InventoryModel>("Inventory");

        // 从检查器配置实例化武器
        for (int i = 0; i < WeaponSlots.Length && i < MaxWeaponSlot; i++)
        {
            var config = WeaponSlots[i];
            if (config == null || config.ControllerScene == null)
                continue;

            Node node = config.ControllerScene.Instantiate();
            if (node is not WeaponBase weapon)
            {
                GD.PrintErr($"槽位 {i}: 武器场景必须继承 WeaponBase");
                node.QueueFree();
                continue;
            }

            WeaponMount.AddChild(weapon);

            var data = new WeaponData
            {
                WeaponId = config.ControllerScene.ResourcePath
                    .GetFile()
                    .GetBaseName(),
                DisplayName = weapon.Name,
                ControllerScene = config.ControllerScene,
                AllowDrop = !config.IsDefault
            };

            // 武器视觉可能嵌套在 Visual 节点中，递归寻找首个有效贴图。
            data.Icon = FindSpriteWithTexture(weapon)?.Texture;

            var instance = new WeaponInstance(data, isBound: config.IsDefault);
            weapon.Initialize(instance);
            weapon.SetEquipped(true);
            weapon.SetSelected(false); // 先隐藏所有武器，由 SelectFirstWeapon 显示选中的

            _slots[i].Instance = instance;
            _slots[i].Controller = weapon;

            GD.Print($"[WeaponManager] 槽位 {i} 实例化: {data.DisplayName}, 默认={config.IsDefault}");
        }

        // 自动选中第一把武器（默认槽位 0）
        if (_selectedSlot == -1)
        {
            SelectFirstWeapon();
        }
    }



    // ============================================================
    // 输入
    // ============================================================

    public override void _UnhandledInput(
        InputEvent inputEvent)
    {

        // 鼠标滚轮切换武器
        if (inputEvent is InputEventMouseButton mouseButton && mouseButton.Pressed)
        {
            if (mouseButton.ButtonIndex == MouseButton.WheelUp)
            {
                int previous = FindPreviousOccupiedSlot(_selectedSlot);
                if (previous != -1)
                    SwitchWeapon(previous);
            }
            else if (mouseButton.ButtonIndex == MouseButton.WheelDown)
            {
                int next = FindNextOccupiedSlot(_selectedSlot);
                if (next != -1)
                    SwitchWeapon(next);
            }
        }

        // 数字键切换武器
        if (inputEvent.IsActionPressed("weapon_slot_1"))
            SwitchWeapon(0);
        if (inputEvent.IsActionPressed("weapon_slot_2"))
            SwitchWeapon(1);
        if (inputEvent.IsActionPressed("weapon_slot_3"))
            SwitchWeapon(2);



        // 丢弃/拾取武器（Q键上下文相关：优先拾取附近武器，无则丢弃当前武器）

        if (inputEvent.IsActionPressed(
            "drop_weapon"))
        {
            if (!TryPickupNearbyWeapon())
            {
                DropWeapon(
                    _selectedSlot
                );
            }
        }
    }





    // ============================================================
    // 装备武器
    // ============================================================
    // 判断武器是否可以装备

    public bool EquipWeapon(
        int slotIndex,
        WeaponInstance instance)
    {
        if (!IsValidSlot(slotIndex))
            return false;

        if (!_slots[slotIndex].IsEmpty)
        {
            GD.Print(
                "该槽位已有武器"
            );

            return false;
        }
        // 创建武器控制器
        if (instance.Data.ControllerScene == null)
        {
            GD.PrintErr(
                "武器没有ControllerScene"
            );

            return false;
        }
        Node node =
            instance.Data.ControllerScene.Instantiate();
        if (node is not WeaponBase weapon)
        {
            GD.PrintErr(
                "武器场景必须继承WeaponBase"
            );
            node.QueueFree();
            return false;
        }
        WeaponMount.AddChild(
            weapon
        );
        weapon.Initialize(
            instance
        );
        weapon.SetEquipped(
            true
        );
        // 是否当前武器
        weapon.SetSelected(
            slotIndex == _selectedSlot
        );
        _slots[slotIndex].Instance =
            instance;
        _slots[slotIndex].Controller =
            weapon;
        EmitSignal(
            SignalName.WeaponChanged,
            slotIndex,
            instance.Data
        );
        // 如果当前没有武器
        if (_selectedSlot == -1)
        {
            SwitchWeapon(
                slotIndex
            );
        }
        return true;
    }


    // ============================================================
    // 切换武器
    // ============================================================
    public bool SwitchWeapon(int slotIndex)
    {

        if (!IsValidSlot(slotIndex))
            return false;
        if (_slots[slotIndex].IsEmpty)
            return false;
        // 取消旧武器选择
        if (IsValidSlot(_selectedSlot))
        {
            _slots[_selectedSlot]
                .Controller?
                .SetSelected(false);
        }



        // 选择新武器

        _selectedSlot =
            slotIndex;



        _slots[_selectedSlot]
            .Controller?
            .SetSelected(true);

        UpdatePaintWeapon(CurrentWeapon);

        EmitSignal(
            SignalName.SelectedWeaponChanged,
            slotIndex
        );


        GD.Print(
            $"当前武器：{_slots[slotIndex].Instance.Data.DisplayName}"
        );


        return true;
    }





    // ============================================================
    // 丢弃武器
    // ============================================================


    public WeaponInstance? RemoveWeapon(
        int slotIndex)
    {

        if (!IsValidSlot(slotIndex))
            return null;



        WeaponSlot slot =
            _slots[slotIndex];



        if (slot.IsEmpty)
            return null;



        WeaponInstance oldWeapon =
            slot.Instance!;



        // 删除控制器

        slot.Controller?
            .SetSelected(false);


        slot.Controller?
            .SetEquipped(false);



        slot.Controller?
            .QueueFree();



        slot.Instance = null;

        slot.Controller = null;



        EmitSignal(
            SignalName.WeaponRemoved,
            slotIndex
        );



        // 如果删除的是当前武器

        if (_selectedSlot == slotIndex)
        {
            _selectedSlot = -1;


            SelectFirstWeapon();
        }



        return oldWeapon;
    }





    public bool DropWeapon(
        int slotIndex)
    {
        GD.Print($"[DropWeapon] 开始, slot={slotIndex}, _selectedSlot={_selectedSlot}");

        // 槽位 0 为默认武器，不可丢弃
        if (slotIndex == 0)
        {
            GD.Print("[DropWeapon] 默认武器不可丢弃");
            return false;
        }

        WeaponInstance existingWeapon = GetWeapon(slotIndex);

        if (existingWeapon == null || !existingWeapon.CanDrop)
        {
            GD.Print("[DropWeapon] 该武器不能丢弃");
            return false;
        }

        WeaponInstance? weapon =
            RemoveWeapon(
                slotIndex
            );

        if (weapon == null)
        {
            GD.Print("[DropWeapon] RemoveWeapon 返回 null，槽位为空或无效");
            return false;
        }

        GD.Print($"[DropWeapon] RemoveWeapon 成功: {weapon.Data.DisplayName}, CanDrop={weapon.CanDrop}");

        // 在玩家位置生成武器掉落物
        // Player 可能在场景切换等情况下已被销毁
        if (!GodotObject.IsInstanceValid(Player))
        {
            GD.Print("[DropWeapon] Player 节点无效，无法获取位置");
            return false;
        }

        GD.Print($"[DropWeapon] Player 有效, 位置={Player.GlobalPosition}, 准备创建掉落物");

        CreateWeaponPickup(
            weapon,
            Player.GlobalPosition
        );

        GD.Print($"[DropWeapon] 丢弃完成: {weapon.Data.DisplayName}");
        return true;
    }


    // ============================================================
    // 武器掉落物
    // ============================================================

    /// <summary>
    /// 在指定位置生成武器掉落物
    /// </summary>
    public void CreateWeaponPickup(
        WeaponInstance instance,
        Vector2 globalPosition)
    {
        var drop = new WeaponDropSpawner();

        // 先添加到场景树（触发 _Ready 创建碰撞体和连接信号）
        GetTree().CurrentScene.AddChild(drop);

        // 再初始化（此时在场景树中，GlobalPosition 和视觉都能正确设置）
        drop.Initialize(instance, globalPosition);

        GD.Print($"[WeaponManager] 生成掉落物: {instance.Data.DisplayName} @ {globalPosition}");
    }

    /// <summary>
    /// 尝试拾取附近的武器掉落物
    /// </summary>
    private bool TryPickupNearbyWeapon()
    {
        foreach (var node in GetTree().GetNodesInGroup("weapon_drops"))
        {
            if (node is not WeaponDropSpawner drop)
                continue;

            if (!drop.CanPickup)
                continue;

            int slotIndex = FindEmptySlot();
            if (slotIndex == -1)
            {
                WeaponInstance backpackWeapon = drop.GetInstance();

                if (
                    Inventory != null &&
                    Inventory.TryAddWeapon(backpackWeapon, out _)
                )
                {
                    drop.QueueFree();
                    GD.Print($"武器槽已满，{backpackWeapon.Data.DisplayName} 已收入背包");
                }
                else
                {
                    GD.Print("武器槽已满，背包也无法容纳该武器");
                }

                return true;
            }

            WeaponInstance instance = drop.GetInstance();
            drop.QueueFree();

            EquipWeapon(slotIndex, instance);
            GD.Print($"拾取:{instance.Data.DisplayName}");
            return true;
        }
        return false;
    }

    public bool TryEquipInventoryWeapon(
        InventoryWeaponEntry entry,
        int slotIndex
    )
    {
        if (
            entry == null ||
            Inventory == null ||
            !Inventory.Contains(entry) ||
            !IsValidSlot(slotIndex)
        )
        {
            return false;
        }

        WeaponInstance previous = GetWeapon(slotIndex);
        bool wasSelected = SelectedSlot == slotIndex;

        if (previous != null && previous.IsBound)
            return false;

        float projectedWeight =
            Inventory.CurrentWeight -
            entry.TotalWeight +
            (previous?.Data.Weight ?? 0.0f);

        if (projectedWeight > Inventory.MaxWeight + 0.001f)
            return false;

        Vector2I originalPosition = entry.GridPosition;
        Inventory.RemoveEntry(entry, false);

        WeaponInstance removed = previous == null
            ? null
            : RemoveWeapon(slotIndex);

        InventoryWeaponEntry storedPrevious = null;
        if (
            removed != null &&
            !Inventory.TryAddWeapon(removed, out storedPrevious)
        )
        {
            EquipWeapon(slotIndex, removed);
            Inventory.TryAddExisting(entry);
            return false;
        }

        if (EquipWeapon(slotIndex, entry.Weapon))
        {
            if (wasSelected)
                SwitchWeapon(slotIndex);
            return true;
        }

        if (storedPrevious != null)
        {
            Inventory.RemoveEntry(storedPrevious, false);
            EquipWeapon(slotIndex, storedPrevious.Weapon);
        }

        entry.GridPosition = originalPosition;
        Inventory.TryAddExisting(entry);
        return false;
    }

    private static Sprite2D FindSpriteWithTexture(Node node)
    {
        if (node is Sprite2D sprite && sprite.Texture != null)
            return sprite;

        foreach (Node child in node.GetChildren())
        {
            Sprite2D found = FindSpriteWithTexture(child);
            if (found != null)
                return found;
        }

        return null;
    }

    public bool DropInventoryWeapon(InventoryWeaponEntry entry)
    {
        if (
            entry == null ||
            Inventory == null ||
            !Inventory.Contains(entry) ||
            !entry.Weapon.CanDrop ||
            !GodotObject.IsInstanceValid(Player)
        )
        {
            return false;
        }

        if (!Inventory.RemoveEntry(entry))
            return false;

        CreateWeaponPickup(entry.Weapon, Player.GlobalPosition);
        return true;
    }

    /// <summary>
    /// 查找第一个空槽位
    /// </summary>
    private int FindEmptySlot()
    {
        for (int i = 0; i < MaxWeaponSlot; i++)
        {
            if (_slots[i].IsEmpty)
                return i;
        }
        return -1;
    }






    // ============================================================
    // 替换武器
    // ============================================================


    public WeaponInstance? ReplaceWeapon(
        int slotIndex,
        WeaponInstance newWeapon)
    {

        if (!IsValidSlot(slotIndex))
            return null;



        WeaponInstance? oldWeapon =
            RemoveWeapon(
                slotIndex
            );



        bool success =
            EquipWeapon(
                slotIndex,
                newWeapon
            );



        if (!success)
        {
            // 如果失败恢复旧武器

            if (oldWeapon != null)
            {
                EquipWeapon(
                    slotIndex,
                    oldWeapon
                );
            }


            return null;
        }



        return oldWeapon;
    }







    // ============================================================
    // 工具函数
    // ============================================================



    private void SelectFirstWeapon()
    {

        for (int i = 0; i < MaxWeaponSlot; i++)
        {
            if (!_slots[i].IsEmpty)
            {
                SwitchWeapon(i);
                return;
            }
        }


        GD.Print(
            "当前没有装备武器"
        );
    }





    public WeaponInstance? GetWeapon(
        int slotIndex)
    {

        if (!IsValidSlot(slotIndex))
            return null;


        return _slots[slotIndex].Instance;
    }





    public bool HasEmptySlot()
    {
        foreach (var slot in _slots)
        {
            if (slot.IsEmpty)
                return true;
        }


        return false;
    }





    private bool IsValidSlot(
        int slot)
    {
        return slot >= 0 &&
               slot < MaxWeaponSlot;
    }

    /// <summary>
    /// 从当前槽位向后查找下一个有武器的槽位（循环）
    /// </summary>
    private int FindNextOccupiedSlot(int current)
    {
        if (current == -1) return -1;
        for (int i = 1; i < MaxWeaponSlot; i++)
        {
            int next = (current + i) % MaxWeaponSlot;
            if (!_slots[next].IsEmpty)
                return next;
        }
        return -1;
    }

    /// <summary>
    /// 从当前槽位向前查找上一个有武器的槽位（循环）
    /// </summary>
    private int FindPreviousOccupiedSlot(int current)
    {
        if (current == -1) return -1;
        for (int i = 1; i < MaxWeaponSlot; i++)
        {
            int prev = (current - i + MaxWeaponSlot) % MaxWeaponSlot;
            if (!_slots[prev].IsEmpty)
                return prev;
        }
        return -1;
    }

    private void UpdatePaintWeapon(WeaponBase currentWeapon)
    {
        IPaintSkillWeapon paintWeapon = currentWeapon as IPaintSkillWeapon;

        if (!GodotObject.IsInstanceValid(_paintStrokeController))
        {
            GD.PushWarning(
                "WeaponManager 没有绑定 PaintStrokeController，" +
                "当前武器可以正常切换，但绘画技能暂不可用。"
            );
            return;
        }

        _paintStrokeController.SetActiveWeapon(paintWeapon);
    }

}
