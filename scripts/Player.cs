using Godot;

/// <summary>
/// 玩家移动、动画和生命状态。
/// 实现 IDamageable 后可以接收敌人的接触伤害及后续其他伤害来源。
/// </summary>
[GlobalClass]
public partial class Player : CharacterBody2D, IDamageable
{
    [Signal]
    public delegate void HealthChangedEventHandler(float current, float maximum);

    [Signal]
    public delegate void DiedEventHandler();

    private const string WalkAnimationPrefix = "walk_";
    private const string IdleAnimationPrefix = "idle_";

    [ExportCategory("Movement")]

    [Export]
    public float MoveSpeed { get; set; } = 120.0f;

    /// <summary>玩家持续挤压敌人时赋予敌人的移动速度。</summary>
    [Export(PropertyHint.Range, "0,1000,5")]
    public float EnemyPushSpeed { get; set; } = 120.0f;

    [ExportCategory("Health")]

    [Export(PropertyHint.Range, "1,10000,1")]
    public float MaxHealth { get; set; } = 100.0f;

    /// <summary>受伤后暂时忽略新伤害，防止多只敌人同帧瞬间叠伤。</summary>
    [Export(PropertyHint.Range, "0,3,0.05")]
    public float InvulnerabilityDuration { get; set; } = 1.5f;

    [ExportCategory("Damage Feedback")]

    [Export]
    public Color DamageFlashColor { get; set; } =
        new(1.0f, 0.25f, 0.25f, 1.0f);

    [Export(PropertyHint.Range, "0.01,1,0.01")]
    public float DamageFlashDuration { get; set; } = 0.08f;

    [Export]
    public Color DamageNumberColor { get; set; } =
        new(1.0f, 0.25f, 0.2f, 1.0f);

    [Export]
    public Color HealingNumberColor { get; set; } =
        new(0.35f, 1.0f, 0.55f, 1.0f);

    [Export]
    public Vector2 DamageNumberOffset { get; set; } =
        new(0.0f, -30.0f);

    [Export(PropertyHint.Range, "8,64,1")]
    public int DamageNumberFontSize { get; set; } = 18;

    [Export(PropertyHint.Range, "0,128,1")]
    public float DamageNumberRiseDistance { get; set; } = 40.0f;

    [Export(PropertyHint.Range, "0.05,3,0.05")]
    public float DamageNumberDuration { get; set; } = 0.75f;

    [ExportCategory("Death")]

    [Export]
    public StringName DeathAnimation { get; set; } = "death";
    [Export]
    public StringName DeathFadeAnimation { get; set; } = "death_fade";

    /// <summary>死亡动画结束后等待多久重载当前场景。</summary>
    [Export(PropertyHint.Range, "0,3,0.05")]
    public float SceneResetDelay { get; set; } = 0.2f;

    public float CurrentHealth { get; private set; }
    public bool IsDead { get; private set; }
    public bool MovementLocked { get; private set; }
    public float HealthRatio =>
        MaxHealth <= 0.0f
            ? 0.0f
            : Mathf.Clamp(CurrentHealth / MaxHealth, 0.0f, 1.0f);

    private AnimatedSprite2D _bodySprite;
    private string _facingSuffix = "front";
    private bool _isInvulnerable;
    private bool _sceneResetScheduled;
    private ulong _damageFlashVersion;
    private Color _normalSpriteColor = Colors.White;
    private AnimationPlayer _animationPlayer;
    private float _pollutionMovementMultiplier = 1.0f;

    public override void _Ready()
    {
        AddToGroup("player");

        CurrentHealth = Mathf.Max(MaxHealth, 1.0f);
        IsDead = false;
        _isInvulnerable = false;
        _sceneResetScheduled = false;
        MovementLocked = false;

        _bodySprite =
            GetNodeOrNull<AnimatedSprite2D>("AnimatedSprite2D");
        _animationPlayer =
            GetNodeOrNull<AnimationPlayer>("AnimationPlayer");

        if (_bodySprite != null)
        {
            _normalSpriteColor = _bodySprite.Modulate;
            _bodySprite.AnimationFinished += OnAnimationFinished;
        }

        if (_animationPlayer != null)
            _animationPlayer.AnimationFinished += OnDeathFadeAnimationFinished;

        UpdateAnimation();
        EmitSignal(SignalName.HealthChanged, CurrentHealth, MaxHealth);
    }

    public override void _PhysicsProcess(double delta)
    {
        if (IsDead)
        {
            Velocity = Vector2.Zero;
            return;
        }

        if(MovementLocked)
        {
            Velocity = Vector2.Zero;
            UpdateAnimation();
            return;
        }

        Vector2 movement = Input.GetVector(
            "move_left",
            "move_right",
            "move_up",
            "move_down"
        );

        Velocity =
            movement *
            MoveSpeed *
            _pollutionMovementMultiplier;
        MoveAndSlide();
        PushCollidingEnemies(movement);

        if (!movement.IsZeroApprox())
            _facingSuffix = VectorToFacingSuffix(movement);

        UpdateAnimation();
    }

