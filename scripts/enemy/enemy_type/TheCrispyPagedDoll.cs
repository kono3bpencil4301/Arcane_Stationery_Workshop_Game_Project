using Godot;

[GlobalClass]
public partial class TheCrispyPagedDoll : EnemyBase
{
    private enum ChargeState
    {
        Positioning,
        Observing,
        Windup,
        Charging,
        Recovering,
        Stunned
    }

    [ExportCategory("Crispy Paged Doll")]

    [Export]
    public StringName MoveAnimation = "default";

    [Export]
    public StringName ChargeAnimation = "dash";

    [Export]
    public StringName DizzyAnimation = "dizzy";

    /// <summary>
    /// 与玩家的水平距离小于该值时保持上一次朝向，避免在玩家附近反复翻转。
    /// </summary>
    [Export(PropertyHint.Range, "0,64,1")]
    public float FacingDeadZone = 8.0f;

    [ExportCategory("Charge State Machine")]

    /// <summary>脆页偶观察玩家时希望保持的中距离。</summary>
    [Export(PropertyHint.Range, "32,512,1")]
    public float ObservationDistance = 120.0f;

    [Export(PropertyHint.Range, "0,128,1")]
    public float ObservationDistanceTolerance = 20.0f;

    [Export(PropertyHint.Range, "0.05,10,0.05")]
    public float ObservationDuration = 0.75f;

    /// <summary>锁定冲锋方向后，折叠蓄力的持续时间。</summary>
    [Export(PropertyHint.Range, "0.05,3,0.05")]
    public float ChargeWindupDuration = 0.55f;

    [Export(PropertyHint.Range, "1,10,0.05")]
    public float ChargeSpeedMultiplier = 1.5f;

    [Export(PropertyHint.Range, "1,10,0.05")]
    public float ChargeDamageMultiplier = 2.0f;

    /// <summary>没有撞到任何物体时，直线冲锋最多持续多久。</summary>
    [Export(PropertyHint.Range, "0.1,5,0.05")]
    public float MaximumChargeDuration = 1.25f;

    [Export(PropertyHint.Range, "0,3,0.05")]
    public float ChargeRecoveryDuration = 0.35f;

    [Export(PropertyHint.Range, "0.05,5,0.05")]
    public float WallStunDuration = 1.2f;

    /// <summary>直线冲锋撞墙时扣除的自身生命值。</summary>
    [Export(PropertyHint.Range, "0,1000,0.5")]
    public float WallImpactDamage = 5.0f;

    [ExportCategory("Charge Feedback")]

    /// <summary>蓄力末尾的折叠比例，配合冲刺专用帧形成明显预警。</summary>
    [Export]
    public Vector2 WindupFoldScale = new(1.15f, 0.55f);

    [Export]
    public Color ExposedCoreColor = new(1.0f, 0.38f, 0.12f, 1.0f);

    [Export]
    public Vector2 ExposedCoreOffset = new(0.0f, -2.0f);

    [Export(PropertyHint.Range, "1,24,0.5")]
    public float ExposedCoreRadius = 5.0f;

    [Export]
    public Texture2D WarningIconTexture;

    [Export]
    public AudioStream WarningSFX;

    [Export]
    public Vector2 WarningIconOffset = new(0.0f, -38.0f);

    [Export]
    public Vector2 WarningIconScale = new(1.25f, 1.25f);

    [Export(PropertyHint.Range, "0,128,1")]
    public float WarningIconRiseDistance = 20.0f;

    [Export(PropertyHint.Range, "0.05,3,0.05")]
    public float WarningIconDuration = 0.5f;

    [ExportCategory("Death Explosion")]

    /// <summary>死亡时触发碎纸爆炸的概率，0 为关闭、1 为必定触发。</summary>
    [Export(PropertyHint.Range, "0,1,0.05")]
    public float ExplosionChance = 0.75f;

    [Export]
    public PackedScene PaperScrapScene;

    [Export(PropertyHint.Range, "3,8,1")]
    public int MinimumScrapCount = 5;

    [Export(PropertyHint.Range, "3,8,1")]
    public int MaximumScrapCount = 7;

