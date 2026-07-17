using Godot;
using System.Globalization;

/// <summary>
/// 世界空间中的伤害数字。节点会挂到当前场景根节点，避免受击对象死亡释放时
/// 提前中断最后一次上飘动画。
/// </summary>
public partial class FloatingDamageNumber : Label
{
    public static void Spawn(
        Node2D target,
        float damage,
        Color color,
        Vector2 offset,
        float riseDistance,
        float duration,
        int fontSize
    )
    {
        if (
            !GodotObject.IsInstanceValid(target) ||
            !target.IsInsideTree() ||
            damage <= 0.0f
        )
        {
            return;
        }

        Node parent = target.GetTree().CurrentScene ?? target.GetParent();

        if (parent == null)
            return;

        float safeDuration = Mathf.Max(duration, 0.05f);
        Vector2 labelSize = new(80.0f, 28.0f);
        FloatingDamageNumber label = new()
        {
            Text = $"-{FormatDamage(damage)}",
            Size = labelSize,
            CustomMinimumSize = labelSize,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            MouseFilter = MouseFilterEnum.Ignore,
            ZIndex = 100,
            ZAsRelative = false,
            ProcessMode = ProcessModeEnum.Always
        };

        label.AddThemeFontOverride("font", UiPalette.PixelFont);
        label.AddThemeColorOverride("font_color", color);
        label.AddThemeColorOverride("font_outline_color", Colors.Black);
        label.AddThemeConstantOverride("outline_size", 3);
        label.AddThemeFontSizeOverride(
            "font_size",
            Mathf.Clamp(fontSize, 8, 64)
        );

        parent.AddChild(label);
        label.GlobalPosition =
            target.GlobalPosition + offset - labelSize * 0.5f;

        Tween tween = label.CreateTween();
        tween.SetParallel(true);
        tween.SetIgnoreTimeScale(true);
        tween.TweenProperty(
                label,
                "position:y",
                label.Position.Y - Mathf.Max(riseDistance, 0.0f),
                safeDuration
            )
            .SetTrans(Tween.TransitionType.Quad)
            .SetEase(Tween.EaseType.Out);
        tween.TweenProperty(
                label,
                "modulate:a",
                0.0f,
                safeDuration * 0.6f
            )
            .SetDelay(safeDuration * 0.4f);
        tween.Finished += label.QueueFree;
    }

    private static string FormatDamage(float damage)
    {
        return damage.ToString("0.#", CultureInfo.InvariantCulture);
    }
}
