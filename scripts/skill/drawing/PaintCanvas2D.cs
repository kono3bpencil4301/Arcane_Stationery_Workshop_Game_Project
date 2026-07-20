using Godot;
using System.Collections.Generic;

/// <summary>
/// 可被鼠标笔刷直接修改的 2D 像素画布。
///
/// 推荐作为房间节点的子节点。
/// Sprite2D 的位置应位于房间左上角。
/// </summary>
public partial class PaintCanvas2D : Sprite2D
{
    [ExportGroup("Canvas")]

    [Export(PropertyHint.Range, "64,4096,1")]
    private int _canvasWidth = 1024;

    [Export(PropertyHint.Range, "64,4096,1")]
    private int _canvasHeight = 1024;

    private Image _canvasImage;
    private ImageTexture _canvasTexture;

    private Image _cachedBrushImage;
    private PaintBrushData _cachedBrushData;
    private Texture2D _cachedBrushTexture;

    private int _cachedBrushSize;
    private Color _cachedBrushColor;
    private float _cachedBrushOpacity;

    private bool _textureDirty;

    private readonly Dictionary<int, FadePixelState> _fadingPixels =
        new();
    private readonly List<int> _fadePixelKeys =
        new();

    private struct FadePixelState
    {
        public Color BaseColor;
        public float DelayRemaining;
        public float Elapsed;
        public float Duration;
    }

    public Vector2I CanvasSize =>
        new(_canvasWidth, _canvasHeight);

    /// <summary>
    /// 根据当前房间的可绘制区域更新画布尺寸与世界坐标左上角。
    /// 如果画布已经创建，则立即清空并按新尺寸重建。
    /// </summary>
    public void ConfigureCanvas(
        Vector2I canvasSize,
        Vector2 globalTopLeft
    )
    {
        _canvasWidth = Mathf.Max(canvasSize.X, 1);
        _canvasHeight = Mathf.Max(canvasSize.Y, 1);
        GlobalPosition = globalTopLeft;

        if (_canvasImage == null)
            return;

        _fadingPixels.Clear();
        _fadePixelKeys.Clear();
        _textureDirty = false;
        CreateCanvas();
    }

    public override void _Ready()
    {
        Centered = false;

        TextureFilter =
            CanvasItem.TextureFilterEnum.Nearest;

        CreateCanvas();
    }

    public override void _Process(double delta)
    {
        UpdateFadingPixels((float)delta);

        /*
         * 一帧中可能盖几十次笔刷，
         * 但只把 Image 上传给 GPU 一次。
         */
        if (!_textureDirty)
            return;

        _canvasTexture.Update(_canvasImage);
        _textureDirty = false;
    }

    private void CreateCanvas()
    {
        _canvasImage = Image.CreateEmpty(
            _canvasWidth,
            _canvasHeight,
            false,
            Image.Format.Rgba8
        );

        _canvasImage.Fill(Colors.Transparent);

        _canvasTexture =
            ImageTexture.CreateFromImage(_canvasImage);

        Texture = _canvasTexture;
    }

    /// <summary>
    /// 清空整张画布。
    /// </summary>
    public void ClearCanvas()
    {
        _canvasImage.Fill(Colors.Transparent);
        _fadingPixels.Clear();
        _fadePixelKeys.Clear();
        _textureDirty = true;
    }

    /// <summary>
    /// 判断一个世界坐标是否位于画布中。
    /// </summary>
    public bool ContainsGlobalPoint(
        Vector2 globalPoint
    )
    {
        Vector2 localPoint =
            ToLocal(globalPoint);

        return localPoint.X >= 0.0f &&
               localPoint.Y >= 0.0f &&
               localPoint.X < _canvasWidth &&
               localPoint.Y < _canvasHeight;
    }

    /// <summary>
    /// 将世界坐标限制在画布范围内。
    /// </summary>
    public Vector2 ClampGlobalPoint(
        Vector2 globalPoint
    )
    {
        Vector2 localPoint =
            ToLocal(globalPoint);

        localPoint.X = Mathf.Clamp(
            localPoint.X,
            0.0f,
            _canvasWidth - 1.0f
        );

        localPoint.Y = Mathf.Clamp(
            localPoint.Y,
            0.0f,
            _canvasHeight - 1.0f
        );

        return ToGlobal(localPoint);
    }

    /// <summary>
    /// 在指定世界坐标盖一次笔刷。
    /// </summary>
    public void StampAtGlobal(
        Vector2 globalPoint,
        PaintBrushData brushData
    )
    {
        if (brushData == null)
            return;

        EnsureBrushCache(brushData);

        if (_cachedBrushImage == null)
            return;

        Vector2 localPoint =
            ToLocal(globalPoint);

        Vector2I imageCenter = new(
            Mathf.RoundToInt(localPoint.X),
            Mathf.RoundToInt(localPoint.Y)
        );

        Vector2I brushSize =
            _cachedBrushImage.GetSize();

        Vector2I destination = new(
            imageCenter.X - brushSize.X / 2,
            imageCenter.Y - brushSize.Y / 2
        );

        ApplyBrush(
            destination,
            brushData
        );
    }

