using Godot;

/// <summary>
/// 简单的单层慢动作控制器。
/// 后续加入完美闪避、击杀停顿时，可升级为请求栈。
/// </summary>
public partial class SlowMotionController : Node
{
    private bool _isActive;
    private double _previousTimeScale = 1.0;

    public bool IsActive => _isActive;

    public void Begin(float targetScale)
    {
        if (_isActive)
            return;

        _previousTimeScale =
            Engine.TimeScale;

        Engine.TimeScale = Mathf.Clamp(
            targetScale,
            0.05f,
            1.0f
        );

        _isActive = true;
    }

    public void End()
    {
        if (!_isActive)
            return;

        Engine.TimeScale =
            _previousTimeScale;

        _isActive = false;
    }

    public override void _ExitTree()
    {
        End();
    }
}