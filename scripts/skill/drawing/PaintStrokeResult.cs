using Godot;
using System;
using System.Collections.Generic;

/// <summary>
/// 一次已经完成的绘制结果。
/// 所有轨迹点均使用世界坐标。
/// </summary>
public sealed class PaintStrokeResult
{
    private readonly Vector2[] _globalPoints;

    public IReadOnlyList<Vector2> GlobalPoints =>
        _globalPoints;

    public int PointCount => _globalPoints.Length;

    public float TotalLength { get; }

    public float Duration { get; }

    public float AverageSpeed =>
        Duration > 0.0001f
            ? TotalLength / Duration
            : 0.0f;

    public Rect2 GlobalBounds { get; }

    public PaintBrushData BrushData { get; }

    public Vector2 StartPoint =>
        PointCount > 0
            ? _globalPoints[0]
            : Vector2.Zero;

    public Vector2 EndPoint =>
        PointCount > 0
            ? _globalPoints[^1]
            : Vector2.Zero;

    public PaintStrokeResult(
        IReadOnlyList<Vector2> globalPoints,
        float duration,
        PaintBrushData brushData
    )
    {
        if (globalPoints == null)
            throw new ArgumentNullException(nameof(globalPoints));

        BrushData = brushData ??
            throw new ArgumentNullException(nameof(brushData));

        Duration = Mathf.Max(0.0f, duration);

        _globalPoints =
            new Vector2[globalPoints.Count];

        for (int i = 0; i < globalPoints.Count; i++)
            _globalPoints[i] = globalPoints[i];

        TotalLength =
            CalculateTotalLength(_globalPoints);

        GlobalBounds =
            CalculateBounds(_globalPoints);
    }

    public Vector2[] GetPointsCopy()
    {
        return (Vector2[])_globalPoints.Clone();
    }

    private static float CalculateTotalLength(
        IReadOnlyList<Vector2> points
    )
    {
        float length = 0.0f;

        for (int i = 1; i < points.Count; i++)
        {
            length += points[i - 1]
                .DistanceTo(points[i]);
        }

        return length;
    }

    private static Rect2 CalculateBounds(
        IReadOnlyList<Vector2> points
    )
    {
        if (points.Count == 0)
            return new Rect2();

        Vector2 minimum = points[0];
        Vector2 maximum = points[0];

        for (int i = 1; i < points.Count; i++)
        {
            Vector2 point = points[i];

            minimum.X = Mathf.Min(minimum.X, point.X);
            minimum.Y = Mathf.Min(minimum.Y, point.Y);

            maximum.X = Mathf.Max(maximum.X, point.X);
            maximum.Y = Mathf.Max(maximum.Y, point.Y);
        }

        return new Rect2(
            minimum,
            maximum - minimum
        );
    }
}