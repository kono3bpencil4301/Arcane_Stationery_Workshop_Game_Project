using Godot;
using System;

public partial class InventoryPanelController : Control
{
    private InventoryModel _inventory;
    private EquipmentModel _equipment;
    private WeaponManager _weapons;
    private GridContainer _grid;
    private Label _weightLabel;
    private Label _detailName;
    private Label _detailBody;
    private Label _feedbackLabel;
    private Button _equipButton;
    private Button _dropButton;
    private PopupMenu _contextMenu;
    private InventoryEntry _selectedEntry;
    private InventoryEntry _contextEntry;

    public void Bind(
        InventoryModel inventory,
        EquipmentModel equipment,
        WeaponManager weapons
    )
    {
        _inventory = inventory;
        _equipment = equipment;
        _weapons = weapons;
    }

    public override void _Ready()
    {
        ProcessMode = ProcessModeEnum.Always;
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        MouseFilter = MouseFilterEnum.Stop;

        ColorRect shade = new()
        {
            Color = new Color(0.025f, 0.035f, 0.09f, 0.78f),
            MouseFilter = MouseFilterEnum.Stop
        };
        shade.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddChild(shade);

        PanelContainer panel = new();
        panel.AnchorLeft = 0.5f;
        panel.AnchorTop = 0.5f;
        panel.AnchorRight = 0.5f;
        panel.AnchorBottom = 0.5f;
        panel.OffsetLeft = -380;
        panel.OffsetTop = -230;
        panel.OffsetRight = 380;
        panel.OffsetBottom = 230;
        panel.AddThemeStyleboxOverride(
            "panel",
            UiPalette.MakePanel(
                new Color(UiPalette.Panel, 0.97f),
                UiPalette.WarningCoral,
                2
            )
        );
        AddChild(panel);

        VBoxContainer layout = new();
        layout.AddThemeConstantOverride("separation", 7);
        panel.AddChild(layout);

        HBoxContainer header = new();
        layout.AddChild(header);

        Label title = new()
        {
            Text = "背包 / 异常档案",
            SizeFlagsHorizontal = SizeFlags.ExpandFill
        };
        UiPalette.StyleLabel(title, 16);
        header.AddChild(title);

        _weightLabel = new Label
        {
            HorizontalAlignment = HorizontalAlignment.Right
        };
        UiPalette.StyleLabel(_weightLabel, 12);
        header.AddChild(_weightLabel);

        HSeparator separator = new();
        separator.Modulate = UiPalette.ArchiveBlue;
        layout.AddChild(separator);

        HBoxContainer content = new()
        {
            SizeFlagsVertical = SizeFlags.ExpandFill
        };
        content.AddThemeConstantOverride("separation", 12);
        layout.AddChild(content);

        VBoxContainer left = new()
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill
        };
        content.AddChild(left);

        Label gridTitle = new() { Text = "物资网格  8 × 6" };
        UiPalette.StyleLabel(gridTitle, 11);
        left.AddChild(gridTitle);

        _grid = new GridContainer
        {
            Columns = Mathf.Max(_inventory?.Columns ?? 8, 1)
        };
        _grid.AddThemeConstantOverride("h_separation", 2);
        _grid.AddThemeConstantOverride("v_separation", 2);
        left.AddChild(_grid);

        VBoxContainer detail = new()
        {
            CustomMinimumSize = new Vector2(205, 0)
        };
        detail.AddThemeConstantOverride("separation", 8);
        content.AddChild(detail);

        Label detailTitle = new() { Text = "物品详情" };
        UiPalette.StyleLabel(detailTitle, 11);
        detail.AddChild(detailTitle);

        _detailName = new Label
        {
            Text = "未选择物品",
            AutowrapMode = TextServer.AutowrapMode.WordSmart
        };
        UiPalette.StyleLabel(_detailName, 15);
        detail.AddChild(_detailName);

        _detailBody = new Label
        {
            Text = "左键选择，拖拽到武器槽或装备栏。\n右键可打开操作菜单。",
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
            SizeFlagsVertical = SizeFlags.ExpandFill
        };
        UiPalette.StyleLabel(_detailBody, 11);
        _detailBody.AddThemeColorOverride("font_color", UiPalette.Muted);
        detail.AddChild(_detailBody);

        _equipButton = new Button { Text = "装备" };
        UiPalette.StyleButton(_equipButton, 12);
        _equipButton.Pressed += EquipSelected;
        detail.AddChild(_equipButton);

