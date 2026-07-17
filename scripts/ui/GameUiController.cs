using Godot;

public partial class GameUiController : CanvasLayer
{
    private const string InventoryAction = "inventory_toggle";

    private InventoryPanelController _inventoryPanel;
    private bool _inventoryOpen;
    private bool _previousPauseState;
    private Input.MouseModeEnum _previousMouseMode;
    private PaintStrokeController _paintController;

    public override void _Ready()
    {
        ProcessMode = ProcessModeEnum.Always;
        SetProcessInput(true);

        Player player = GetTree().GetFirstNodeInGroup("player") as Player;
        WeaponManager weaponManager =
            player?.GetNodeOrNull<WeaponManager>("WeaponManager");
        SharedSkillCharge skillCharge =
            player?.GetNodeOrNull<SharedSkillCharge>("SharedSkillCharge");
        InventoryModel inventory =
            player?.GetNodeOrNull<InventoryModel>("Inventory");
        EquipmentModel equipment =
            player?.GetNodeOrNull<EquipmentModel>("Equipment");
        RunHudState runState =
            player?.GetNodeOrNull<RunHudState>("RunHudState");
        _paintController = player?.GetNodeOrNull<PaintStrokeController>(
            "WeaponManager/PaintStrokeController"
        );
        EnemySpawner spawner = GetTree().CurrentScene?
            .GetNodeOrNull<EnemySpawner>("EnemySpawnerTileLayer");

        if (
            player == null ||
            weaponManager == null ||
            skillCharge == null ||
            inventory == null ||
            equipment == null
        )
        {
            GD.PushError("GameUI 找不到玩家 HUD 依赖，界面未初始化。");
            return;
        }

        Control root = new()
        {
            Name = "UiRoot",
            ProcessMode = ProcessModeEnum.Always,
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        root.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        AddChild(root);

        StatusClusterView status = new();
        status.Bind(player, skillCharge, runState);
        status.Position = new Vector2(12, 10);
        root.AddChild(status);

        WaveTimerView timer = new();
        timer.Bind(spawner);
        timer.AnchorLeft = 0.5f;
        timer.AnchorRight = 0.5f;
        timer.OffsetLeft = -56;
        timer.OffsetTop = 10;
        timer.OffsetRight = 56;
        timer.OffsetBottom = 44;
        root.AddChild(timer);

        WeaponBarView weaponBar = new();
        weaponBar.Bind(weaponManager);
        weaponBar.AnchorTop = 1.0f;
        weaponBar.AnchorBottom = 1.0f;
        weaponBar.OffsetLeft = 14;
        weaponBar.OffsetTop = -74;
        weaponBar.OffsetRight = 198;
        weaponBar.OffsetBottom = -14;
        root.AddChild(weaponBar);

        EquipmentBarView equipmentBar = new();
        equipmentBar.Bind(equipment, inventory);
        equipmentBar.AnchorLeft = 0.5f;
        equipmentBar.AnchorRight = 0.5f;
        equipmentBar.AnchorTop = 1.0f;
        equipmentBar.AnchorBottom = 1.0f;
        equipmentBar.OffsetLeft = -178;
        equipmentBar.OffsetTop = -58;
        equipmentBar.OffsetRight = 178;
        equipmentBar.OffsetBottom = -14;
        root.AddChild(equipmentBar);

        _inventoryPanel = new InventoryPanelController();
        _inventoryPanel.Bind(inventory, equipment, weaponManager);
        root.AddChild(_inventoryPanel);

        weaponBar.DropRequested += _inventoryPanel.TryEquipWeaponEntry;
        equipmentBar.DropRequested += _inventoryPanel.TryEquipItemEntry;
    }

    public override void _Input(InputEvent inputEvent)
    {
        if (
            inputEvent.IsActionPressed(InventoryAction) ||
            IsTabPressed(inputEvent)
        )
        {
            SetInventoryOpen(!_inventoryOpen);
            GetViewport().SetInputAsHandled();
            return;
        }

        if (_inventoryOpen && inputEvent.IsActionPressed("ui_cancel"))
        {
            SetInventoryOpen(false);
            GetViewport().SetInputAsHandled();
        }
    }

    private static bool IsTabPressed(InputEvent inputEvent)
    {
        return
            inputEvent is InputEventKey keyEvent &&
            keyEvent.Pressed &&
            !keyEvent.Echo &&
            (
                keyEvent.Keycode == Key.Tab ||
                keyEvent.PhysicalKeycode == Key.Tab
            );
    }

    public void SetInventoryOpen(bool open)
    {
        if (_inventoryPanel == null || _inventoryOpen == open)
            return;

        _inventoryOpen = open;

        if (open)
        {
            _paintController?.CancelPainting();
            _previousPauseState = GetTree().Paused;
            _previousMouseMode = Input.MouseMode;
            _inventoryPanel.ShowPanel();
            Input.MouseMode = Input.MouseModeEnum.Visible;
            GetTree().Paused = true;
            return;
        }

        _inventoryPanel.HidePanel();
        GetTree().Paused = _previousPauseState;
        Input.MouseMode = _previousMouseMode;
    }

    public override void _ExitTree()
    {
        if (_inventoryOpen && GetTree() != null)
        {
            GetTree().Paused = _previousPauseState;
            Input.MouseMode = _previousMouseMode;
        }
    }
}
