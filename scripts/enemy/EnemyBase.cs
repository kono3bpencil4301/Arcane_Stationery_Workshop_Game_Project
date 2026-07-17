using Godot;
using System.Collections.Generic;

[GlobalClass]
public partial class EnemyBase : CharacterBody2D, IDamageable
{
    [ExportCategory("Status")]

    [Export]
    public float MaxHealth = 50f;


    [Export]
    public float MoveSpeed = 80f;


    [ExportCategory("Movement")]


    /// <summary>敌人与玩家保持的最小追踪距离，避免持续撞击并推挤玩家。</summary>
    [Export(PropertyHint.Range, "0,128,1")]
    public float TargetStopDistance = 30.0f;


    /// <summary>敌人之间开始产生分离力的距离。</summary>
    [Export(PropertyHint.Range, "8,128,1")]
    public float SeparationRadius = 30.0f;


    /// <summary>分离方向在最终移动方向中的权重。</summary>
    [Export(PropertyHint.Range, "0,3,0.05")]
    public float SeparationStrength = 1.15f;


    /// <summary>受到玩家推动后，外部推力每秒衰减的速度。</summary>
    [Export(PropertyHint.Range, "0,3000,10")]
    public float PushDeceleration = 600.0f;


    /// <summary>不同敌人对玩家推动的响应倍率；0 表示不可推动。</summary>
    [Export(PropertyHint.Range, "0,2,0.05")]
    public float PlayerPushMultiplier = 1.0f;


    [ExportCategory("Attack")]


    /// <summary>敌人靠近玩家后每次造成的接触伤害。</summary>
    [Export(PropertyHint.Range, "0,1000,1")]
    public float ContactDamage = 10.0f;


    [Export(PropertyHint.Range, "1,256,1")]
    public float ContactAttackRange = 30.0f;


    [Export(PropertyHint.Range, "0.1,10,0.1")]
    public float ContactAttackInterval = 1.0f;


    [ExportCategory("Damage Feedback")]


    [Export]
    public Color DamageNumberColor = new(1.0f, 0.82f, 0.2f, 1.0f);


    [Export]
    public Vector2 DamageNumberOffset = new(0.0f, -30.0f);


    [Export(PropertyHint.Range, "8,64,1")]
    public int DamageNumberFontSize = 18;


    [Export(PropertyHint.Range, "0,128,1")]
    public float DamageNumberRiseDistance = 40.0f;


    [Export(PropertyHint.Range, "0.05,3,0.05")]
    public float DamageNumberDuration = 0.75f;


    [ExportCategory("Death")]


    [Export(PropertyHint.Range, "0,3,0.05")]
    public double DeathFadeDuration = 0.4;


    protected float CurrentHealth;


    protected bool IsDead = false;


    protected Node2D Target;


    /// <summary>
    /// 当前包含该敌人的技能区域。
    /// HashSet 可以避免同一个 Area2D 因重复信号而被注册多次。
    /// </summary>
    private readonly HashSet<Node> insideSkillAreas = new();


    private Polygon2D _skillMarkVisual;


    private float _contactAttackCooldownRemaining;


    private Vector2 _externalPushVelocity;


    /// <summary>供特殊敌人状态机叠加玩家推力。</summary>
    protected Vector2 ExternalPushVelocity => _externalPushVelocity;


    public override void _Ready()
    {
        // 敌人必须在自身移动查询中检测 Player 层。
        // 如果只有玩家检测敌人，敌人会先穿入玩家碰撞体，随后玩家的
        // MoveAndSlide 重叠恢复会把玩家快速弹开。
        SetCollisionMaskValue(2, true);

        CurrentHealth = MaxHealth;

        AddToGroup("enemy");

        FindPlayer();
    }


