using Godot;

/// <summary>Reusable spirit ink coin counter for the HUD and inventory.</summary>
public partial class InkCoinCounterView : Control
{
    private const string IconPath =
        "res://assets/icons/ink_coins_icon.png";

    private InkCoinWallet _wallet;
    private Label _amountLabel;

    public string DisplayedBalanceText => _amountLabel?.Text ?? string.Empty;

    public void Bind(InkCoinWallet wallet)
    {
        _wallet = wallet;
    }

    public override void _Ready()
    {
        CustomMinimumSize = new Vector2(128, 30);
        MouseFilter = MouseFilterEnum.Ignore;
        TextureFilter = TextureFilterEnum.Nearest;

        PanelContainer panel = new()
        {
            MouseFilter = MouseFilterEnum.Ignore
        };
        panel.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        panel.AddThemeStyleboxOverride(
            "panel",
            UiPalette.MakePanel(
                new Color(UiPalette.Panel, 0.94f),
                UiPalette.ChargeGold,
                1
            )
        );
        AddChild(panel);

        HBoxContainer content = new()
        {
            Alignment = BoxContainer.AlignmentMode.Center,
            MouseFilter = MouseFilterEnum.Ignore
        };
        content.AddThemeConstantOverride("separation", 5);
        panel.AddChild(content);

        TextureRect icon = new()
        {
            Texture = GD.Load<Texture2D>(IconPath),
            CustomMinimumSize = new Vector2(20, 20),
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
            MouseFilter = MouseFilterEnum.Ignore,
            TextureFilter = TextureFilterEnum.Nearest
        };
        content.AddChild(icon);

        Label nameLabel = new()
        {
            Text = "灵墨币",
            MouseFilter = MouseFilterEnum.Ignore
        };
        UiPalette.StyleLabel(nameLabel, 11);
        nameLabel.AddThemeColorOverride(
            "font_color",
            UiPalette.ChargeGold.Lightened(0.25f)
        );
        content.AddChild(nameLabel);

        _amountLabel = new Label
        {
            Name = "AmountLabel",
            HorizontalAlignment = HorizontalAlignment.Right,
            CustomMinimumSize = new Vector2(34, 0),
            MouseFilter = MouseFilterEnum.Ignore
        };
        UiPalette.StyleLabel(_amountLabel, 12);
        content.AddChild(_amountLabel);

        if (_wallet != null)
            _wallet.BalanceChanged += OnBalanceChanged;

        Refresh();
    }

    private void OnBalanceChanged(int balance, int delta)
    {
        Refresh();
    }

    private void Refresh()
    {
        if (_amountLabel != null)
            _amountLabel.Text = (_wallet?.Balance ?? 0).ToString();
    }

    public override void _ExitTree()
    {
        if (_wallet != null)
            _wallet.BalanceChanged -= OnBalanceChanged;
    }
}