    [Export(PropertyHint.Range, "0,1000,0.5")]
    public float ScrapDamage = 5.0f;

    [Export(PropertyHint.Range, "1,1000,1")]
    public float ScrapSpeed = 180.0f;

    [Export(PropertyHint.Range, "0.05,10,0.05")]
    public float ScrapLifetime = 1.5f;

    [Export(PropertyHint.Range, "0,64,1")]
    public float ScrapSpawnRadius = 12.0f;

    [ExportCategory("Damage Feedback")]

    [Export]
    public Color DamageFlashColor = new(1.0f, 0.25f, 0.25f, 1.0f);

    [Export(PropertyHint.Range, "0.01,1,0.01")]
    public double DamageFlashDuration = 0.05;

    /// <summary>
    /// 冲刺路线被墙或栅栏阻挡后，多久重新检查一次。
    /// </summary>
    [Export(PropertyHint.Range, "0.1,2,0.05")]
    public float BlockedChargeRetryDuration = 0.4f;

    private AnimatedSprite2D _animatedSprite;
    private Polygon2D _exposedCoreVisual;
    private ChargeState _state = ChargeState.Positioning;
    private float _stateTimeRemaining;
    private float _physicsDelta;
    private Vector2 _lockedChargeDirection = Vector2.Right;
    private Vector2 _normalSpriteScale = Vector2.One;
    private ulong _damageFlashVersion;
    private Color _normalSpriteColor = Colors.White;
    private bool _isFacingLeft;
    private bool _normalEnemyCollisionEnabled;
    private readonly RandomNumberGenerator _explosionRandom = new();
    private bool _deathExplosionResolved;

    public TheCrispyPagedDoll()
    {
        // 脆页偶比墨团更脆但移动更快；场景 Inspector 中保存的值仍可覆盖它们。
        MaxHealth = 30f;
        MoveSpeed = 80f;
        MinimumInkCoinReward = 2;
        MaximumInkCoinReward = 5;
    }

    public override void _Ready()
    {
        base._Ready();

        _explosionRandom.Randomize();

        _animatedSprite =
            GetNodeOrNull<AnimatedSprite2D>("AnimatedSprite2D");
        _normalEnemyCollisionEnabled = GetCollisionMaskValue(3);

        if (_animatedSprite != null)
        {
            // 固定保存正常状态，避免连续受击或状态切换累积缩放、颜色。
            _normalSpriteColor = _animatedSprite.Modulate;
            _normalSpriteScale = _animatedSprite.Scale;
            _isFacingLeft = _animatedSprite.FlipH;
        }

        CreateExposedCoreVisual();
        EnterPositioning();
    }

    public override void _PhysicsProcess(double delta)
    {
        _physicsDelta = (float)delta;
        base._PhysicsProcess(delta);
    }

    protected override void MoveToTarget()
    {
        switch (_state)
        {
            case ChargeState.Positioning:
                UpdatePositioning();
                break;

            case ChargeState.Observing:
                UpdateObserving();
                break;

            case ChargeState.Windup:
                UpdateWindup();
                break;

            case ChargeState.Charging:
                UpdateCharging();
                break;

            case ChargeState.Recovering:
                UpdateRecovery();
                break;

            case ChargeState.Stunned:
                UpdateStun();
                break;
        }
    }

    private void UpdatePositioning()
    {
        MoveAtObservationDistance();

        float tolerance = Mathf.Max(ObservationDistanceTolerance, 0.0f);
        float distance = GlobalPosition.DistanceTo(Target.GlobalPosition);

        if (Mathf.Abs(distance - ObservationDistance) <= tolerance)
            EnterObserving();
    }

    private void UpdateObserving()
    {
        // 观察期间仍会小幅前后调整，尽量维持中距离。
        MoveAtObservationDistance();
        _stateTimeRemaining -= _physicsDelta;

        if (_stateTimeRemaining > 0.0f)
            return;

        if (HasClearChargePathToPlayer())
        {
            EnterWindup();
            return;
        }

        // 玩家一开始就在墙或栅栏后方，暂时不冲刺。
        _stateTimeRemaining = Mathf.Max(
            BlockedChargeRetryDuration,
            0.1f
        );
    }

