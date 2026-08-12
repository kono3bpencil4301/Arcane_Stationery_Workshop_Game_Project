using Godot;

/// <summary>
/// Pixel health bar whose remaining fill stays pinned to the left edge, so
/// damage retracts the right edge toward the left.
/// </summary>
public partial class EnemyHealthBar : Node2D
{
    public static readonly Color LowHealthColor = new("e43b44");
    public static readonly Color MediumHealthColor = new("f6c445");
    public static readonly Color HighHealthColor = new("55c96b");
    public static readonly Color EmptyHealthColor = new("171923");

    public float BarLength { get; private set; } = 32.0f;
    public float BarThickness { get; private set; } = 4.0f;
    public float HealthRatio { get; private set; } = 1.0f;
    public Color FillColor { get; private set; } = HighHealthColor;

    /// <summary>Current left-anchored fill rectangle, exposed for smoke tests.</summary>
    public Rect2 FillRect => new(
        -BarLength * 0.5f,
        -BarThickness * 0.5f,
        BarLength * HealthRatio,
        BarThickness
    );

    public void Configure(float length, float thickness)
    {
        BarLength = Mathf.Max(length, 1.0f);
        BarThickness = Mathf.Max(thickness, 1.0f);
        QueueRedraw();
    }

    public void SetHealth(float current, float maximum)
    {
        HealthRatio = maximum <= 0.0f
            ? 0.0f
            : Mathf.Clamp(current / maximum, 0.0f, 1.0f);
        FillColor = ResolveFillColor(HealthRatio);
        QueueRedraw();
    }

    public override void _Draw()
    {
        Rect2 track = new(
            -BarLength * 0.5f,
            -BarThickness * 0.5f,
            BarLength,
            BarThickness
        );
        DrawRect(track, EmptyHealthColor, true);

        if (HealthRatio > 0.0f)
            DrawRect(FillRect, FillColor, true);
    }

    private static Color ResolveFillColor(float ratio)
    {
        if (ratio < 0.3f)
            return LowHealthColor;

        if (ratio < 0.7f)
            return MediumHealthColor;

        return HighHealthColor;
    }
}