    /// <summary>
    /// 玩家仍然与敌人发生实体碰撞，但会把移动方向转成敌人的外部推力。
    /// 墙体等其他碰撞对象不受影响。
    /// </summary>
    private void PushCollidingEnemies(Vector2 movement)
    {
        if(movement.IsZeroApprox() || EnemyPushSpeed <= 0.0f)
            return;

        Vector2 pushDirection = movement.Normalized();

        for(int index = 0; index < GetSlideCollisionCount(); index++)
        {
            KinematicCollision2D collision = GetSlideCollision(index);

            if(collision.GetCollider() is EnemyBase enemy)
                enemy.ApplyPlayerPush(pushDirection, EnemyPushSpeed);
        }
    }

    /// <summary>
    /// IDamageable 入口。死亡或受伤无敌期间不会重复结算伤害。
    /// </summary>
    public void TakeDamage(
        float amount,
        Node source,
        Vector2 hitPosition,
        Vector2 hitDirection
    )
    {
        ApplyDamage(
            amount,
            source,
            hitPosition,
            hitDirection,
            true
        );
    }

    /// <summary>
    /// 持续环境伤害不受接触伤害无敌帧影响，确保按固定周期结算。
    /// </summary>
    public void TakeContinuousDamage(float amount, Node source)
    {
        ApplyDamage(
            amount,
            source,
            GlobalPosition,
            Vector2.Zero,
            false
        );
    }

    public void SetPollutionMovementMultiplier(float multiplier)
    {
        _pollutionMovementMultiplier = Mathf.Clamp(
            multiplier,
            0.0f,
            1.0f
        );
    }

    public void SetMovementLocked(bool locked)
    {
        MovementLocked = locked && !IsDead;

        if(MovementLocked)
            Velocity = Vector2.Zero;
    }

    public float RestoreHealth(float amount)
    {
        if(IsDead || amount <= 0.0f)
            return 0.0f;

        float previousHealth = CurrentHealth;
        CurrentHealth = Mathf.Min(
            Mathf.Max(MaxHealth, 1.0f),
            CurrentHealth + amount
        );
        float restored = CurrentHealth - previousHealth;

        if(restored <= 0.0f)
            return 0.0f;

        FloatingDamageNumber.SpawnHealing(
            this,
            restored,
            HealingNumberColor,
            DamageNumberOffset,
            DamageNumberRiseDistance,
            DamageNumberDuration,
            DamageNumberFontSize
        );
        EmitSignal(SignalName.HealthChanged, CurrentHealth, MaxHealth);
        return restored;
    }

    private void ApplyDamage(
        float amount,
        Node source,
        Vector2 hitPosition,
        Vector2 hitDirection,
        bool respectInvulnerability
    )
    {
        if (
            IsDead ||
            amount <= 0.0f ||
            (respectInvulnerability && _isInvulnerable)
        )
        {
            return;
        }

        CurrentHealth = Mathf.Max(
            0.0f,
            CurrentHealth - amount
        );

        FloatingDamageNumber.Spawn(
            this,
            amount,
            DamageNumberColor,
            DamageNumberOffset,
            DamageNumberRiseDistance,
            DamageNumberDuration,
            DamageNumberFontSize
        );

        EmitSignal(SignalName.HealthChanged, CurrentHealth, MaxHealth);

        GD.Print(
            $"Player HP: {CurrentHealth:F1}/{MaxHealth:F1}"
        );

        if (CurrentHealth <= 0.0f)
        {
            Die();
            return;
        }

        StartDamageFeedback();

        if (respectInvulnerability)
            StartInvulnerability();
    }

    private void StartDamageFeedback()
    {
        if (!GodotObject.IsInstanceValid(_bodySprite))
            return;

        ulong flashVersion = ++_damageFlashVersion;
        _bodySprite.Modulate = DamageFlashColor;

        SceneTreeTimer timer = GetTree().CreateTimer(
            Mathf.Max(DamageFlashDuration, 0.01f),
            true,
            false,
            true
        );

        timer.Timeout += () =>
        {
            if (
                flashVersion != _damageFlashVersion ||
                !GodotObject.IsInstanceValid(_bodySprite)
            )
            {
                return;
            }

            _bodySprite.Modulate = _normalSpriteColor;
        };
    }

