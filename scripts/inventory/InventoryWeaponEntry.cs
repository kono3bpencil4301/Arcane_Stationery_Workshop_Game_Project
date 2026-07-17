using Godot;

public sealed class InventoryWeaponEntry : InventoryEntry
{
    public WeaponInstance Weapon { get; }
    public override bool IsWeapon => true;

    public InventoryWeaponEntry(WeaponInstance weapon)
        : base(CreateItemData(weapon), 1)
    {
        Weapon = weapon;
    }

    private static InventoryItemData CreateItemData(WeaponInstance weapon)
    {
        WeaponData weaponData = weapon.Data;

        return new InventoryItemData
        {
            ItemId = string.IsNullOrWhiteSpace(weaponData.WeaponId)
                ? weaponData.DisplayName
                : weaponData.WeaponId,
            DisplayName = weaponData.DisplayName,
            Icon = weaponData.Icon,
            Category = InventoryItemCategory.Weapon,
            GridSize = new Vector2I(2, 1),
            Weight = Mathf.Max(weaponData.Weight, 0.0f),
            IsEquipable = true,
            MaxStack = 1
        };
    }
}
