using Godot;
using System.Collections.Generic;

[GlobalClass]
public partial class InventoryModel : Node
{
    [Signal]
    public delegate void InventoryChangedEventHandler();

    [Signal]
    public delegate void FeedbackRequestedEventHandler(string message);

    [Export(PropertyHint.Range, "1,20,1")]
    public int Columns { get; set; } = 8;

    [Export(PropertyHint.Range, "1,20,1")]
    public int Rows { get; set; } = 6;

    [Export(PropertyHint.Range, "1,1000,0.5")]
    public float MaxWeight { get; set; } = 30.0f;

    private readonly List<InventoryEntry> _entries = new();

    public IReadOnlyList<InventoryEntry> Entries => _entries;

    public float CurrentWeight
    {
        get
        {
            float total = 0.0f;
            foreach (InventoryEntry entry in _entries)
                total += entry.TotalWeight;
            return total;
        }
    }

    public bool TryAddWeapon(
        WeaponInstance weapon,
        out InventoryWeaponEntry entry
    )
    {
        entry = new InventoryWeaponEntry(weapon);
        return TryAddExisting(entry);
    }

    public bool TryAddItem(InventoryItemData data, int quantity = 1)
    {
        if (data == null || quantity <= 0)
            return false;

        int remaining = quantity;

        if (data.MaxStack > 1)
        {
            foreach (InventoryEntry entry in _entries)
            {
                if (entry.IsWeapon || entry.Data.ItemId != data.ItemId)
                    continue;

                int available = Mathf.Max(data.MaxStack - entry.Quantity, 0);
                int transferred = Mathf.Min(available, remaining);
                entry.Quantity += transferred;
                remaining -= transferred;

                if (remaining <= 0)
                {
                    EmitSignal(SignalName.InventoryChanged);
                    return true;
                }
            }
        }

        while (remaining > 0)
        {
            int stackSize = Mathf.Min(Mathf.Max(data.MaxStack, 1), remaining);
            InventoryEntry entry = new(data, stackSize);

            if (!TryAddExisting(entry))
                return false;

            remaining -= stackSize;
        }

        return true;
    }

    public bool TryAddExisting(InventoryEntry entry)
    {
        if (entry == null || _entries.Contains(entry))
            return false;

        if (CurrentWeight + entry.TotalWeight > MaxWeight + 0.001f)
        {
            EmitSignal(SignalName.FeedbackRequested, "负重超限");
            return false;
        }

        if (!TryFindFreePosition(entry, out Vector2I position))
        {
            EmitSignal(SignalName.FeedbackRequested, "背包空间不足");
            return false;
        }

        entry.GridPosition = position;
        _entries.Add(entry);
        EmitSignal(SignalName.InventoryChanged);
        return true;
    }

    public bool RemoveEntry(InventoryEntry entry, bool emitSignal = true)
    {
        if (entry == null || !_entries.Remove(entry))
            return false;

        entry.GridPosition = new Vector2I(-1, -1);
        if (emitSignal)
            EmitSignal(SignalName.InventoryChanged);
        return true;
    }

    public bool TryMoveEntry(InventoryEntry entry, Vector2I position)
    {
        if (!_entries.Contains(entry) || !CanOccupy(entry, position, entry))
            return false;

        entry.GridPosition = position;
        EmitSignal(SignalName.InventoryChanged);
        return true;
    }

    public InventoryEntry FindById(string entryId)
    {
        foreach (InventoryEntry entry in _entries)
        {
            if (entry.EntryId == entryId)
                return entry;
        }

        return null;
    }

    public bool Contains(InventoryEntry entry) => _entries.Contains(entry);

    private bool TryFindFreePosition(
        InventoryEntry entry,
        out Vector2I position
    )
    {
        for (int y = 0; y < Rows; y++)
        {
            for (int x = 0; x < Columns; x++)
            {
                Vector2I candidate = new(x, y);
                if (!CanOccupy(entry, candidate))
                    continue;

                position = candidate;
                return true;
            }
        }

        position = new Vector2I(-1, -1);
        return false;
    }

    private bool CanOccupy(
        InventoryEntry entry,
        Vector2I position,
        InventoryEntry ignoredEntry = null
    )
    {
        Vector2I size = entry.OccupiedSize;

        if (
            position.X < 0 ||
            position.Y < 0 ||
            position.X + size.X > Columns ||
            position.Y + size.Y > Rows
        )
        {
            return false;
        }

        Rect2I candidate = new(position, size);

        foreach (InventoryEntry existing in _entries)
        {
            if (existing == ignoredEntry)
                continue;

            Rect2I occupied = new(
                existing.GridPosition,
                existing.OccupiedSize
            );

            if (candidate.Intersects(occupied))
                return false;
        }

        return true;
    }
}