        _dropButton = new Button { Text = "丢弃" };
        UiPalette.StyleButton(_dropButton, 12);
        _dropButton.Pressed += DropSelected;
        detail.AddChild(_dropButton);

        _feedbackLabel = new Label
        {
            Text = "",
            AutowrapMode = TextServer.AutowrapMode.WordSmart
        };
        UiPalette.StyleLabel(_feedbackLabel, 10);
        _feedbackLabel.AddThemeColorOverride(
            "font_color",
            UiPalette.WarningCoral
        );
        detail.AddChild(_feedbackLabel);

        HBoxContainer loadout = new();
        loadout.AddThemeConstantOverride("separation", 12);
        layout.AddChild(loadout);

        VBoxContainer weaponsColumn = new();
        loadout.AddChild(weaponsColumn);
        Label weaponTitle = new() { Text = "三武器槽" };
        UiPalette.StyleLabel(weaponTitle, 10);
        weaponsColumn.AddChild(weaponTitle);
        WeaponBarView weaponBar = new();
        weaponBar.Bind(_weapons);
        weaponBar.DropRequested += TryEquipWeaponEntry;
        weaponsColumn.AddChild(weaponBar);

        VBoxContainer equipmentColumn = new()
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill
        };
        loadout.AddChild(equipmentColumn);
        Label equipmentTitle = new() { Text = "装备栏（右键卸下）" };
        UiPalette.StyleLabel(equipmentTitle, 10);
        equipmentColumn.AddChild(equipmentTitle);
        EquipmentBarView equipmentBar = new();
        equipmentBar.Bind(_equipment, _inventory);
        equipmentBar.DropRequested += TryEquipItemEntry;
        equipmentColumn.AddChild(equipmentBar);

        _contextMenu = new PopupMenu();
        _contextMenu.AddItem("装备", 1);
        _contextMenu.AddItem("丢弃", 2);
        _contextMenu.IdPressed += OnContextAction;
        AddChild(_contextMenu);

        if (_inventory != null)
        {
            _inventory.InventoryChanged += Refresh;
            _inventory.FeedbackRequested += ShowFeedback;
        }

        Refresh();
        Visible = false;
    }

    public void ShowPanel()
    {
        Refresh();
        Visible = true;
        _feedbackLabel.Text = "";
    }

    public void HidePanel()
    {
        Visible = false;
        _contextMenu?.Hide();
    }

    public void TryEquipWeaponEntry(string entryId, int slotIndex)
    {
        if (
            _inventory?.FindById(entryId) is InventoryWeaponEntry weaponEntry &&
            _weapons?.TryEquipInventoryWeapon(weaponEntry, slotIndex) == true
        )
        {
            _selectedEntry = null;
            ShowFeedback("武器已装备");
            Refresh();
            return;
        }

        ShowFeedback("无法装备到该武器槽");
    }

    public void TryEquipItemEntry(string entryId, int slotIndex)
    {
        InventoryEntry entry = _inventory?.FindById(entryId);
        if (
            entry != null &&
            _equipment?.TryEquip(entry, slotIndex, _inventory) == true
        )
        {
            _selectedEntry = null;
            ShowFeedback("物资已装备");
            Refresh();
            return;
        }

        ShowFeedback("该物资不能放入装备栏");
    }

    private void Refresh()
    {
        if (_grid == null || _inventory == null)
            return;

        foreach (Node child in _grid.GetChildren())
        {
            _grid.RemoveChild(child);
            child.QueueFree();
        }

        _grid.Columns = Mathf.Max(_inventory.Columns, 1);

        for (int y = 0; y < _inventory.Rows; y++)
        {
            for (int x = 0; x < _inventory.Columns; x++)
            {
                Vector2I cellPosition = new(x, y);
                InventoryEntry occupant = FindOccupant(cellPosition);
                bool isTopLeft = occupant?.GridPosition == cellPosition;

                InventoryItemView cell = new()
                {
                    CellPosition = cellPosition
                };
                cell.Selected += SelectEntry;
                cell.ContextRequested += OpenContextMenu;
                cell.MoveRequested += MoveEntry;
                _grid.AddChild(cell);
                cell.SetEntry(occupant, isTopLeft);
            }
        }

        _weightLabel.Text =
            $"负重  {_inventory.CurrentWeight:0.0} / " +
            $"{_inventory.MaxWeight:0.0} kg";

        if (_selectedEntry != null && !_inventory.Contains(_selectedEntry))
            _selectedEntry = null;

        RefreshDetails();
    }

    private InventoryEntry FindOccupant(Vector2I cellPosition)
    {
        foreach (InventoryEntry entry in _inventory.Entries)
        {
            Rect2I occupied = new(entry.GridPosition, entry.OccupiedSize);
            if (occupied.HasPoint(cellPosition))
                return entry;
        }

        return null;
    }

    private void SelectEntry(InventoryEntry entry)
    {
        _selectedEntry = entry;
        _feedbackLabel.Text = "";
        RefreshDetails();
    }

    private void RefreshDetails()
    {
        if (_detailName == null)
            return;

        if (_selectedEntry == null)
        {
            _detailName.Text = "未选择物品";
            _detailBody.Text =
                "左键选择，拖拽到武器槽或装备栏。\n右键可打开操作菜单。";
            _equipButton.Disabled = true;
            _dropButton.Disabled = true;
            return;
        }

        InventoryItemData data = _selectedEntry.Data;
        _detailName.Text = data.DisplayName;
        _detailBody.Text =
            $"类别：{CategoryName(data.Category)}\n" +
            $"数量：{_selectedEntry.Quantity}\n" +
            $"重量：{_selectedEntry.TotalWeight:0.0} kg\n" +
            $"价值：{data.Value}\n" +
            $"污染：{(data.IsPolluted ? "是" : "否")}";
        _equipButton.Disabled =
            !_selectedEntry.IsWeapon &&
            !data.IsEquipable &&
            !data.IsQuickUsable;
        _equipButton.Text = data.IsQuickUsable
            ? "放入快捷栏"
            : "装备";
        _dropButton.Disabled =
            _selectedEntry is InventoryWeaponEntry weaponEntry &&
            !weaponEntry.Weapon.CanDrop;
    }

    private void EquipSelected()
    {
        if (_selectedEntry == null)
            return;

        if (_selectedEntry is InventoryWeaponEntry weaponEntry)
        {
            int targetSlot = FindFirstEmptyWeaponSlot();
            if (targetSlot < 0)
                targetSlot = Mathf.Max(_weapons?.SelectedSlot ?? 0, 0);
            TryEquipWeaponEntry(weaponEntry.EntryId, targetSlot);
            return;
        }

        for (int slotIndex = 0; slotIndex < _equipment.SlotCount; slotIndex++)
        {
            if (_equipment.GetSlot(slotIndex) != null)
                continue;

            TryEquipItemEntry(_selectedEntry.EntryId, slotIndex);
            return;
        }

        ShowFeedback("装备栏已满");
    }

    private void DropSelected()
    {
        if (_selectedEntry == null)
            return;

        bool dropped = _selectedEntry is InventoryWeaponEntry weaponEntry
            ? _weapons?.DropInventoryWeapon(weaponEntry) == true
            : _inventory.RemoveEntry(_selectedEntry);

        if (!dropped)
        {
            ShowFeedback("该物品不能丢弃");
            return;
        }

        _selectedEntry = null;
        ShowFeedback("物品已丢弃");
        Refresh();
    }

    private void MoveEntry(string entryId, Vector2I targetPosition)
    {
        InventoryEntry entry = _inventory.FindById(entryId);
        if (entry == null || !_inventory.TryMoveEntry(entry, targetPosition))
            ShowFeedback("目标格位不可用");
    }

    private void OpenContextMenu(InventoryEntry entry, Vector2 position)
    {
        _contextEntry = entry;
        _selectedEntry = entry;
        RefreshDetails();
        _contextMenu.Position = (Vector2I)position;
        _contextMenu.Popup();
    }

    private void OnContextAction(long actionId)
    {
        _selectedEntry = _contextEntry;
        if (actionId == 1)
            EquipSelected();
        else if (actionId == 2)
            DropSelected();
    }

    private int FindFirstEmptyWeaponSlot()
    {
        for (int slotIndex = 0; slotIndex < 3; slotIndex++)
        {
            if (_weapons?.GetWeapon(slotIndex) == null)
                return slotIndex;
        }

        return -1;
    }

    private void ShowFeedback(string message)
    {
        if (_feedbackLabel != null)
            _feedbackLabel.Text = message;
    }

    private static string CategoryName(InventoryItemCategory category)
    {
        return category switch
        {
            InventoryItemCategory.Material => "材料",
            InventoryItemCategory.Equipment => "装备物资",
            InventoryItemCategory.Consumable => "消耗品",
            InventoryItemCategory.Weapon => "备用武器",
            _ => "未知"
        };
    }

    public override void _ExitTree()
    {
        if (_inventory != null)
        {
            _inventory.InventoryChanged -= Refresh;
            _inventory.FeedbackRequested -= ShowFeedback;
        }
    }
}