    private void EnsureBrushCache(
        PaintBrushData brushData
    )
    {
        bool cacheIsValid =
            _cachedBrushImage != null &&
            _cachedBrushData == brushData &&
            _cachedBrushTexture == brushData.BrushTexture &&
            _cachedBrushSize == brushData.BrushSize &&
            _cachedBrushColor == brushData.BrushColor &&
            Mathf.IsEqualApprox(
                _cachedBrushOpacity,
                brushData.Opacity
            );

        if (cacheIsValid)
            return;

        _cachedBrushData = brushData;
        _cachedBrushTexture =
            brushData.BrushTexture;

        _cachedBrushSize =
            Mathf.Max(1, brushData.BrushSize);

        _cachedBrushColor =
            brushData.BrushColor;

        _cachedBrushOpacity =
            Mathf.Clamp(brushData.Opacity, 0.0f, 1.0f);

        _cachedBrushImage =
            BuildBrushImage(brushData);
    }

    private Image BuildBrushImage(
        PaintBrushData brushData
    )
    {
        int targetSize =
            Mathf.Max(1, brushData.BrushSize);

        Image brushImage;

        if (brushData.BrushTexture != null)
        {
            Image source =
                brushData.BrushTexture.GetImage();

            if (source != null && !source.IsEmpty())
            {
                brushImage = source.GetRegion(
                    new Rect2I(
                        Vector2I.Zero,
                        source.GetSize()
                    )
                );

                brushImage.Convert(
                    Image.Format.Rgba8
                );

                if (
                    brushImage.GetWidth() != targetSize ||
                    brushImage.GetHeight() != targetSize
                )
                {
                    brushImage.Resize(
                        targetSize,
                        targetSize,
                        Image.Interpolation.Lanczos
                    );
                }
            }
            else
            {
                brushImage =
                    GenerateCircleBrush(targetSize);
            }
        }
        else
        {
            brushImage =
                GenerateCircleBrush(targetSize);
        }

        TintBrushImage(
            brushImage,
            brushData.BrushColor,
            brushData.Opacity
        );

        return brushImage;
    }

