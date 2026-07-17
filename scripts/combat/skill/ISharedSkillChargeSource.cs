using Godot;

/// <summary>
/// 能为玩家共享技能槽提供充能的伤害来源。
/// 数值使用最大技能槽的百分比，而不是固定点数。
/// </summary>
public interface ISharedSkillChargeSource
{
    float SharedSkillChargePercent { get; }

    Node SharedSkillChargeOwner { get; }
}
