using Godot;
using System.Collections.Generic;

/// <summary>
/// 读取鼠标输入，生成连续绘画轨迹。
///
/// 该节点只负责：
/// 1. 输入
/// 2. 鼠标采样
/// 3. 插值
/// 4. 调用画布盖笔刷
/// 5. 将结果交给武器
/// </summary>
public partial class PaintStrokeController : Node2D
{
    [Signal]
    public delegate void PaintStartedEventHandler();

    [Signal]
    public delegate void PaintProgressChangedEventHandler(
        float currentLength,
        float maximumLength
    );

    [Signal]
    public delegate void PaintCompletedEventHandler();

    [Signal]
    public delegate void PaintCancelledEventHandler();

    // Keep emit sites independent from Godot's generated SignalName class.
    // This prevents design-time builds from reporting CS0117 while the source
    // generator is still refreshing after a new signal declaration is added.
    private static readonly StringName PaintStartedSignal =
        new("PaintStarted");

    private static readonly StringName PaintProgressChangedSignal =
        new("PaintProgressChanged");

    private static readonly StringName PaintCompletedSignal =
        new("PaintCompleted");

    private static readonly StringName PaintCancelledSignal =
        new("PaintCancelled");

    [ExportGroup("Input")]

    [Export]
    private StringName _paintAction =
        new("weapon_skill");

    [ExportGroup("Dependencies")]

    [Export]
    private PaintCanvas2D _paintCanvas;

    [Export]
    private SlowMotionController _slowMotionController;

    private readonly List<Vector2> _strokePoints =
        new();

    private IPaintSkillWeapon _activeWeapon;

    private bool _isPainting;
    private bool _hasPreviousPoint;

    private Vector2 _previousPoint;

    private float _currentLength;
    private ulong _startTimeMilliseconds;

    public bool IsPainting => _isPainting;

    public override void _UnhandledInput(
        InputEvent inputEvent
    )
    {
        if (
            inputEvent.IsActionPressed(_paintAction)
        )
        {
            TryBeginPainting();
            GetViewport().SetInputAsHandled();
            return;
        }

        if (
            inputEvent.IsActionReleased(_paintAction)
        )
        {
            CompletePainting();
            GetViewport().SetInputAsHandled();
            return;
        }

        if (
            _isPainting &&
            inputEvent.IsActionPressed("ui_cancel")
        )
        {
            CancelPainting();
            GetViewport().SetInputAsHandled();
        }
    }

    public override void _Process(double delta)
    {
        if (!_isPainting)
            return;

        PaintBrushData brushData =
            _activeWeapon.PaintBrushData;

        float elapsedSeconds =
            GetElapsedSeconds();

        if (
            elapsedSeconds >=
            brushData.MaxDrawDuration
        )
        {
            CompletePainting();
            return;
        }

        Vector2 mousePoint = ResolvePaintPoint(
            GetGlobalMousePosition()
        );

        AppendMousePoint(
            mousePoint,
            brushData
        );
    }

    /// <summary>
    /// WeaponManager 在切换当前武器后调用。
    /// </summary>
    public void SetActiveWeapon(
        IPaintSkillWeapon weapon
    )
    {
        if (_activeWeapon == weapon)
            return;

        if (_isPainting)
            CancelPainting();

        _activeWeapon = weapon;
    }

    /// <summary>
    /// 玩家进入新房间时调用。
    /// </summary>
    public void SetPaintCanvas(
        PaintCanvas2D canvas
    )
    {
        if (_isPainting)
            CancelPainting();

        _paintCanvas = canvas;
    }

    private void TryBeginPainting()
    {
        if (_isPainting)
            return;

        if (_paintCanvas == null)
        {
            GD.PushWarning(
                "PaintStrokeController 没有设置 PaintCanvas2D。"
            );
            return;
        }

        if (_activeWeapon == null)
            return;

        if (!_activeWeapon.CanBeginPaint())
            return;

        PaintBrushData brushData =
            _activeWeapon.PaintBrushData;

        if (brushData == null)
        {
            GD.PushWarning(
                "当前武器没有设置 PaintBrushData。"
            );
            return;
        }

        _strokePoints.Clear();

        _currentLength = 0.0f;
        _hasPreviousPoint = false;
        _startTimeMilliseconds =
            Time.GetTicksMsec();

        _isPainting = true;

        _activeWeapon.BeginPaint();
        EmitSignal(PaintStartedSignal);
        EmitSignal(
            PaintProgressChangedSignal,
            0.0f,
            brushData.MaxStrokeLength
        );

        if (_slowMotionController != null)
        {
            _slowMotionController.Begin(
                brushData.SlowMotionScale
            );
        }

        Vector2 startPoint = ResolvePaintPoint(
            GetGlobalMousePosition()
        );

        AddFirstPoint(
            startPoint,
            brushData
        );
    }

    private void AddFirstPoint(
        Vector2 globalPoint,
        PaintBrushData brushData
    )
    {
        if (ShouldStampPaintCanvas())
        {
            _paintCanvas.StampAtGlobal(
                globalPoint,
                brushData
            );
        }

        _strokePoints.Add(globalPoint);

        _previousPoint = globalPoint;
        _hasPreviousPoint = true;
        NotifyPaintPointAccepted(globalPoint);
    }

