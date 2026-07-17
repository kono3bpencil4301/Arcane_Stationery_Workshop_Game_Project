using Godot;

/// <summary>怪潮中的一个加权敌人候选项。</summary>
[GlobalClass]
public partial class EnemySpawnEntry : Resource
{
    [Export]
    public PackedScene EnemyScene { get; set; }

    [Export(PropertyHint.Range, "0,1000,0.1,or_greater")]
    public float Weight { get; set; } = 1.0f;
}
