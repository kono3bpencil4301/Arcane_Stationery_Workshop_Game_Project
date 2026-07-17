using Godot;
using Godot.Collections;

/// <summary>一个关卡周期内按百分比排列的怪潮事件队列。</summary>
[GlobalClass]
public partial class EnemyWaveTimelineData : Resource
{
    [Export(PropertyHint.Range, "1,7200,1,or_greater")]
    public float CycleDurationSeconds { get; set; } = 300.0f;

    [Export]
    public bool LoopTimeline { get; set; } = true;

    [Export]
    public Array<EnemyWaveEvent> WaveEvents { get; set; } = new();
}
