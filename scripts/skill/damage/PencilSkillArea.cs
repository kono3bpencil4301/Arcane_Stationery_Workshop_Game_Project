/// <summary>
/// 铅笔主动技能“石墨轨迹”。
/// 沿玩家实际绘制的鼠标轨迹覆盖经过的瓦片。
/// 持续伤害、减速和生命周期均由 SkillDamageArea 统一处理。
/// </summary>

public partial class PencilSkillArea : SkillDamageArea
{
    protected override void ConfigureFromStroke(
        PaintStrokeResult strokeResult
    )
    {
        BuildTileAlignedArea(strokeResult.GlobalPoints);
    }
}
