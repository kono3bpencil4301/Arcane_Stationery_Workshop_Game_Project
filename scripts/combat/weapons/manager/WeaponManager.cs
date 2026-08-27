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

        // [Export] NodePath 自动解析可能失败（WeaponManager 继承 Node 但场景中是 Node2D）
        // 手动 fallback：确保 Player 和 WeaponMount 引用有效
        if (!GodotObject.IsInstanceValid(Player))
        {
            Player = GetParent<Node2D>();
        }
        if (!GodotObject.IsInstanceValid(WeaponMount))
        {
            WeaponMount = GetNodeOrNull<Node2D>("WeaponMount");
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
    public bool EquipWeapon(int slotIndex, WeaponInstance instance)
    {
        if (
            !IsValidSlot(slotIndex) ||
            !_slots[slotIndex].IsEmpty ||
            instance == null
        )
            return false;

        return SwapSlot(slotIndex, instance, out _);
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
        return true;
    }
    // ============================================================
    // 丢弃武器
    // ============================================================

    public WeaponInstance? RemoveWeapon(int slotIndex)
    {
        if (!IsValidSlot(slotIndex))
            return null;

        bool wasSelected = _selectedSlot == slotIndex;
        WeaponInstance? removed = RemoveFromSlot(slotIndex);
        if (removed == null)
            return null;

        EmitSignal(SignalName.WeaponRemoved, slotIndex);

        if (wasSelected)
        {
            _selectedSlot = -1;
            SelectFirstWeapon();

            if (_selectedSlot == -1)
                UpdatePaintWeapon(null);
        }

        return removed;
    }
    public bool DropWeapon(
        int slotIndex)
    {
        GD.Print($"[DropWeapon] 开始, slot={slotIndex}, _selectedSlot={_selectedSlot}");

        // 槽位 0 为默认武器，不可丢弃
        if (slotIndex == 0) return false;
        WeaponInstance existingWeapon = GetWeapon(slotIndex);
        if (existingWeapon == null || !existingWeapon.CanDrop) return false;
        if (!GodotObject.IsInstanceValid(Player) || Player == null) return false;
        WeaponInstance? weapon = RemoveWeapon(slotIndex);
        if (weapon == null) return false;

        GD.Print($"[DropWeapon] RemoveWeapon 成功: {weapon.Data.DisplayName}, CanDrop={weapon.CanDrop}");
        // 在玩家位置生成武器掉落物
        // Player 可能在场景切换等情况下已被销毁
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
    public void CreateWeaponPickup(WeaponInstance instance, Vector2 globalPosition)
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
            if (node is not WeaponDropSpawner drop) continue;
            if (!drop.CanPickup) continue;
            int slotIndex = FindEmptySlot();
            if (slotIndex == -1)
            {
                WeaponInstance backpackWeapon = drop.GetInstance();
                if (Inventory != null && Inventory.TryAddWeapon(backpackWeapon, out _))
                    drop.QueueFree();
                return true;
            }
            WeaponInstance instance = drop.GetInstance();
            if (EquipWeapon(slotIndex, instance))
                drop.QueueFree();

            // 附近存在可拾取物时，不应继续执行“丢弃当前武器”。
            return true;
        }
        return false;
    }

    public bool TryEquipInventoryWeapon(InventoryWeaponEntry entry, int slotIndex)
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

        WeaponInstance? previousWeapon = GetWeapon(slotIndex);

        if (previousWeapon != null && previousWeapon.IsBound)
            return false;

        float projectedWeight =
            Inventory.CurrentWeight -
            entry.TotalWeight +
            (previousWeapon?.Data.Weight ?? 0.0f);

        if (projectedWeight > Inventory.MaxWeight + 0.001f)
            return false;

        Vector2I originalPosition = entry.GridPosition;

        if (!Inventory.RemoveEntry(entry))
            return false;

        // 先确认背包确实能容纳旧武器，再提交槽位交换。
        // 这样 SwapSlot 失败时，原槽位仍保持不变，不需要重建旧控制器。
        InventoryWeaponEntry storedPrevious = null;
        if (
            previousWeapon != null &&
            !Inventory.TryAddWeapon(previousWeapon, out storedPrevious)
        )
        {
            RestoreInventoryEntry(entry, originalPosition);
            return false;
        }

        if (SwapSlot(slotIndex, entry.Weapon, out WeaponInstance? outgoing))
            return true;

        if (storedPrevious != null)
            Inventory.RemoveEntry(storedPrevious);

        RestoreInventoryEntry(entry, originalPosition);
        GD.PushError(
            $"无法创建武器控制器：{entry.Weapon.Data.DisplayName}。"
        );
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
        if (entry == null || Inventory == null || !Inventory.Contains(entry) || !entry.Weapon.CanDrop || !GodotObject.IsInstanceValid(Player))
            return false;

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
        GD.Print("当前没有装备武器");
    }

    public WeaponInstance? GetWeapon(int slotIndex)
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

    private void UpdatePaintWeapon(WeaponBase? currentWeapon)
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

    private bool SwapSlot(int slotIndex, WeaponInstance incoming, out WeaponInstance? outgoing)
    {
        outgoing = null;

        if (!IsValidSlot(slotIndex) || incoming == null)
            return false;

        WeaponBase? incomingController = CreateController(incoming);
        if (incomingController == null)
            return false;

        bool wasSelected = _selectedSlot == slotIndex;

        outgoing = RemoveFromSlot(slotIndex);

        WeaponSlot slot = _slots[slotIndex];
        slot.Instance = incoming;
        slot.Controller = incomingController;
        if (wasSelected)
        {
            incomingController.SetSelected(true);
            UpdatePaintWeapon(incomingController);
        }
        else if (_selectedSlot == -1)
        {
            SwitchWeapon(slotIndex);
        }

        if (outgoing != null)
            EmitSignal(SignalName.WeaponRemoved, slotIndex);

        EmitSignal(SignalName.WeaponChanged, slotIndex, incoming.Data);

        return true;
    }

    private WeaponInstance? RemoveFromSlot(int slotIndex)
    {
        if (!IsValidSlot(slotIndex))
            return null;

        WeaponSlot slot = _slots[slotIndex];
        WeaponInstance? outgoing = slot.Instance;
        if (outgoing == null)
        {
            return null;
        }

        WeaponBase? controller = slot.Controller;

        slot.Instance = null;
        slot.Controller = null;

        controller?.SetSelected(false);
        controller?.SetEquipped(false);
        controller?.QueueFree();

        return outgoing;
    }

    private WeaponBase? CreateController(WeaponInstance instance)
    {
        PackedScene scene = instance.Data.ControllerScene;
        if (scene == null || !GodotObject.IsInstanceValid(WeaponMount))
            return null;
        Node node = scene.Instantiate();
        if (node is not WeaponBase controller)
        {
            GD.PushError("武器场景必须继承 WeaponBase。");
            node.QueueFree();
            return null;
        }

        WeaponMount.AddChild(controller);
        controller.Initialize(instance);
        controller.SetEquipped(true);
        controller.SetSelected(false);

        return controller;
    }

    private void RestoreInventoryEntry(
        InventoryWeaponEntry entry,
        Vector2I originalPosition
    )
    {
        if (Inventory == null || !Inventory.TryAddExisting(entry))
        {
            GD.PushError(
                $"无法回滚背包武器：{entry.Weapon.Data.DisplayName}。"
            );
            return;
        }

        if (entry.GridPosition != originalPosition)
            Inventory.TryMoveEntry(entry, originalPosition);
    }
}
