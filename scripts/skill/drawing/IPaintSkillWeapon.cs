/// <summary>
/// 支持鼠标绘制技能的武器需要实现该接口。
/// </summary>
public interface IPaintSkillWeapon
{
    PaintBrushData PaintBrushData { get; }

    /// <summary>
    /// 当前是否允许开始绘画。
    /// 可以检查武器是否选中、技能充能和冷却。
    /// </summary>
    bool CanBeginPaint();

    /// <summary>
    /// 绘制刚刚开始。
    /// 适合暂停自动攻击或预占充能。
    /// </summary>
    void BeginPaint();

    /// <summary>
    /// 玩家完成了一条有效笔迹。
    /// </summary>
    void CommitPaint(PaintStrokeResult result);

    /// <summary>
    /// 玩家取消绘制，或者笔迹长度不足。
    /// </summary>
    void CancelPaint();
}