    private void AppendMousePoint(
        Vector2 currentPoint,
        PaintBrushData brushData
    )
    {
        if (!_hasPreviousPoint)
        {
            AddFirstPoint(
                currentPoint,
                brushData
            );

            return;
        }

        float distance =
            _previousPoint.DistanceTo(currentPoint);

        if (
            distance <
            brushData.MinSampleDistance
        )
        {
            return;
        }

        float remainingLength =
            brushData.MaxStrokeLength -
            _currentLength;

        if (remainingLength <= 0.0f)
        {
            CompletePainting();
            return;
        }

        Vector2 acceptedPoint =
            currentPoint;

        bool reachedMaximumLength = false;

        if (distance >= remainingLength)
        {
            Vector2 direction =
                (currentPoint - _previousPoint)
                .Normalized();

            acceptedPoint =
                _previousPoint +
                direction * remainingLength;

            reachedMaximumLength = true;
        }

        StampInterpolatedSegment(
            _previousPoint,
            acceptedPoint,
            brushData
        );

        float acceptedDistance =
            _previousPoint.DistanceTo(
                acceptedPoint
            );

        _currentLength += acceptedDistance;
        EmitSignal(
            PaintProgressChangedSignal,
            _currentLength,
            brushData.MaxStrokeLength
        );

        _strokePoints.Add(acceptedPoint);

        _previousPoint = acceptedPoint;
        NotifyPaintPointAccepted(acceptedPoint);

        if (reachedMaximumLength)
            CompletePainting();
    }

    /// <summary>
    /// 在上一采样点和当前采样点之间插值盖章。
    /// 这是避免快速移动鼠标时出现断线的核心。
    /// </summary>
    private void StampInterpolatedSegment(
        Vector2 from,
        Vector2 to,
        PaintBrushData brushData
    )
    {
        if (!ShouldStampPaintCanvas())
            return;

        float distance =
            from.DistanceTo(to);

        if (distance <= 0.001f)
            return;

        float spacing = Mathf.Max(
            1.0f,
            brushData.BrushSize *
            brushData.SpacingRatio
        );

        int stampCount = Mathf.Max(
            1,
            Mathf.CeilToInt(
                distance / spacing
            )
        );

        for (int i = 1; i <= stampCount; i++)
        {
            float interpolation =
                (float)i / stampCount;

            Vector2 stampPoint =
                from.Lerp(
                    to,
                    interpolation
                );

            _paintCanvas.StampAtGlobal(
                stampPoint,
                brushData
            );
        }
    }

    private Vector2 ResolvePaintPoint(Vector2 globalPoint)
    {
        Vector2 resolvedPoint = _paintCanvas.ClampGlobalPoint(globalPoint);

        if (_activeWeapon is IPaintStrokeRuntime runtime)
        {
            resolvedPoint = runtime.ConstrainPaintSkillPoint(
                resolvedPoint
            );
        }

        return resolvedPoint;
    }

    private bool ShouldStampPaintCanvas()
    {
        return _activeWeapon is not IPaintStrokeRuntime runtime ||
               runtime.ShouldStampPaintCanvas;
    }

    private void NotifyPaintPointAccepted(Vector2 globalPoint)
    {
        if (_activeWeapon is IPaintStrokeRuntime runtime)
            runtime.OnPaintSkillPointAccepted(globalPoint);
    }

    private void CompletePainting()
    {
        if (!_isPainting)
            return;

        IPaintSkillWeapon weapon =
            _activeWeapon;

        PaintBrushData brushData =
            weapon.PaintBrushData;

        float duration =
            GetElapsedSeconds();

        bool isValid =
            _strokePoints.Count >= 2 &&
            _currentLength >=
            brushData.MinValidLength;

        PaintStrokeResult result = null;

        if (isValid)
        {
            result = new PaintStrokeResult(
                _strokePoints,
                duration,
                brushData
            );
        }

        StopPaintingSession();

        if (result != null)
        {
            weapon.CommitPaint(result);
            EmitSignal(PaintCompletedSignal);
        }
        else
        {
            weapon.CancelPaint();
            EmitSignal(PaintCancelledSignal);
        }
    }

    public void CancelPainting()
    {
        if (!_isPainting)
            return;

        IPaintSkillWeapon weapon =
            _activeWeapon;

        StopPaintingSession();

        weapon?.CancelPaint();
        EmitSignal(PaintCancelledSignal);
    }

    private void StopPaintingSession()
    {
        _isPainting = false;
        _hasPreviousPoint = false;

        _slowMotionController?.End();

        _strokePoints.Clear();
        _currentLength = 0.0f;
    }

    private float GetElapsedSeconds()
    {
        ulong currentTime =
            Time.GetTicksMsec();

        return (
            currentTime -
            _startTimeMilliseconds
        ) / 1000.0f;
    }

    public override void _ExitTree()
    {
        if (_isPainting)
            CancelPainting();
    }
}
