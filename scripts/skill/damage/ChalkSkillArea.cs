/// <summary>
/// 粉笔主动技能“点名时间”。
/// 沿玩家实际绘制的粉笔线生成连续伤害区域。
/// 持续伤害、减速和生命周期均由 SkillDamageArea 统一处理。
/// </summary>
public partial class ChalkSkillArea : SkillDamageArea
{
    protected override void ConfigureFromStroke(
        PaintStrokeResult strokeResult
    )
    {
        BuildTileAlignedArea(strokeResult.GlobalPoints);
    }
}