    private void StartInvulnerability()
    {
        if (InvulnerabilityDuration <= 0.0f)
            return;

        _isInvulnerable = true;

        SceneTreeTimer timer = GetTree().CreateTimer(
            InvulnerabilityDuration,
            true,
            false,
            true
        );

        timer.Timeout += () =>
        {
            if (GodotObject.IsInstanceValid(this) && !IsDead)
                _isInvulnerable = false;
        };
    }

    private void Die()
    {
        if (IsDead)
            return;

        IsDead = true;
        MovementLocked = false;
        _isInvulnerable = true;
        Velocity = Vector2.Zero;
        EmitSignal(SignalName.Died);

        // 死亡动画与重置不应被绘画技能的慢动作继续拖慢。
        Engine.TimeScale = 1.0;

        RemoveFromGroup("player");
        SetPhysicsProcess(false);

        CollisionLayer = 0;
        CollisionMask = 0;

        foreach (
            Node node in
            FindChildren("*", "CollisionShape2D", true, false)
        )
        {
            if (node is CollisionShape2D collisionShape)
            {
                collisionShape.SetDeferred(
                    CollisionShape2D.PropertyName.Disabled,
                    true
                );
            }
        }

        Node weaponManager = GetNodeOrNull("WeaponManager");
        if (weaponManager != null)
            weaponManager.ProcessMode = ProcessModeEnum.Disabled;

        bool hasDeathAnimation =
            _bodySprite != null &&
            _bodySprite.SpriteFrames != null &&
            _bodySprite.SpriteFrames.HasAnimation(DeathAnimation);

        bool hasDeathFadeAnimation =
            _animationPlayer != null &&
            _animationPlayer.HasAnimation(DeathFadeAnimation);

        if (hasDeathAnimation)
        {
            _bodySprite.Modulate = _normalSpriteColor;
            _bodySprite.Play(DeathAnimation);
        }
        else
        {
            GD.PushWarning(
                $"Player 找不到死亡动画: {DeathAnimation}"
            );
        }

        if (hasDeathFadeAnimation)
        {
            _animationPlayer.Play(DeathFadeAnimation);
        }
        else
        {
            GD.PushWarning(
                $"Player 找不到死亡退场动画: {DeathFadeAnimation}"
            );
        }

        if (!hasDeathAnimation && !hasDeathFadeAnimation)
            ScheduleSceneReset();
    }

    private void OnAnimationFinished()
    {
        if (
            IsDead &&
            _bodySprite != null &&
            _bodySprite.Animation == DeathAnimation
        )
        {
            // 配置了淡出时由 AnimationPlayer 的完成信号负责重载，
            // 避免较短的逐帧死亡动画提前截断淡出效果。
            if (
                _animationPlayer == null ||
                !_animationPlayer.HasAnimation(DeathFadeAnimation)
            )
            {
                ScheduleSceneReset();
            }
        }
    }

    private void OnDeathFadeAnimationFinished(StringName animationName)
    {
        if (IsDead && animationName == DeathFadeAnimation)
            ScheduleSceneReset();
    }

    private void ScheduleSceneReset()
    {
        if (_sceneResetScheduled)
            return;

        _sceneResetScheduled = true;

        SceneTreeTimer timer = GetTree().CreateTimer(
            Mathf.Max(SceneResetDelay, 0.01f),
            true,
            false,
            true
        );
        timer.Timeout += ReloadCurrentScene;
    }

    private void ReloadCurrentScene()
    {
        if (!IsInsideTree())
            return;

        Engine.TimeScale = 1.0;
        Error error = GetTree().ReloadCurrentScene();

        if (error != Error.Ok)
            GD.PushError($"重新加载当前场景失败: {error}");
    }

    private void UpdateAnimation()
    {
        if (IsDead)
            return;

        string animationName =
            IdleAnimationPrefix + _facingSuffix;

        if (!Velocity.IsZeroApprox())
            animationName = WalkAnimationPrefix + _facingSuffix;

        if (
            _bodySprite == null ||
            _bodySprite.SpriteFrames == null
        )
        {
            return;
        }

        if (!_bodySprite.SpriteFrames.HasAnimation(animationName))
        {
            GD.PushWarning("Animation not found: " + animationName);
            return;
        }

        if (_bodySprite.Animation != animationName)
            _bodySprite.Play(animationName);
    }

    private static string VectorToFacingSuffix(Vector2 movement)
    {
        if (Mathf.Abs(movement.X) > Mathf.Abs(movement.Y))
            return movement.X > 0.0f ? "right" : "left";

        return movement.Y > 0.0f ? "front" : "back";
    }

    public override void _ExitTree()
    {
        if (GodotObject.IsInstanceValid(_bodySprite))
            _bodySprite.AnimationFinished -= OnAnimationFinished;

        if (GodotObject.IsInstanceValid(_animationPlayer))
            _animationPlayer.AnimationFinished -= OnDeathFadeAnimationFinished;
    }
}