    private void MoveAtObservationDistance()
    {
        Vector2 toTarget = Target.GlobalPosition - GlobalPosition;
        float distance = toTarget.Length();
        float targetDistance = Mathf.Max(ObservationDistance, 0.0f);
        float tolerance = Mathf.Max(ObservationDistanceTolerance, 0.0f);
        Vector2 rangeDirection = Vector2.Zero;

        if (!toTarget.IsZeroApprox())
        {
            if (distance > targetDistance + tolerance)
                rangeDirection = toTarget.Normalized();
            else if (distance < Mathf.Max(targetDistance - tolerance, 0.0f))
                rangeDirection = -toTarget.Normalized();
        }

        Vector2 movementDirection =
            rangeDirection +
            CalculateEnemySeparation() * Mathf.Max(SeparationStrength, 0.0f);

        Vector2 movementVelocity = Vector2.Zero;

        if (!movementDirection.IsZeroApprox())
        {
            movementVelocity =
                movementDirection.Normalized() *
                MoveSpeed *
                GetSkillSpeedMultiplier();
        }

        Velocity = movementVelocity + ExternalPushVelocity;

        if (!Velocity.IsZeroApprox())
            MoveAndSlide();

        UpdateFacingDirection();
    }

    private void EnterObserving()
    {
        _state = ChargeState.Observing;
        _stateTimeRemaining = Mathf.Max(ObservationDuration, 0.05f);
    }

    private void EnterWindup()
    {
        _state = ChargeState.Windup;
        _stateTimeRemaining = Mathf.Max(ChargeWindupDuration, 0.05f);
        Velocity = Vector2.Zero;

        Vector2 toTarget = Target.GlobalPosition - GlobalPosition;

        if (!toTarget.IsZeroApprox())
            _lockedChargeDirection = toTarget.Normalized();

        SetFacingFromDirection(_lockedChargeDirection);
        HideExposedCore();
        SpawnChargeWarning();

        if (WarningSFX != null)
            GetNodeOrNull<AudioManager>("/root/AudioManager")?.PlaySFX(WarningSFX);

        if (HasAnimation(ChargeAnimation))
        {
            _animatedSprite.Play(ChargeAnimation);
            _animatedSprite.Pause();
            _animatedSprite.Frame = 0;
            _animatedSprite.FrameProgress = 0.0f;
        }
    }

    private void UpdateWindup()
    {
        Velocity = Vector2.Zero;
        _stateTimeRemaining -= _physicsDelta;

        if (GodotObject.IsInstanceValid(_animatedSprite))
        {
            float duration = Mathf.Max(ChargeWindupDuration, 0.05f);
            float progress = Mathf.Clamp(
                1.0f - _stateTimeRemaining / duration,
                0.0f,
                1.0f
            );
            Vector2 foldedScale =
                _normalSpriteScale * WindupFoldScale;
            _animatedSprite.Scale =
                _normalSpriteScale.Lerp(foldedScale, progress);
        }

        if (_stateTimeRemaining <= 0.0f)
            EnterCharging();
    }


    private void EnterCharging()
    {
        _state = ChargeState.Charging;
        _stateTimeRemaining = Mathf.Max(MaximumChargeDuration, 0.1f);
        ResetContactAttackCooldown();

        // 冲锋期间忽略其他敌人，防止碰撞滑动改变已锁定的直线路径。
        SetCollisionMaskValue(3, false);

        if (GodotObject.IsInstanceValid(_animatedSprite))
        {
            _animatedSprite.Scale = _normalSpriteScale;

            if (HasAnimation(ChargeAnimation))
                _animatedSprite.Play(ChargeAnimation);
        }
    }

