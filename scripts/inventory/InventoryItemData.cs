using Godot;

public enum InventoryItemCategory
{
    Material,
    Equipment,
    Consumable,
    Weapon
}

[GlobalClass]
public partial class InventoryItemData : Resource
{
    [Export] public string ItemId { get; set; } = "";
    [Export] public string DisplayName { get; set; } = "";
    [Export] public Texture2D Icon { get; set; }
    [Export] public InventoryItemCategory Category { get; set; }
    [Export] public Vector2I GridSize { get; set; } = Vector2I.One;
    [Export(PropertyHint.Range, "0,1000,0.1")]
    public float Weight { get; set; } = 1.0f;
    [Export(PropertyHint.Range, "0,1000000,1")]
    public int Value { get; set; }
    [Export] public bool IsPolluted { get; set; }
    [Export] public bool IsEquipable { get; set; }
    [Export] public bool IsQuickUsable { get; set; }
    [Export(PropertyHint.Range, "1,999,1")]
    public int MaxStack { get; set; } = 1;
}
