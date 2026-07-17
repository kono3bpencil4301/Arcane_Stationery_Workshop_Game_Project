using Godot;
using System;

public partial class InventoryItemView : Button
{
    public InventoryEntry Entry { get; private set; }
    public Vector2I CellPosition { get; set; }

    public event Action<InventoryEntry> Selected;
    public event Action<InventoryEntry, Vector2> ContextRequested;
    public event Action<string, Vector2I> MoveRequested;

    public override void _Ready()
    {
        CustomMinimumSize = new Vector2(42, 42);
        FocusMode = FocusModeEnum.None;
        ClipText = true;
        ExpandIcon = true;
        AddThemeConstantOverride("icon_max_width", 30);
        UiPalette.StyleButton(this, 10);
        Pressed += () =>
        {
            if (Entry != null)
                Selected?.Invoke(Entry);
        };
    }

    public void SetEntry(InventoryEntry entry, bool isTopLeft = true)
    {
        Entry = entry;

        if (entry == null)
        {
            Text = "";
            Icon = null;
            Disabled = false;
            TooltipText = "空格";
            return;
        }

        if (!isTopLeft)
        {
            Text = "·";
            Icon = null;
            Disabled = true;
            TooltipText = entry.Data.DisplayName;
            return;
        }

        Disabled = false;
        Icon = entry.Data.Icon;
        Text = entry.Data.Icon == null
            ? entry.Data.DisplayName
            : entry.Quantity > 1
                ? entry.Quantity.ToString()
                : "";
        TooltipText = $"{entry.Data.DisplayName}\n重量 {entry.TotalWeight:0.0}";
    }

    public override Variant _GetDragData(Vector2 atPosition)
    {
        if (Entry == null || Disabled)
            return default;

        Label preview = new()
        {
            Text = Entry.Data.DisplayName,
            CustomMinimumSize = new Vector2(92, 26),
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };
        UiPalette.StyleLabel(preview, 11);
        preview.AddThemeStyleboxOverride(
            "normal",
            UiPalette.MakePanel(UiPalette.PanelSoft, UiPalette.WarningCoral)
        );
        SetDragPreview(preview);
        return Variant.From(Entry.EntryId);
    }

    public override bool _CanDropData(Vector2 atPosition, Variant data)
    {
        return data.VariantType == Variant.Type.String;
    }

    public override void _DropData(Vector2 atPosition, Variant data)
    {
        MoveRequested?.Invoke(data.AsString(), CellPosition);
    }

    public override void _GuiInput(InputEvent inputEvent)
    {
        if (
            inputEvent is InputEventMouseButton mouseButton &&
            mouseButton.Pressed &&
            mouseButton.ButtonIndex == MouseButton.Right &&
            Entry != null
        )
        {
            ContextRequested?.Invoke(Entry, GetGlobalMousePosition());
            AcceptEvent();
        }
    }
}
