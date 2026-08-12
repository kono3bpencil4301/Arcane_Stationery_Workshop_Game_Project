using Godot;

public partial class ChalkWeapon : WeaponBase
{
    [ExportCategory("Weapon")]

    [Export]
    public PackedScene ProjectileScene { get; set; }

    [Export]
    public Node2D Muzzle { get; set; }

    [Export]
    public float AttackInterval { get; set; } = 1.1f;

    [Export(PropertyHint.Range, "0,64,1")]
public float ProjectileSpawnOffset { get; set; } = 18.0f;

    private float _cooldownRemaining;

    protected override float AutoAttackCooldown =>
        Mathf.Max(AttackInterval, 0.01f);

    public override void _Process(double delta)
    {
        base._Process(delta);

        if (_cooldownRemaining > 0.0f)
        {
            _cooldownRemaining -= (float)delta;
        }
    }

    /// <summary>
    /// 自动寻找攻击范围内最近的敌人并发射粉笔。
    /// </summary>
    protected override void AutoAttack()
    {
        Node2D target = FindNearestEnemy();

        if (target == null)
        {
            return;
        }

        TryFire(target.GlobalPosition);
    }

    private Node2D FindNearestEnemy()
    {
        if (Muzzle == null)
        {
            return null;
        }

        float attackRange = Mathf.Max(Data.AttackRange, 0.0f);
        float maxDistanceSquared = attackRange * attackRange;
        float nearestDistanceSquared = maxDistanceSquared;
        Node2D nearestEnemy = null;

        foreach (Node node in GetTree().GetNodesInGroup("enemy"))
        {
            if (node is not Node2D enemy)
            {
                continue;
            }

            float distanceSquared =
                Muzzle.GlobalPosition.DistanceSquaredTo(
                    enemy.GlobalPosition
                );

            if (distanceSquared > nearestDistanceSquared)
            {
                continue;
            }

            nearestDistanceSquared = distanceSquared;
            nearestEnemy = enemy;
            if(!HasClearShot(Muzzle.GlobalPosition, enemy.GlobalPosition))  continue;
        }


        return nearestEnemy;
    }

    public bool CanFire()
    {
        return
            _cooldownRemaining <= 0.0f &&
            ProjectileScene != null &&
            Muzzle != null;
    }

    /// <summary>
    /// targetGlobalPosition 可以来自：
    /// 1. 最近敌人的位置
    /// 2. 鼠标世界坐标
    /// 3. 锁定目标的位置
    /// </summary>
    public bool TryFire(Vector2 targetGlobalPosition)
    {
        if (!CanFire())
        {
            return false;
        }

        Vector2 direction =
            targetGlobalPosition -
            Muzzle.GlobalPosition;

        var normalizedDirection = direction.Normalized();

        if (direction.IsZeroApprox())
        {
            return false;
        }

        ChalkProjectile projectile =
            ProjectileScene.Instantiate<ChalkProjectile>();

        GetTree().CurrentScene.AddChild(projectile);

        projectile.GlobalPosition = Muzzle.GlobalPosition;

        projectile.GlobalPosition =
            Muzzle.GlobalPosition + normalizedDirection * Mathf.Max(ProjectileSpawnOffset, 0.0f);

        projectile.Setup(normalizedDirection, this);

        _cooldownRemaining = AttackInterval;

        return true;
    }

    private bool HasClearShot(Vector2 from, Vector2 to)
    {
        World2D world = GetWorld2D();

        if(world == null)
            return false;

        PhysicsRayQueryParameters2D Query = PhysicsRayQueryParameters2D.Create(from, to);

        Query.CollisionMask = 1u;
        Query.CollideWithBodies = true;
        Query.CollideWithAreas = false;

        Godot.Collections.Dictionary result =
            world.DirectSpaceState.IntersectRay(Query);

        return result.Count == 0;
    }
}
