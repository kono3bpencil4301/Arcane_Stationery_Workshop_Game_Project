public enum WeaponOperationMode
{
    /// <summary>
    /// 只有被玩家选中时才运行自动攻击，
    /// 同时可以使用主动技能。
    /// 例如：铅笔、粉笔、直尺。
    /// </summary>
    SelectedOnly = 0,

    /// <summary>
    /// 只要占据装备槽就持续运行，
    /// 但不能作为当前主动操作武器释放技能。
    /// 例如：自动护盾便笺、纯被动收纳袋。
    /// </summary>
    PassiveOnly = 1,

    /// <summary>
    /// 只要装备就持续运行自动效果，
    /// 被选中后还可以释放主动技能。
    /// 例如：环绕橡皮、圆规。
    /// </summary>
    Hybrid = 2
}