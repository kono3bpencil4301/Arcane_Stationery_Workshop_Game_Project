using Godot;

/// <summary>
/// 可以受到伤害的对象。
/// 建议由敌人的 CharacterBody2D 根节点实现。
/// </summary>
public interface IDamageable
{
    void TakeDamage(
        float amount,
        Node source,
        Vector2 hitPosition,
        Vector2 hitDirection
    );
}
