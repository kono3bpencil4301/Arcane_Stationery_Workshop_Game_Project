using Godot;

/// <summary>
/// 黑斑菌团释放的孢子弹。命中玩家后造成一次伤害并刷新持续中毒，
/// 同时实现 IErasable，供主橡皮与小橡皮清除。
/// </summary>
public partial class MoldSporeProjectile : CharacterBody2D, IErasable
{
    private static readonly StringName ErasableGroup = new("erasable");
    private static readonly StringName EnemyProjectileGroup =
        new("enemy_projectile");

    public int EraseLevel => 1;

    private Vector2 _direction = Vector2.Right;
    private Node _source;
    private float _damage = 10.0f;
    private float _speed = 140.0f;
    private float _remainingLifetime = 3.0f;
    private float _poisonDuration = 5.0f;
    private float _poisonTickInterval = 1.0f;
    private float _poisonDamagePerTick = 2.0f;
    private bool _isConsumed;

    /// <summary>必须在 AddChild 前配置，确保 _Ready 能添加来源碰撞例外。</summary>
    public void Configure(
        Vector2 direction,
        Node source,
        float damage,
        float speed,
        float lifetime,
        float poisonDuration,
        float poisonTickInterval,
        float poisonDamagePerTick
    )
    {
        _direction = direction.IsZeroApprox()
            ? Vector2.Right
            : direction.Normalized();
        _source = source;
        _damage = Mathf.Max(damage, 0.0f);
        _speed = Mathf.Max(speed, 1.0f);
        _remainingLifetime = Mathf.Max(lifetime, 0.05f);
        _poisonDuration = Mathf.Max(poisonDuration, 0.1f);
        _poisonTickInterval = Mathf.Max(poisonTickInterval, 0.1f);
        _poisonDamagePerTick = Mathf.Max(poisonDamagePerTick, 0.0f);
    }

    public override void _Ready()
    {
        AddToGroup(ErasableGroup);
        AddToGroup(EnemyProjectileGroup);
        Rotation = _direction.Angle();

        if(
            GodotObject.IsInstanceValid(_source) &&
            _source is PhysicsBody2D sourceBody
        )
        {
            AddCollisionExceptionWith(sourceBody);
        }
    }

    public override void _PhysicsProcess(double delta)
    {
        if(_isConsumed)
            return;

        float physicsDelta = (float)delta;
        _remainingLifetime -= physicsDelta;

        if(_remainingLifetime <= 0.0f)
        {
            Consume();
            return;
        }

        Vector2 intendedMotion = _direction * _speed * physicsDelta;
        Vector2 motionStart = GlobalPosition;
        KinematicCollision2D collision = MoveAndCollide(intendedMotion);

        if(collision == null)
            return;

        if(EnemyProjectileTileCollision.IsFence(collision))
        {
            EnemyProjectileTileCollision.FinishMotionThroughFence(
                this,
                motionStart,
                intendedMotion
            );
            return;
        }

        Node collider = collision.GetCollider() as Node;

        if(collider is Player player)
        {
            player.TakeDamage(
                _damage,
                this,
                collision.GetPosition(),
                _direction
            );

            MoldPoisonEffect.ApplyTo(
                player,
                _poisonDuration,
                _poisonTickInterval,
                _poisonDamagePerTick
            );
        }

        // 孢子撞到玩家或房间碰撞后立即消失。
        Consume();
    }

    public void Erase(Node source)
    {
        Consume();
    }

    private void Consume()
    {
        if(_isConsumed)
            return;

        _isConsumed = true;
        CollisionLayer = 0;
        CollisionMask = 0;
        SetPhysicsProcess(false);
        QueueFree();
    }
}

/// <summary>
/// 挂在玩家节点上的可刷新中毒状态。同类孢子再次命中只刷新持续时间，
/// 不重复叠加多个计时器。
/// </summary>
internal partial class MoldPoisonEffect : Node
{
    private const string EffectNodeName = "TheMoldSpotFungusPoison";

    private Player _target;
    private float _remainingDuration;
    private float _tickInterval = 1.0f;
    private float _damagePerTick;
    private float _tickAccumulator;

    public static void ApplyTo(
        Player target,
        float duration,
        float tickInterval,
        float damagePerTick
    )
    {
        if(
            !GodotObject.IsInstanceValid(target) ||
            target.IsDead ||
            damagePerTick <= 0.0f
        )
        {
            return;
        }

        MoldPoisonEffect effect =
            target.GetNodeOrNull<MoldPoisonEffect>(EffectNodeName);

        if(!GodotObject.IsInstanceValid(effect))
        {
            effect = new MoldPoisonEffect
            {
                Name = EffectNodeName
            };
            target.AddChild(effect);
        }

        effect.Refresh(
            target,
            duration,
            tickInterval,
            damagePerTick
        );
    }

    public override void _PhysicsProcess(double delta)
    {
        if(
            !GodotObject.IsInstanceValid(_target) ||
            _target.IsDead
        )
        {
            QueueFree();
            return;
        }

        float physicsDelta = (float)delta;
        float activeDelta = Mathf.Min(
            physicsDelta,
            Mathf.Max(_remainingDuration, 0.0f)
        );
        _remainingDuration -= physicsDelta;
        _tickAccumulator += activeDelta;

        while(_tickAccumulator >= _tickInterval)
        {
            _tickAccumulator -= _tickInterval;
            _target.TakeContinuousDamage(_damagePerTick, this);

            if(_target.IsDead)
                break;
        }

        if(_remainingDuration <= 0.0f || _target.IsDead)
            QueueFree();
    }

    private void Refresh(
        Player target,
        float duration,
        float tickInterval,
        float damagePerTick
    )
    {
        _target = target;
        _remainingDuration = Mathf.Max(
            _remainingDuration,
            Mathf.Max(duration, 0.1f)
        );
        _tickInterval = Mathf.Max(tickInterval, 0.1f);
        _damagePerTick = Mathf.Max(_damagePerTick, damagePerTick);
        _tickAccumulator = Mathf.Min(
            _tickAccumulator,
            _tickInterval
        );
    }
}