    /// <summary>
    /// 没有自定义贴图时，生成圆形软边笔刷。
    /// </summary>
    private static Image GenerateCircleBrush(
        int size
    )
    {
        Image image = Image.CreateEmpty(
            size,
            size,
            false,
            Image.Format.Rgba8
        );

        image.Fill(Colors.Transparent);

        float center =
            (size - 1) * 0.5f;

        float radius =
            size * 0.5f;

        float feather =
            Mathf.Max(1.0f, size * 0.15f);

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                Vector2 difference = new(
                    x - center,
                    y - center
                );

                float distance =
                    difference.Length();

                float alpha = Mathf.Clamp(
                    (radius - distance) / feather,
                    0.0f,
                    1.0f
                );

                image.SetPixel(
                    x,
                    y,
                    new Color(1.0f, 1.0f, 1.0f, alpha)
                );
            }
        }

        return image;
    }

    private static void TintBrushImage(
        Image image,
        Color tint,
        float opacity
    )
    {
        int width = image.GetWidth();
        int height = image.GetHeight();

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                Color source =
                    image.GetPixel(x, y);

                Color result = new(
                    source.R * tint.R,
                    source.G * tint.G,
                    source.B * tint.B,
                    source.A * tint.A * opacity
                );

                image.SetPixel(x, y, result);
            }
        }
    }

    private void ApplyBrush(
        Vector2I destination,
        PaintBrushData brushData
    )
    {
        Vector2I brushSize =
            _cachedBrushImage.GetSize();

        int sourceX = 0;
        int sourceY = 0;

        int destinationX = destination.X;
        int destinationY = destination.Y;

        int width = brushSize.X;
        int height = brushSize.Y;

        /*
         * 将笔刷裁剪到画布边界内。
         */
        if (destinationX < 0)
        {
            sourceX = -destinationX;
            width -= sourceX;
            destinationX = 0;
        }

        if (destinationY < 0)
        {
            sourceY = -destinationY;
            height -= sourceY;
            destinationY = 0;
        }

        width = Mathf.Min(
            width,
            _canvasWidth - destinationX
        );

        height = Mathf.Min(
            height,
            _canvasHeight - destinationY
        );

        if (width <= 0 || height <= 0)
            return;

        Rect2I sourceRect = new(
            new Vector2I(sourceX, sourceY),
            new Vector2I(width, height)
        );

        Vector2I clippedDestination = new(
            destinationX,
            destinationY
        );

        if (brushData.BlendMode == PaintBlendMode.Erase)
        {
            EraseRect(
                sourceRect,
                clippedDestination
            );
        }
        else
        {
            _canvasImage.BlendRect(
                _cachedBrushImage,
                sourceRect,
                clippedDestination
            );

            RegisterFadePixels(
                sourceRect,
                clippedDestination,
                brushData
            );
        }

        _textureDirty = true;
    }

    private void EraseRect(
        Rect2I sourceRect,
        Vector2I destination
    )
    {
        for (int y = 0; y < sourceRect.Size.Y; y++)
        {
            for (int x = 0; x < sourceRect.Size.X; x++)
            {
                int brushX =
                    sourceRect.Position.X + x;

                int brushY =
                    sourceRect.Position.Y + y;

                int canvasX =
                    destination.X + x;

                int canvasY =
                    destination.Y + y;

                Color brushPixel =
                    _cachedBrushImage.GetPixel(
                        brushX,
                        brushY
                    );

                if (brushPixel.A <= 0.0f)
                    continue;

                Color canvasPixel =
                    _canvasImage.GetPixel(
                        canvasX,
                        canvasY
                    );

                float newAlpha = Mathf.Max(
                    0.0f,
                    canvasPixel.A - brushPixel.A
                );

                if (newAlpha <= 0.001f)
                {
                    _canvasImage.SetPixel(
                        canvasX,
                        canvasY,
                        Colors.Transparent
                    );

                    _fadingPixels.Remove(
                        GetPixelIndex(canvasX, canvasY)
                    );
                }
                else
                {
                    canvasPixel.A = newAlpha;

                    _canvasImage.SetPixel(
                        canvasX,
                        canvasY,
                        canvasPixel
                    );

                    int pixelIndex =
                        GetPixelIndex(canvasX, canvasY);

                    if (_fadingPixels.TryGetValue(
                        pixelIndex,
                        out FadePixelState fadeState
                    ))
                    {
                        fadeState.BaseColor = canvasPixel;
                        _fadingPixels[pixelIndex] = fadeState;
                    }
                }
            }
        }
    }

    private void RegisterFadePixels(
        Rect2I sourceRect,
        Vector2I destination,
        PaintBrushData brushData
    )
    {
        for (int y = 0; y < sourceRect.Size.Y; y++)
        {
            for (int x = 0; x < sourceRect.Size.X; x++)
            {
                Color brushPixel = _cachedBrushImage.GetPixel(
                    sourceRect.Position.X + x,
                    sourceRect.Position.Y + y
                );

                if (brushPixel.A <= 0.0f)
                    continue;

                int canvasX = destination.X + x;
                int canvasY = destination.Y + y;
                int pixelIndex = GetPixelIndex(canvasX, canvasY);

                if (!brushData.FadeEnabled)
                {
                    _fadingPixels.Remove(pixelIndex);
                    continue;
                }

                _fadingPixels[pixelIndex] = new FadePixelState
                {
                    BaseColor = _canvasImage.GetPixel(canvasX, canvasY),
                    DelayRemaining = Mathf.Max(0.0f, brushData.FadeDelay),
                    Elapsed = 0.0f,
                    Duration = Mathf.Max(0.01f, brushData.FadeDuration)
                };
            }
        }
    }

    private void UpdateFadingPixels(float delta)
    {
        if (_fadingPixels.Count == 0 || delta <= 0.0f)
            return;

        _fadePixelKeys.Clear();
        _fadePixelKeys.AddRange(_fadingPixels.Keys);

        foreach (int pixelIndex in _fadePixelKeys)
        {
            if (!_fadingPixels.TryGetValue(
                pixelIndex,
                out FadePixelState fadeState
            ))
            {
                continue;
            }

            float fadeDelta = delta;
            if (fadeState.DelayRemaining > 0.0f)
            {
                fadeDelta = Mathf.Max(
                    0.0f,
                    delta - fadeState.DelayRemaining
                );
                fadeState.DelayRemaining = Mathf.Max(
                    0.0f,
                    fadeState.DelayRemaining - delta
                );
            }

            if (fadeDelta <= 0.0f)
            {
                _fadingPixels[pixelIndex] = fadeState;
                continue;
            }

            fadeState.Elapsed += fadeDelta;
            float fadeProgress = Mathf.Clamp(
                fadeState.Elapsed / fadeState.Duration,
                0.0f,
                1.0f
            );

            int pixelX = pixelIndex % _canvasWidth;
            int pixelY = pixelIndex / _canvasWidth;

            if (fadeProgress >= 1.0f)
            {
                _canvasImage.SetPixel(
                    pixelX,
                    pixelY,
                    Colors.Transparent
                );
                _fadingPixels.Remove(pixelIndex);
            }
            else
            {
                Color fadedColor = fadeState.BaseColor;
                fadedColor.A = fadeState.BaseColor.A *
                    (1.0f - fadeProgress);
                _canvasImage.SetPixel(
                    pixelX,
                    pixelY,
                    fadedColor
                );
                _fadingPixels[pixelIndex] = fadeState;
            }

            _textureDirty = true;
        }
    }

    private int GetPixelIndex(int x, int y)
    {
        return y * _canvasWidth + x;
    }
}