    private void UpdateCharging()
    {
        float chargeSpeed =
            MoveSpeed *
            Mathf.Max(ChargeSpeedMultiplier, 1.0f) *
            GetSkillSpeedMultiplier();
        Velocity = _lockedChargeDirection * chargeSpeed;
        _stateTimeRemaining -= _physicsDelta;

        KinematicCollision2D collision =
            MoveAndCollide(Velocity * _physicsDelta);

        if (collision != null)
        {
            Node collider = collision.GetCollider() as Node;

            // 玩家伤害由 EnemyBase 的统一接触伤害入口处理。
            if (IsPlayerCollider(collider))
                return;

            if (IsWallCollider(collider))
            {
                ApplyWallImpactDamage();

                if (!IsDead)
                    EnterStunned();

                return;
            }

            // 未知实体也会结束冲锋，避免持续卡在同一碰撞面上。
            EnterRecovering();
            return;
        }

        if (_stateTimeRemaining <= 0.0f)
            EnterRecovering();
    }

    protected override bool CanContactAttack()
    {
        return _state == ChargeState.Charging;
    }

    protected override float GetContactDamageMultiplier()
    {
        return Mathf.Max(ChargeDamageMultiplier, 1.0f);
    }

    protected override Vector2 GetContactHitDirection()
    {
        return _lockedChargeDirection;
    }

    protected override void OnContactAttackLanded()
    {
        if (_state == ChargeState.Charging)
            EnterRecovering();
    }

    private void EnterRecovering()
    {
        RestoreEnemyCollisionMask();
        _state = ChargeState.Recovering;
        _stateTimeRemaining = Mathf.Max(ChargeRecoveryDuration, 0.0f);
        Velocity = Vector2.Zero;
        RestoreMovementVisual();
        UpdateFacingDirection();

        if (_stateTimeRemaining <= 0.0f)
            EnterPositioning();
    }

    private void UpdateRecovery()
    {
        Velocity = Vector2.Zero;
        UpdateFacingDirection();
        _stateTimeRemaining -= _physicsDelta;

        if (_stateTimeRemaining <= 0.0f)
            EnterPositioning();
    }

    private void EnterStunned()
    {
        RestoreEnemyCollisionMask();
        _state = ChargeState.Stunned;
        _stateTimeRemaining = Mathf.Max(WallStunDuration, 0.05f);
        Velocity = Vector2.Zero;
        PlayStunnedVisual();

        if (GodotObject.IsInstanceValid(_exposedCoreVisual))
            _exposedCoreVisual.Visible = true;
    }

    private void UpdateStun()
    {
        Velocity = Vector2.Zero;
        _stateTimeRemaining -= _physicsDelta;

        if (_stateTimeRemaining <= 0.0f)
            EnterPositioning();
    }

    private void EnterPositioning()
    {
        RestoreEnemyCollisionMask();
        _state = ChargeState.Positioning;
        _stateTimeRemaining = 0.0f;
        RestoreMovementVisual();
        HideExposedCore();
    }

    /// <summary>
    /// 只翻转视觉节点，不修改 CharacterBody2D 与碰撞体的 Transform。
    /// Windup 与 Charging 不会调用此方法，因此冲锋途中不会反转。
    /// </summary>
    private void UpdateFacingDirection()
    {
        if (
            !GodotObject.IsInstanceValid(_animatedSprite) ||
            !GodotObject.IsInstanceValid(Target)
        )
        {
            return;
        }

        float horizontalDistance =
            Target.GlobalPosition.X - GlobalPosition.X;

        if (Mathf.Abs(horizontalDistance) <= Mathf.Max(FacingDeadZone, 0.0f))
            return;

        SetFacingFromDirection(new Vector2(horizontalDistance, 0.0f));
    }

    private void SetFacingFromDirection(Vector2 direction)
    {
        if (
            !GodotObject.IsInstanceValid(_animatedSprite) ||
            Mathf.Abs(direction.X) <= 0.001f
        )
        {
            return;
        }

        bool shouldFaceLeft = direction.X < 0.0f;

        if (shouldFaceLeft == _isFacingLeft)
            return;

        _isFacingLeft = shouldFaceLeft;
        _animatedSprite.FlipH = _isFacingLeft;
    }

    private bool HasAnimation(StringName animation)
    {
        return
            GodotObject.IsInstanceValid(_animatedSprite) &&
            _animatedSprite.SpriteFrames != null &&
            _animatedSprite.SpriteFrames.HasAnimation(animation);
    }

