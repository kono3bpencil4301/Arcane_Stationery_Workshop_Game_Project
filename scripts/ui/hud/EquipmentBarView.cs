using Godot;
using System;

public partial class EquipmentSlotView : Button
{
    private EquipmentModel _equipment;
    private InventoryModel _inventory;
    private int _slotIndex;

    public event Action<string, int> DropRequested;

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
        GuiInput += OnGuiInput;
    }

    public override void _Process(double delta)
    {
        InventoryEntry entry = _equipment?.GetSlot(_slotIndex);
        Icon = entry?.Data.Icon;
        Text = entry == null
            ? (_slotIndex + 1).ToString()
            : entry.Data.Icon == null
                ? entry.Data.DisplayName
                : entry.Quantity > 1
                    ? entry.Quantity.ToString()
                    : "";
        TooltipText = entry == null
            ? $"装备栏 {_slotIndex + 1}（空）"
            : $"{entry.Data.DisplayName}\n右键卸下";
    }

    public override bool _CanDropData(Vector2 atPosition, Variant data)
    {
        return data.VariantType == Variant.Type.String;
    }

    public override void _DropData(Vector2 atPosition, Variant data)
    {
        DropRequested?.Invoke(data.AsString(), _slotIndex);
    }

    private void OnGuiInput(InputEvent inputEvent)
    {
        if (
            inputEvent is InputEventMouseButton mouseButton &&
            mouseButton.Pressed &&
            mouseButton.ButtonIndex == MouseButton.Right
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
            AddChild(slot);
        }
    }
}
