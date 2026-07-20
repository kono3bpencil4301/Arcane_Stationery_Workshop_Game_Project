using Godot;
using System;

public partial class InventoryItemView : Button
{
    private Label _quantityLabel;

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
        CreateQuantityLabel();
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
            SetQuantityLabel(0, false);
            Disabled = false;
            TooltipText = "空格";
            return;
        }

        if (!isTopLeft)
        {
            Text = "·";
            Icon = null;
            SetQuantityLabel(0, false);
            Disabled = true;
            TooltipText = entry.Data.DisplayName;
            return;
        }

        Disabled = false;
        Icon = entry.Data.Icon;
        Text = entry.Data.Icon == null
            ? entry.Data.DisplayName
            : "";
        SetQuantityLabel(entry.Quantity, true);
        TooltipText =
            $"{entry.Data.DisplayName}\n重量 {entry.TotalWeight:0.0} kg";
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
