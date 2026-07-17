using Godot;

/// <summary>
/// 当前单局 HUD 的轻量状态。尚未完成的玩法只暴露占位值，不产生效果。
/// </summary>
[GlobalClass]
public partial class RunHudState : Node
{
    [Signal]
    public delegate void StateChangedEventHandler();

    [Export(PropertyHint.Range, "1,99,1")]
    public int FloorNumber { get; set; } = 1;

    [Export]
    public bool HasRoomExploration { get; set; }

    [Export(PropertyHint.Range, "0,1,0.01")]
    public float RoomExplorationRatio { get; set; }

    [Export(PropertyHint.Range, "0,1000,1")]
    public float PollutionCurrent { get; set; }

    [Export(PropertyHint.Range, "1,1000,1")]
    public float PollutionMaximum { get; set; } = 100.0f;

    public float PollutionRatio =>
        PollutionMaximum <= 0.0f
            ? 0.0f
            : Mathf.Clamp(PollutionCurrent / PollutionMaximum, 0.0f, 1.0f);

    public void SetFloor(int floorNumber)
    {
        int nextFloor = Mathf.Max(floorNumber, 1);
        if (FloorNumber == nextFloor)
            return;

        FloorNumber = nextFloor;
        EmitSignal(SignalName.StateChanged);
    }

    public void SetPollution(float current, float maximum)
    {
        float nextMaximum = Mathf.Max(maximum, 1.0f);
        float nextCurrent = Mathf.Clamp(current, 0.0f, nextMaximum);

        if (
            Mathf.IsEqualApprox(PollutionMaximum, nextMaximum) &&
            Mathf.IsEqualApprox(PollutionCurrent, nextCurrent)
        )
        {
            return;
        }

        PollutionMaximum = nextMaximum;
        PollutionCurrent = nextCurrent;
        EmitSignal(SignalName.StateChanged);
    }
}
