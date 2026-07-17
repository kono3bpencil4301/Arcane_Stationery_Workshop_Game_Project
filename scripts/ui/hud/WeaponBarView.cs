using Godot;
using System;

public partial class WeaponBarView : HBoxContainer
{
    private WeaponManager _manager;

    public event Action<string, int> DropRequested;

    public void Bind(WeaponManager manager)
    {
        _manager = manager;
    }

    public override void _Ready()
    {
        AddThemeConstantOverride("separation", 5);
        MouseFilter = MouseFilterEnum.Pass;

        for (int slotIndex = 0; slotIndex < 3; slotIndex++)
        {
            WeaponSlotView slot = new();
            slot.Bind(_manager, slotIndex);
            slot.DropRequested += (entryId, index) =>
                DropRequested?.Invoke(entryId, index);
            AddChild(slot);
        }
    }
}