    public override void _PhysicsProcess(double delta)
    {
        if(IsDead)
            return;

        _contactAttackCooldownRemaining = Mathf.Max(
            0.0f,
            _contactAttackCooldownRemaining - (float)delta
        );


        if(!GodotObject.IsInstanceValid(Target))
        {
            Target = null;
            FindPlayer();
        }


        if(Target != null)
        {
            MoveToTarget();
            TryContactAttack();
        }
        else
        {
            Velocity = _externalPushVelocity;


            if(!Velocity.IsZeroApprox())
                MoveAndSlide();
        }


        _externalPushVelocity =
            _externalPushVelocity.MoveToward(
                Vector2.Zero,
                Mathf.Max(PushDeceleration, 0.0f) * (float)delta
            );
    }


    /// <summary>
    /// 使用距离判定攻击玩家，不依赖敌人与玩家之间的物理推挤。
    /// </summary>
    private void TryContactAttack()
    {
        if(
            !CanContactAttack() ||
            _contactAttackCooldownRemaining > 0.0f ||
            Target is not IDamageable damageable
        )
        {
            return;
        }


        Vector2 hitDirection = GetContactHitDirection();


        if(
            GlobalPosition.DistanceSquaredTo(Target.GlobalPosition) >
            Mathf.Pow(Mathf.Max(ContactAttackRange, 1.0f), 2.0f)
        )
        {
            return;
        }


        damageable.TakeDamage(
            Mathf.Max(
                ContactDamage * GetContactDamageMultiplier(),
                0.0f
            ),
            this,
            Target.GlobalPosition,
            hitDirection
        );


        _contactAttackCooldownRemaining =
            Mathf.Max(ContactAttackInterval, 0.1f);


        OnContactAttackLanded();
    }


    /// <summary>特殊敌人可限制只有某个攻击状态能够造成接触伤害。</summary>
    protected virtual bool CanContactAttack()
    {
        return true;
    }


    /// <summary>特殊攻击的伤害倍率，普通敌人保持 1 倍。</summary>
    protected virtual float GetContactDamageMultiplier()
    {
        return 1.0f;
    }


    protected virtual Vector2 GetContactHitDirection()
    {
        return GlobalPosition.DirectionTo(Target.GlobalPosition);
    }


    /// <summary>伤害成功送入目标后通知特殊敌人切换状态。</summary>
    protected virtual void OnContactAttackLanded()
    {
    }


    protected void ResetContactAttackCooldown()
    {
        _contactAttackCooldownRemaining = 0.0f;
    }



    protected virtual void MoveToTarget()
    {
        Vector2 toTarget =
            Target.GlobalPosition - GlobalPosition;
        float targetDistance = toTarget.Length();


        Vector2 pursuitDirection =
            targetDistance > Mathf.Max(TargetStopDistance, 0.0f) &&
            !toTarget.IsZeroApprox()
                ? toTarget.Normalized()
                : Vector2.Zero;


        Vector2 separationDirection =
            CalculateEnemySeparation();


        // 已经进入停止距离后，只保留切向或远离玩家的分离力。
        // 否则敌群互相排斥时，仍可能把最内侧敌人再次挤向玩家。
        if(
            targetDistance <= Mathf.Max(TargetStopDistance, 0.0f) &&
            !toTarget.IsZeroApprox()
        )
        {
            Vector2 towardTarget = toTarget.Normalized();
            float towardAmount = separationDirection.Dot(towardTarget);

            if(towardAmount > 0.0f)
            {
                separationDirection -=
                    towardTarget * towardAmount;
            }
        }


        Vector2 movementDirection =
            pursuitDirection +
            separationDirection * Mathf.Max(SeparationStrength, 0.0f);


        Vector2 pursuitVelocity = Vector2.Zero;


        if(!movementDirection.IsZeroApprox())
        {
            pursuitVelocity =
                movementDirection.Normalized() *
                MoveSpeed *
                GetSkillSpeedMultiplier();
        }


        Velocity = pursuitVelocity + _externalPushVelocity;


        if(Velocity.IsZeroApprox())
            return;


        MoveAndSlide();
    }


