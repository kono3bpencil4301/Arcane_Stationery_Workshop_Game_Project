using Godot;
using Godot.Collections;

/// <summary>描述一段怪潮的名称、生成节奏与敌人池。</summary>
[GlobalClass]
public partial class EnemyWaveData : Resource
{
    [Export]
    public string EventName { get; set; } = "未命名怪潮";

    [Export(PropertyHint.Range, "0.05,60,0.05,or_greater")]
    public float SpawnInterval { get; set; } = 1.0f;

    [Export]
    public Array<EnemySpawnEntry> EnemyQueue { get; set; } = new();
}
