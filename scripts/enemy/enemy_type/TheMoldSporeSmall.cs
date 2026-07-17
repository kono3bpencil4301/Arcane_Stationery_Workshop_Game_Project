using Godot;
public partial class TheMoldSporeSmall : EnemyBase
{
    [ExportCategory("Pollution Trail")]
    [Export] public float PollutionTrailDuration = 2.0f;
    [Export(PropertyHint.Range, "0,60,0.1")]
    public float PollutionDuration = 5.0f;
    [Export]
    public Color PollutionColor = new Color(0.0f, 0.0f, 0.0f, 0.42f);


    [Export(PropertyHint.Range, "0.1,1,0.05")]
    public float PollutedPlayerSpeedMultiplier = 0.8f;


    [Export(PropertyHint.Range, "0,30,0.1")]
    public float PollutionGraceDuration = 3.0f;

    [Export(PropertyHint.Range, "0,100,0.5")]
    public float PollutionDamagePerSecond = 2.0f;

}