    /// <summary>
    /// 由玩家的滑动碰撞调用。使用速度而非瞬移，确保敌人仍受墙体和其他敌人碰撞约束。
    /// </summary>
    public void ApplyPlayerPush(Vector2 direction, float pushSpeed)
    {
        if(
            IsDead ||
            direction.IsZeroApprox() ||
            pushSpeed <= 0.0f ||
            PlayerPushMultiplier <= 0.0f
        )
        {
            return;
        }


        _externalPushVelocity =
            direction.Normalized() *
            pushSpeed *
            PlayerPushMultiplier;
    }


    /// <summary>
    /// 计算附近敌人的排斥方向，避免所有敌人追踪同一点后完全重叠。
    /// </summary>
    protected Vector2 CalculateEnemySeparation()
    {
        float radius = Mathf.Max(SeparationRadius, 1.0f);
        Vector2 separation = Vector2.Zero;


        foreach(Node node in GetTree().GetNodesInGroup("enemy"))
        {
            if(
                node == this ||
                node is not Node2D otherEnemy ||
                !GodotObject.IsInstanceValid(otherEnemy)
            )
            {
                continue;
            }


            Vector2 away =
                GlobalPosition - otherEnemy.GlobalPosition;
            float distance = away.Length();


            if(distance >= radius)
                continue;


            if(distance <= 0.001f)
            {
                // 完全重叠时给每个实例一个稳定方向，使它们能够脱离重叠。
                float angle =
                    (GetInstanceId() % 360) *
                    Mathf.Pi / 180.0f;
                away = Vector2.FromAngle(angle);
                distance = 0.0f;
            }
            else
            {
                away /= distance;
            }


            float weight = 1.0f - distance / radius;
            separation += away * weight;
        }


        return separation;
    }



    protected void FindPlayer()
    {
        Node player =
            GetTree()
            .GetFirstNodeInGroup("player");


        // 兼容尚未统一大小写的旧场景。
        player ??=
            GetTree()
            .GetFirstNodeInGroup("Player");


        if(player is Node2D node)
        {
            Target = node;
        }
    }


    /// <summary>
    /// 由 SkillDamageArea 在敌人进入时调用。
    /// 返回 false 表示该区域之前已经注册过。
    /// </summary>
    public bool RegisterSkillArea(SkillDamageArea area)
    {
        if(
            area == null ||
            !GodotObject.IsInstanceValid(area) ||
            !insideSkillAreas.Add(area)
        )
        {
            return false;
        }


        UpdateSkillMarkVisual();
        return true;
    }


    /// <summary>由 SkillDamageArea 在敌人离开或区域销毁时调用。</summary>
    public void UnregisterSkillArea(SkillDamageArea area)
    {
        if(area == null || !insideSkillAreas.Remove(area))
            return;


        UpdateSkillMarkVisual();
    }


    /// <summary>
    /// 多个技能区域重叠时不叠乘减速，使用其中最低（最强）的倍率。
    /// </summary>
    protected float GetSkillSpeedMultiplier()
    {
        float multiplier = 1.0f;
        List<Node> invalidAreas = null;


        foreach(Node node in insideSkillAreas)
        {
            if(
                !GodotObject.IsInstanceValid(node) ||
                node is not SkillDamageArea area
            )
            {
                invalidAreas ??= new List<Node>();
                invalidAreas.Add(node);
                continue;
            }


            multiplier = Mathf.Min(
                multiplier,
                Mathf.Clamp(area.MovementSpeedMultiplier, 0.1f, 1.0f)
            );
        }


        if(invalidAreas != null)
        {
            foreach(Node node in invalidAreas)
                insideSkillAreas.Remove(node);
        }


        return multiplier;
    }