    private void RestoreMovementVisual()
    {
        if (!GodotObject.IsInstanceValid(_animatedSprite))
            return;

        RestoreSpriteProperties();

        if (HasAnimation(MoveAnimation))
        {
            _animatedSprite.Play(MoveAnimation);
        }
        else
        {
            GD.PushWarning(
                $"{Name} 找不到移动动画: {MoveAnimation}"
            );
        }
    }

    private void PlayStunnedVisual()
    {
        if (!GodotObject.IsInstanceValid(_animatedSprite))
            return;

        RestoreSpriteProperties();

        if (HasAnimation(DizzyAnimation))
        {
            _animatedSprite.Play(DizzyAnimation);
        }
        else
        {
            GD.PushWarning(
                $"{Name} 找不到眩晕动画: {DizzyAnimation}"
            );
        }
    }

    private void RestoreSpriteProperties()
    {
        _animatedSprite.Scale = _normalSpriteScale;
        _animatedSprite.SpeedScale = 1.0f;
        _animatedSprite.Modulate = _normalSpriteColor;
    }

    private void ApplyWallImpactDamage()
    {
        float damage = Mathf.Max(WallImpactDamage, 0.0f);

        if (damage <= 0.0f)
            return;

        TakeDamage(
            damage,
            this,
            GlobalPosition,
            -_lockedChargeDirection
        );
    }

    private void SpawnChargeWarning()
    {
        if (
            WarningIconTexture == null ||
            !IsInsideTree()
        )
        {
            return;
        }

        Node parent = GetTree().CurrentScene ?? GetParent();

        if (parent == null)
            return;

        Sprite2D warningIcon = new()
        {
            Name = "ChargeWarningIcon",
            Texture = WarningIconTexture,
            Scale = WarningIconScale,
            ZIndex = 100,
            ZAsRelative = false,
            ProcessMode = ProcessModeEnum.Always
        };
        parent.AddChild(warningIcon);
        warningIcon.GlobalPosition = GlobalPosition + WarningIconOffset;

        float duration = Mathf.Max(WarningIconDuration, 0.05f);
        Tween tween = warningIcon.CreateTween();
        tween.SetParallel(true);
        tween.SetIgnoreTimeScale(true);
        tween.TweenProperty(
                warningIcon,
                "position:y",
                warningIcon.Position.Y -
                    Mathf.Max(WarningIconRiseDistance, 0.0f),
                duration
            )
            .SetTrans(Tween.TransitionType.Quad)
            .SetEase(Tween.EaseType.Out);
        tween.TweenProperty(
                warningIcon,
                "modulate:a",
                0.0f,
                duration * 0.35f
            )
            .SetDelay(duration * 0.65f);
        tween.Finished += warningIcon.QueueFree;
    }

    private void CreateExposedCoreVisual()
    {
        float radius = Mathf.Max(ExposedCoreRadius, 1.0f);
        _exposedCoreVisual = new Polygon2D
        {
            Name = "ExposedPaperCore",
            Polygon = new Vector2[]
            {
                new(0.0f, -radius),
                new(radius, 0.0f),
                new(0.0f, radius),
                new(-radius, 0.0f)
            },
            Position = ExposedCoreOffset,
            Color = ExposedCoreColor,
            ZIndex = 5,
            Visible = false
        };
        AddChild(_exposedCoreVisual);
    }

    private void HideExposedCore()
    {
        if (GodotObject.IsInstanceValid(_exposedCoreVisual))
            _exposedCoreVisual.Visible = false;
    }

    private void RestoreEnemyCollisionMask()
    {
        SetCollisionMaskValue(3, _normalEnemyCollisionEnabled);
    }

    private static bool IsPlayerCollider(Node collider)
    {
        return
            collider != null &&
            (
                collider is Player ||
                collider.IsInGroup("player") ||
                collider.IsInGroup("Player")
            );
    }

    private static bool IsWallCollider(Node collider)
    {
        if (collider is StaticBody2D || collider is TileMapLayer)
            return true;

        return
            collider is CollisionObject2D collisionObject &&
            collisionObject.GetCollisionLayerValue(1);
    }

