using Godot;

/// <summary>时间轴上某个进度阈值触发的怪潮事件。</summary>
[GlobalClass]
public partial class EnemyWaveEvent : Resource
{
    [Export]
    public EnemyWaveType EventType { get; set; } = EnemyWaveType.Small;

    [Export(PropertyHint.Range, "0,100,0.1")]
    public float ProgressPercent { get; set; }

    [Export]
    public EnemyWaveData Wave { get; set; }
}