    /// <summary>
    /// 原型阶段使用小菱形表示技能标记：
    /// 石墨为黑色，粉笔为白色。后续可以替换成正式贴图或粒子。
    /// </summary>
    private void UpdateSkillMarkVisual()
    {
        DamageType? markType = null;


        foreach(Node node in insideSkillAreas)
        {
            if(node is not SkillDamageArea area)
                continue;


            markType = area.DamageType;


            if(area.DamageType == DamageType.ChalkDust)
                break;
        }


        if(markType == null)
        {
            if(GodotObject.IsInstanceValid(_skillMarkVisual))
                _skillMarkVisual.Visible = false;


            return;
        }


        if(!GodotObject.IsInstanceValid(_skillMarkVisual))
        {
            _skillMarkVisual = new Polygon2D
            {
                Name = "SkillAreaMark",
                Polygon = new Vector2[]
                {
                    new(0.0f, -6.0f),
                    new(6.0f, 0.0f),
                    new(0.0f, 6.0f),
                    new(-6.0f, 0.0f)
                },
                Position = new Vector2(0.0f, -28.0f),
                ZIndex = 20
            };


            AddChild(_skillMarkVisual);
        }


        _skillMarkVisual.Color =
            markType == DamageType.ChalkDust
                ? Colors.White
                : new Color(0.04f, 0.04f, 0.04f, 1.0f);
        _skillMarkVisual.Visible = true;
    }



    public virtual void TakeDamage(
        float damage,
        Node source,
        Vector2 hitPosition,
        Vector2 hitDirection
    )
    {
        if(IsDead || damage <= 0.0f)
            return;


        CurrentHealth -= damage;
        FloatingDamageNumber.Spawn(
            this,
            damage,
            DamageNumberColor,
            DamageNumberOffset,
            DamageNumberRiseDistance,
            DamageNumberDuration,
            DamageNumberFontSize
        );
        RegisterPlayerHit(source);


        GD.Print(
            $"{Name} HP:{CurrentHealth}"
        );


        FlashDamage();


        if(CurrentHealth <=0)
        {
            Die();
        }
    }

    private static void RegisterPlayerHit(Node source)
    {
        if (source is not ISharedSkillChargeSource chargeSource)
            return;

        float percent = Mathf.Max(
            chargeSource.SharedSkillChargePercent,
            0.0f
        );

        if (percent <= 0.0f)
            return;

        Node current = chargeSource.SharedSkillChargeOwner;

        while (current != null)
        {
            if (current is Player player)
            {
                player
                    .GetNodeOrNull<SharedSkillCharge>("SharedSkillCharge")?
                    .RegisterHitPercent(percent);
                return;
            }

            current = current.GetParent();
        }
    }



    protected virtual void FlashDamage()
    {
        // 后续做闪白
    }



    protected virtual void Die()
    {
        IsDead = true;


        Velocity = Vector2.Zero;


        RemoveFromGroup("enemy");
        SetPhysicsProcess(false);
        insideSkillAreas.Clear();


        // 淡出期间不再阻挡玩家、墙壁或武器攻击。
        CollisionLayer = 0;
        CollisionMask = 0;


        foreach(Node node in FindChildren("*", "CollisionShape2D", true, false))
        {
            if(node is CollisionShape2D collisionShape)
            {
                collisionShape.SetDeferred(
                    CollisionShape2D.PropertyName.Disabled,
                    true
                );
            }
        }


        GD.Print(
            $"{Name} Dead"
        );


        if(DeathFadeDuration <= 0.0)
        {
            QueueFree();
            return;
        }


        Tween fadeTween = CreateTween();
        fadeTween.SetTrans(Tween.TransitionType.Sine);
        fadeTween.SetEase(Tween.EaseType.Out);
        fadeTween.TweenProperty(
            this,
            "modulate:a",
            0.0f,
            DeathFadeDuration
        );
        fadeTween.TweenCallback(
            Callable.From(QueueFree)
        );
    }
}
