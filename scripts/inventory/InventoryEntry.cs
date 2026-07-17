using Godot;
using System;

public class InventoryEntry
{
    public string EntryId { get; } = Guid.NewGuid().ToString("N");
    public InventoryItemData Data { get; }
    public int Quantity { get; set; }
    public Vector2I GridPosition { get; set; } = new(-1, -1);
    public bool Rotated { get; set; }

    public virtual bool IsWeapon => false;

    public Vector2I OccupiedSize =>
        Rotated
            ? new Vector2I(Data.GridSize.Y, Data.GridSize.X)
            : Data.GridSize;

    public float TotalWeight =>
        Mathf.Max(Data.Weight, 0.0f) * Mathf.Max(Quantity, 0);

    public InventoryEntry(InventoryItemData data, int quantity = 1)
    {
        Data = data ?? throw new ArgumentNullException(nameof(data));
        Quantity = Mathf.Max(quantity, 1);
    }
}
