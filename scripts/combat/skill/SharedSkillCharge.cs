using Godot;

/// <summary>
/// 玩家级共享技能充能。所有已装备武器读取并消费同一份状态。
/// </summary>
[GlobalClass]
public partial class SharedSkillCharge : Node
{
    [Signal]
    public delegate void ChargeChangedEventHandler(float current, float maximum);

    [Signal]
    public delegate void ReadyChangedEventHandler(bool isReady);

    [Export(PropertyHint.Range, "1,1000,1")]
    public float MaxCharge { get; set; } = 100.0f;

    [Export]
    public bool StartFull { get; set; }

    public float CurrentCharge { get; private set; }

    public float ChargeRatio =>
        MaxCharge <= 0.0f
            ? 0.0f
            : Mathf.Clamp(CurrentCharge / MaxCharge, 0.0f, 1.0f);

    public bool IsReady =>
        MaxCharge > 0.0f && CurrentCharge >= MaxCharge;

    private bool _lastReadyState;

    public override void _Ready()
    {
        CurrentCharge = StartFull ? Mathf.Max(MaxCharge, 1.0f) : 0.0f;
        _lastReadyState = IsReady;
        EmitSignal(SignalName.ChargeChanged, CurrentCharge, MaxCharge);
        EmitSignal(SignalName.ReadyChanged, _lastReadyState);
    }

    public void RegisterHitPercent(float percent)
    {
        if (percent <= 0.0f)
            return;

        AddCharge(
            Mathf.Max(MaxCharge, 0.0f) * percent / 100.0f
        );
    }

    public void AddCharge(float amount)
    {
        if (amount <= 0.0f || IsReady)
            return;

        SetCharge(CurrentCharge + amount);
    }

    public bool TryConsume()
    {
        if (!IsReady)
            return false;

        SetCharge(0.0f);
        return true;
    }

    public void ResetCharge(bool full = false)
    {
        SetCharge(full ? MaxCharge : 0.0f);
    }

    private void SetCharge(float value)
    {
        float maximum = Mathf.Max(MaxCharge, 1.0f);
        float nextValue = Mathf.Clamp(value, 0.0f, maximum);

        if (Mathf.IsEqualApprox(CurrentCharge, nextValue))
            return;

        CurrentCharge = nextValue;
        EmitSignal(SignalName.ChargeChanged, CurrentCharge, maximum);

        bool ready = IsReady;
        if (ready == _lastReadyState)
            return;

        _lastReadyState = ready;
        EmitSignal(SignalName.ReadyChanged, ready);
    }
}
