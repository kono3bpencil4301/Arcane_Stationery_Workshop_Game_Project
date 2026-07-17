using Godot;

public static class UiPalette
{
    public static readonly Color Panel = new("11162f");
    public static readonly Color PanelSoft = new("1b2344");
    public static readonly Color ArchiveBlue = new("0d3c70");
    public static readonly Color ArcaneViolet = new("7a1b77");
    public static readonly Color HealthMagenta = new("d21e78");
    public static readonly Color WarningCoral = new("f97042");
    public static readonly Color ChargeGold = new("c9a40e");
    public static readonly Color RingBlue = new("5b6ee1");
    public static readonly Color Text = new("f4f1dc");
    public static readonly Color Muted = new("8f99b8");

    public static Font PixelFont =>
        GD.Load<Font>(
            "res://assets/fonts/fusion-pixel-12px-monospaced-zh_hans.otf"
        );

    public static StyleBoxFlat MakePanel(
        Color background,
        Color border,
        int borderWidth = 1
    )
    {
        return new StyleBoxFlat
        {
            BgColor = background,
            BorderColor = border,
            BorderWidthLeft = borderWidth,
            BorderWidthTop = borderWidth,
            BorderWidthRight = borderWidth,
            BorderWidthBottom = borderWidth,
            ContentMarginLeft = 6,
            ContentMarginTop = 4,
            ContentMarginRight = 6,
            ContentMarginBottom = 4
        };
    }

    public static void StyleLabel(Label label, int fontSize = 12)
    {
        label.AddThemeFontOverride("font", PixelFont);
        label.AddThemeFontSizeOverride("font_size", fontSize);
        label.AddThemeColorOverride("font_color", Text);
        label.AddThemeColorOverride("font_outline_color", Panel);
        label.AddThemeConstantOverride("outline_size", 1);
    }

    public static void StyleButton(Button button, int fontSize = 12)
    {
        button.AddThemeFontOverride("font", PixelFont);
        button.AddThemeFontSizeOverride("font_size", fontSize);
        button.AddThemeColorOverride("font_color", Text);
        button.AddThemeColorOverride("font_hover_color", Colors.White);
        button.AddThemeStyleboxOverride(
            "normal",
            MakePanel(PanelSoft, ArchiveBlue)
        );
        button.AddThemeStyleboxOverride(
            "hover",
            MakePanel(PanelSoft.Lightened(0.08f), WarningCoral)
        );
        button.AddThemeStyleboxOverride(
            "pressed",
            MakePanel(ArchiveBlue, ChargeGold)
        );
        button.AddThemeStyleboxOverride(
            "disabled",
            MakePanel(Panel, new Color(0.2f, 0.24f, 0.38f, 1.0f))
        );
    }
}
