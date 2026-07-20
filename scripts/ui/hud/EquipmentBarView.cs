using Godot;
using System;

public partial class EquipmentSlotView : Button
{
    private EquipmentModel _equipment;
    private InventoryModel _inventory;
    private int _slotIndex;
    private Label _quantityLabel;

    public event Action<string, int> DropRequested;
    public event Action<int> UseRequested;

    public void Bind(
        EquipmentModel equipment,
        InventoryModel inventory,
        int slotIndex
    )
    {
        _equipment = equipment;
        _inventory = inventory;
        _slotIndex = slotIndex;
    }

    public override void _Ready()
    {
        CustomMinimumSize = new Vector2(42, 42);
        FocusMode = FocusModeEnum.None;
        ExpandIcon = true;
        AddThemeConstantOverride("icon_max_width", 28);
        UiPalette.StyleButton(this, 10);
        CreateQuantityLabel();
    }

    public override void _Process(double delta)
    {
        InventoryEntry entry = _equipment?.GetSlot(_slotIndex);
        Icon = entry?.Data.Icon;
        Text = entry == null
            ? (_slotIndex + 1).ToString()
            : entry.Data.Icon == null
                ? entry.Data.DisplayName
                : "";
        SetQuantityLabel(entry?.Quantity ?? 0, entry != null);
        TooltipText = entry == null
            ? $"装备栏 {_slotIndex + 1}（空）"
            : entry.Data.IsQuickUsable
                ? $"{entry.Data.DisplayName}\n" +
                    $"重量 {entry.TotalWeight:0.0} kg\n" +
                    "左键使用；使用中右键取消，未使用时右键卸下"
                : $"{entry.Data.DisplayName}\n" +
                    $"重量 {entry.TotalWeight:0.0} kg\n右键卸下";
    }

    private void CreateQuantityLabel()
    {
        _quantityLabel = new Label
        {
            Name = "QuantityLabel",
            MouseFilter = MouseFilterEnum.Ignore,
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Bottom,
            AnchorLeft = 1.0f,
            AnchorTop = 1.0f,
            AnchorRight = 1.0f,
            AnchorBottom = 1.0f,
            OffsetLeft = -27.0f,
            OffsetTop = -21.0f,
            OffsetRight = -3.0f,
            OffsetBottom = -2.0f,
            ZIndex = 20
        };
        UiPalette.StyleLabel(_quantityLabel, 11);
        _quantityLabel.AddThemeColorOverride(
            "font_outline_color",
            Colors.Black
        );
        _quantityLabel.AddThemeConstantOverride("outline_size", 3);
        AddChild(_quantityLabel);
    }

    private void SetQuantityLabel(int quantity, bool visible)
    {
        if (_quantityLabel == null)
            return;

        _quantityLabel.Text = visible
            ? Mathf.Max(quantity, 0).ToString()
            : "";
        _quantityLabel.Visible = visible;
    }

    public override bool _CanDropData(Vector2 atPosition, Variant data)
    {
        return data.VariantType == Variant.Type.String;
    }

    public override void _DropData(Vector2 atPosition, Variant data)
    {
        DropRequested?.Invoke(data.AsString(), _slotIndex);
    }

    public override void _GuiInput(InputEvent inputEvent)
    {
        if (
            inputEvent is InputEventMouseButton mouseButton &&
            mouseButton.Pressed &&
            mouseButton.ButtonIndex == MouseButton.Left &&
            _equipment?.GetSlot(_slotIndex)?.Data.IsQuickUsable == true
        )
        {
            UseRequested?.Invoke(_slotIndex);
            AcceptEvent();
            return;
        }

        if (
            inputEvent is InputEventMouseButton mouseButtonRight &&
            mouseButtonRight.Pressed &&
            mouseButtonRight.ButtonIndex == MouseButton.Right
        )
        {
            _equipment?.TryUnequip(_slotIndex, _inventory);
            AcceptEvent();
        }
    }
}

public partial class EquipmentBarView : HBoxContainer
{
    private EquipmentModel _equipment;
    private InventoryModel _inventory;

    public event Action<string, int> DropRequested;
    public event Action<int> UseRequested;

    public void Bind(EquipmentModel equipment, InventoryModel inventory)
    {
        _equipment = equipment;
        _inventory = inventory;
    }

    public override void _Ready()
    {
        AddThemeConstantOverride("separation", 3);

        int slotCount = Mathf.Max(_equipment?.SlotCount ?? 8, 1);
        for (int slotIndex = 0; slotIndex < slotCount; slotIndex++)
        {
            EquipmentSlotView slot = new();
            slot.Bind(_equipment, _inventory, slotIndex);
            slot.DropRequested += (entryId, index) =>
                DropRequested?.Invoke(entryId, index);
            slot.UseRequested += index => UseRequested?.Invoke(index);
            AddChild(slot);
        }
    }
}
