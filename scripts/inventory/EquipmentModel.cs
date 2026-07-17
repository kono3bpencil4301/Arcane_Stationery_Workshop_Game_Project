using Godot;

[GlobalClass]
public partial class EquipmentModel : Node
{
    [Signal]
    public delegate void EquipmentChangedEventHandler(int slotIndex);

    [Export(PropertyHint.Range, "1,16,1")]
    public int SlotCount { get; set; } = 8;

    private InventoryEntry[] _slots;

    public override void _Ready()
    {
        _slots = new InventoryEntry[Mathf.Max(SlotCount, 1)];
    }

    public InventoryEntry GetSlot(int slotIndex)
    {
        return IsValidSlot(slotIndex) ? _slots[slotIndex] : null;
    }

    public bool TryEquip(
        InventoryEntry entry,
        int slotIndex,
        InventoryModel inventory
    )
    {
        if (
            !IsValidSlot(slotIndex) ||
            entry == null ||
            inventory == null ||
            entry.IsWeapon ||
            (!entry.Data.IsEquipable && !entry.Data.IsQuickUsable) ||
            !inventory.Contains(entry)
        )
        {
            return false;
        }

        InventoryEntry previous = _slots[slotIndex];
        inventory.RemoveEntry(entry, false);

        if (previous != null && !inventory.TryAddExisting(previous))
        {
            inventory.TryAddExisting(entry);
            return false;
        }

        _slots[slotIndex] = entry;
        EmitSignal(SignalName.EquipmentChanged, slotIndex);
        return true;
    }

    public bool TryUnequip(int slotIndex, InventoryModel inventory)
    {
        if (!IsValidSlot(slotIndex) || inventory == null)
            return false;

        InventoryEntry entry = _slots[slotIndex];
        if (entry == null || !inventory.TryAddExisting(entry))
            return false;

        _slots[slotIndex] = null;
        EmitSignal(SignalName.EquipmentChanged, slotIndex);
        return true;
    }

    private bool IsValidSlot(int slotIndex)
    {
        return _slots != null && slotIndex >= 0 && slotIndex < _slots.Length;
    }
}