    protected override void FlashDamage()
    {
        if (!GodotObject.IsInstanceValid(_animatedSprite))
        {
            _animatedSprite =
                GetNodeOrNull<AnimatedSprite2D>("AnimatedSprite2D");
        }

        if (_animatedSprite == null)
            return;

        ulong flashVersion = ++_damageFlashVersion;
        _animatedSprite.Modulate = DamageFlashColor;

        GetTree()
            .CreateTimer(Mathf.Max(DamageFlashDuration, 0.01))
            .Timeout += () =>
            {
                // 连续受击时只允许最后一次计时恢复颜色。
                if (
                    flashVersion != _damageFlashVersion ||
                    !GodotObject.IsInstanceValid(_animatedSprite)
                )
                {
                    return;
                }

                _animatedSprite.Modulate = _normalSpriteColor;
            };
    }

    protected override void Die()
    {
        RestoreEnemyCollisionMask();
        HideExposedCore();
        TrySpawnDeathExplosion();
        base.Die();
    }

    private void TrySpawnDeathExplosion()
    {
        if (_deathExplosionResolved)
            return;

        _deathExplosionResolved = true;

        if (
            PaperScrapScene == null ||
            _explosionRandom.Randf() >
                Mathf.Clamp(ExplosionChance, 0.0f, 1.0f)
        )
        {
            return;
        }

        Node parent = GetTree().CurrentScene ?? GetParent();

        if (parent == null)
            return;

        int minimumCount = Mathf.Clamp(MinimumScrapCount, 3, 8);
        int maximumCount = Mathf.Clamp(MaximumScrapCount, 3, 8);

        if (minimumCount > maximumCount)
            (minimumCount, maximumCount) = (maximumCount, minimumCount);

        int scrapCount = _explosionRandom.RandiRange(
            minimumCount,
            maximumCount
        );
        float angleStep = Mathf.Tau / scrapCount;
        float startAngle = _explosionRandom.RandfRange(0.0f, Mathf.Tau);
        float spawnRadius = Mathf.Max(ScrapSpawnRadius, 0.0f);

        for (int index = 0; index < scrapCount; index++)
        {
            float angleJitter = _explosionRandom.RandfRange(
                -angleStep * 0.12f,
                angleStep * 0.12f
            );
            Vector2 direction = Vector2.Right.Rotated(
                startAngle + angleStep * index + angleJitter
            );
            Node instance = PaperScrapScene.Instantiate();

            if (instance is not CrispyPaperScrap scrap)
            {
                GD.PushWarning(
                    $"{Name} 的 PaperScrapScene 根节点必须使用 CrispyPaperScrap。"
                );
                instance.QueueFree();
                continue;
            }

            scrap.Configure(
                direction,
                this,
                ScrapDamage,
                ScrapSpeed,
                ScrapLifetime
            );
            Vector2 scrapSpawnPosition =
                GlobalPosition + direction * spawnRadius;

            // Die 可能由 Area2D.BodyEntered 等物理查询回调触发。
            // 查询刷新期间不能把带碰撞形状的 CharacterBody2D 加入场景，
            // 因此将 AddChild 延迟，并在入树后再设置全局坐标。
            scrap.TreeEntered += () =>
            {
                if (GodotObject.IsInstanceValid(scrap))
                    scrap.GlobalPosition = scrapSpawnPosition;
            };
            parent.CallDeferred(Node.MethodName.AddChild, scrap);
        }
    }

    private bool HasClearChargePathToPlayer()
    {
        if (!GodotObject.IsInstanceValid(Target) || GetWorld2D() == null)
        {
            return false;
        }

        PhysicsRayQueryParameters2D query = PhysicsRayQueryParameters2D.Create(GlobalPosition, Target.GlobalPosition);
        query.CollisionMask = 1u;
        query.CollideWithBodies = true;
        query.CollideWithAreas = false;
        query.Exclude = new Godot.Collections.Array<Rid>
        {
            GetRid()
        };

        Godot.Collections.Dictionary result = GetWorld2D()
                .DirectSpaceState
                .IntersectRay(query);

        return result.Count == 0;

    }
}
