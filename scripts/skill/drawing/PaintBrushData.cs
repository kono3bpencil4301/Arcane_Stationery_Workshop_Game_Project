using Godot;

/// <summary>
/// 一种可配置的绘画笔刷。
///
/// 建议为不同武器建立独立的 .tres：
/// ChalkBrushData.tres
/// MarkerBrushData.tres
/// BrushBrushData.tres
/// EraserBrushData.tres
/// </summary>
[GlobalClass]
public partial class PaintBrushData : Resource
{
    [ExportGroup("Brush")]

    /// <summary>
    /// 自定义 alpha 笔刷贴图。
    /// 推荐使用白色 RGB + 透明度形状。
    /// 不设置时自动生成圆形软笔刷。
    /// </summary>
    [Export]
    public Texture2D BrushTexture { get; set; }

    [Export]
    public Color BrushColor { get; set; } = Colors.White;

    [Export(PropertyHint.Range, "1,128,1")]
    public int BrushSize { get; set; } = 24;

    [Export(PropertyHint.Range, "0,1,0.01")]
    public float Opacity { get; set; } = 1.0f;

    /// <summary>
    /// 两次笔刷盖章之间的距离。
    ///
    /// 实际间距 = BrushSize × SpacingRatio。
    /// 0.25 表示笔刷直径的四分之一。
    /// </summary>
    [Export(PropertyHint.Range, "0.05,1,0.05")]
    public float SpacingRatio { get; set; } = 0.25f;

    [Export]
    public PaintBlendMode BlendMode { get; set; } =
        PaintBlendMode.Paint;

    [ExportGroup("Stroke")]

    [Export(PropertyHint.Range, "16,4096,1")]
    public float MaxStrokeLength { get; set; } = 600.0f;

    [Export(PropertyHint.Range, "0,256,1")]
    public float MinValidLength { get; set; } = 16.0f;

    [Export(PropertyHint.Range, "0.1,10,0.1")]
    public float MaxDrawDuration { get; set; } = 3.0f;

    /// <summary>
    /// 鼠标移动达到该距离后，才提交一个新的逻辑轨迹点。
    /// 笔刷之间的视觉插值仍由 SpacingRatio 决定。
    /// </summary>
    [Export(PropertyHint.Range, "1,64,1")]
    public float MinSampleDistance { get; set; } = 3.0f;

    [ExportGroup("Time")]

    [Export(PropertyHint.Range, "0.05,1,0.05")]
    public float SlowMotionScale { get; set; } = 0.25f;

    [ExportGroup("Fade")]

    [Export]
    public bool FadeEnabled { get; set; } = true;

    [Export(PropertyHint.Range, "0,30,0.1")]
    public float FadeDelay { get; set; } = 0.5f;

    [Export(PropertyHint.Range, "0.1,30,0.1")]
    public float FadeDuration { get; set; } = 2.5f;
}
