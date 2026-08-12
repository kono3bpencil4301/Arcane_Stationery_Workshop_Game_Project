using Godot;
using System.Collections.Generic;

/// <summary>
/// 黑斑菌团。出生后先分散到 World 墙角附近，随后保持休眠；
/// 玩家进入触发范围后惊醒并追击；
/// 惊醒后周期性向四周释放可被橡皮擦除的带毒孢子。
/// </summary>
[GlobalClass]
public partial class TheMoldSpotFungus : EnemyBase
{
    [ExportCategory("Birth Settlement")]

    /// <summary>出生后是否先寻找 World 层的墙角停靠点。</summary>
    [Export]
    public bool SettleNearWorldOnSpawn { get; set; } = true;

    [Export(PropertyHint.Range, "1,1000,1")]
    public float SettlementMoveSpeed { get; set; } = 80.0f;

    /// <summary>停靠点与 World 碰撞边界之间保留的距离。</summary>
    [Export(PropertyHint.Range, "4,128,1")]
    public float SettlementWallClearance { get; set; } = 24.0f;

    [Export(PropertyHint.Range, "1,64,1")]
    public float SettlementArrivalDistance { get; set; } = 8.0f;

    /// <summary>同一墙角沿相邻墙边生成多个停靠槽，避免菌团再次堆在角点。</summary>
    [Export(PropertyHint.Range, "1,8,1")]
    public int SettlementSlotsPerBoundaryEnd { get; set; } = 3;

    [Export(PropertyHint.Range, "8,128,1")]
    public float SettlementSlotSpacing { get; set; } = 34.0f;

    [Export(PropertyHint.Range, "0,3,0.05")]
    public float SettlementSeparationStrength { get; set; } = 1.35f;

    /// <summary>房间没有可识别的 World 边界时，至少先离开重叠出生点。</summary>
    [Export(PropertyHint.Range, "16,512,1")]
    public float SettlementFallbackDistance { get; set; } = 96.0f;

    [ExportCategory("Awakening")]

    [Export(PropertyHint.Range, "16,1024,1")]
    public float AwakeningRadius { get; set; } = 180.0f;

    [Export]
    public Texture2D WarningIconTexture { get; set; }

    [Export]
    public AudioStream WarningSFX { get; set; }

    [Export]
    public Vector2 WarningIconOffset { get; set; } = new(0.0f, -28.0f);

    [Export]
    public Vector2 WarningIconScale { get; set; } = new(1.1f, 1.1f);

    [Export(PropertyHint.Range, "0,128,1")]
    public float WarningIconRiseDistance { get; set; } = 20.0f;

    [Export(PropertyHint.Range, "0.05,3,0.05")]
    public float WarningIconDuration { get; set; } = 0.65f;

    [ExportCategory("Spore Pulse")]

    [Export]
    public PackedScene SporeScene { get; set; }

    [Export(PropertyHint.Range, "1,32,1")]
    public int SporeCount { get; set; } = 8;

    [Export(PropertyHint.Range, "0.1,30,0.1")]
    public float SporePulseInterval { get; set; } = 3.0f;

    [Export(PropertyHint.Range, "0,10,0.05")]
    public float InitialSporePulseDelay { get; set; } = 0.8f;

    [Export(PropertyHint.Range, "0,64,1")]
    public float SporeSpawnRadius { get; set; } = 18.0f;

    [Export(PropertyHint.Range, "1,1000,1")]
    public float SporeSpeed { get; set; } = 140.0f;

    [Export(PropertyHint.Range, "0.05,10,0.05")]
    public float SporeLifetime { get; set; } = 3.0f;

    [Export(PropertyHint.Range, "0,1000,0.5")]
    public float SporeDamage { get; set; } = 10.0f;

    [Export(PropertyHint.Range, "0.1,30,0.1")]
    public float PoisonDuration { get; set; } = 5.0f;

    [Export(PropertyHint.Range, "0.1,10,0.1")]
    public float PoisonTickInterval { get; set; } = 1.0f;

    [Export(PropertyHint.Range, "0,100,0.5")]
    public float PoisonDamagePerTick { get; set; } = 2.0f;

    [ExportCategory("Pollution Regeneration")]

    [Export(PropertyHint.Range, "0,512,1")]
    public float PollutionDetectionRadius { get; set; } = 42.0f;

    [Export(PropertyHint.Range, "0.1,10,0.1")]
    public float RegenerationInterval { get; set; } = 1.0f;

    [Export(PropertyHint.Range, "0,100,0.5")]
    public float RegenerationPerTick { get; set; } = 1.0f;

    [Export]
    public Color RegenerationNumberColor { get; set; } =
        new(0.65f, 0.22f, 0.9f, 1.0f);

    [ExportCategory("Visuals")]

    [Export]
    public StringName MoveAnimation { get; set; } = "default";

    [Export(PropertyHint.Range, "0,64,1")]
    public float FacingDeadZone { get; set; } = 8.0f;

    [Export]
    public Color DamageFlashColor { get; set; } =
        new(1.0f, 0.35f, 0.35f, 1.0f);

    [Export(PropertyHint.Range, "0.01,1,0.01")]
    public double DamageFlashDuration { get; set; } = 0.08;

    private AnimatedSprite2D _animatedSprite;
    private InkPollutionField _pollutionField;
    private readonly List<Vector2> _settlementCandidates = new();
    private Color _normalSpriteColor = Colors.White;
    private bool _isFacingLeft;
    private bool _isAwake;
    private bool _isSettled;
    private bool _settlementTargetResolved;
    private Vector2 _settlementTarget;
    private float _sporePulseTimeRemaining;
    private float _regenerationAccumulator;
    private float _pulseAngleOffset;
    private ulong _damageFlashVersion;


    public TheMoldSpotFungus()
    {
        MaxHealth = 40.0f;
        MoveSpeed = 45.0f;
        ContactDamage = 10.0f;
        ContactAttackRange = 28.0f;
        TargetStopDistance = 26.0f;
        MinimumInkCoinReward = 2;
        MaximumInkCoinReward = 4;
    }

    public override void _Ready()
    {
        base._Ready();

        _animatedSprite =
            GetNodeOrNull<AnimatedSprite2D>("AnimatedSprite2D");

        if (GodotObject.IsInstanceValid(_animatedSprite))
        {
            _normalSpriteColor = _animatedSprite.Modulate;
            _isFacingLeft = _animatedSprite.FlipH;

            if (
                _animatedSprite.SpriteFrames != null &&
                _animatedSprite.SpriteFrames.HasAnimation(MoveAnimation)
            )
            {
                _animatedSprite.Play(MoveAnimation);
            }
        }

        _sporePulseTimeRemaining =
            Mathf.Max(InitialSporePulseDelay, 0.0f);
        _isSettled = !SettleNearWorldOnSpawn;
        _settlementTargetResolved = false;
        ResolvePollutionField();
    }

    public override void _PhysicsProcess(double delta)
    {
        if (IsDead)
            return;

        float physicsDelta = (float)delta;

        if (!_isSettled)
        {
            if (!_settlementTargetResolved)
                ResolveSettlementTarget();

            MoveToSettlementTarget();
            UpdatePollutionRegeneration(physicsDelta);
            return;
        }

        TryAwaken();
        base._PhysicsProcess(delta);

        if (IsDead)
            return;

        if (_isAwake)
            UpdateSporePulse(physicsDelta);

        UpdatePollutionRegeneration(physicsDelta);
    }

    protected override void MoveToTarget()
    {
        if (!_isSettled || !_isAwake)
        {
            Velocity = ExternalPushVelocity;

            if (!Velocity.IsZeroApprox())
                MoveAndSlide();

            return;
        }

        base.MoveToTarget();
        UpdateFacingDirection();
    }

    protected override bool CanContactAttack()
    {
        return _isSettled && _isAwake && base.CanContactAttack();
    }

    public override void TakeDamage(
        float damage,
        Node source,
        Vector2 hitPosition,
        Vector2 hitDirection
    )
    {
        base.TakeDamage(damage, source, hitPosition, hitDirection);

        if (!IsDead && damage > 0.0f)
            Awaken();
    }

    private void TryAwaken()
    {
        if (
            !_isSettled ||
            _isAwake ||
            !GodotObject.IsInstanceValid(Target)
        )
        {
            return;
        }

        float radius = Mathf.Max(AwakeningRadius, 1.0f);

        if (
            GlobalPosition.DistanceSquaredTo(Target.GlobalPosition) <=
            radius * radius
        )
        {
            Awaken();

        }
    }

    private void Awaken()
    {
        if (_isAwake || IsDead)
            return;

        // 玩家在途中直接攻击时立即中断停靠，进入正常战斗状态。
        _isSettled = true;
        _settlementTargetResolved = false;
        _isAwake = true;
        _sporePulseTimeRemaining =
            Mathf.Max(InitialSporePulseDelay, 0.0f);
        SpawnAwakeningWarning();

        if(WarningSFX != null)
        {
            GetNodeOrNull<AudioManager>("/root/AudioManager")?
                .PlaySFX(WarningSFX);
        }
    }


    public void ForceAwaken()
    {
        if (IsDead)
            return;

        Awaken();
    }


    private void ResolveSettlementTarget()
    {
        _settlementCandidates.Clear();

        Node currentScene = GetTree()?.CurrentScene;
        Node worldBounds = currentScene?.FindChild(
            "WorldBounds",
            true,
            false
        );

        // 程序化房间没有独立的 WorldBounds 节点，墙体碰撞来自 TileMapLayer。
        // 优先读取当前实际铺设区域，避免退回出生点附近的短距离分散逻辑。
        if (currentScene != null)
            CollectTileMapBoundaryCandidates(currentScene);

        if (_settlementCandidates.Count == 0 && worldBounds != null)
            CollectWorldBoundaryCandidates(worldBounds);

        // 其他房间若没有 WorldBounds 约定，则退回扫描所有 World 碰撞节点。
        if (_settlementCandidates.Count == 0 && currentScene != null)
            CollectWorldBoundaryCandidates(currentScene);

        if (_settlementCandidates.Count == 0)
        {
            float fallbackAngle =
                (GetInstanceId() % 360) * Mathf.Pi / 180.0f;
            _settlementTarget = GlobalPosition +
                Vector2.FromAngle(fallbackAngle) *
                Mathf.Max(SettlementFallbackDistance, 16.0f);
            _settlementTargetResolved = true;

            GD.PushWarning(
                $"{Name} 找不到 World 墙角，改用出生点分散停靠。"
            );
            return;
        }

        _settlementTarget = SelectLeastOccupiedSettlementTarget();
        _settlementTargetResolved = true;
    }

    private void CollectTileMapBoundaryCandidates(Node searchRoot)
    {
        TileMapLayer groundLayer = searchRoot.FindChild(
            "GroundTileMapLayer",
            true,
            false
        ) as TileMapLayer;

        if (
            !GodotObject.IsInstanceValid(groundLayer) ||
            groundLayer.TileSet == null
        )
        {
            return;
        }

        Rect2I usedRect;
        DungeonGenerator dungeonGenerator = searchRoot.FindChild(
            "DungeonGenerator",
            true,
            false
        ) as DungeonGenerator;

        if (
            GodotObject.IsInstanceValid(dungeonGenerator) &&
            dungeonGenerator.TryGetRoomAtGlobalPosition(
                GlobalPosition,
                out DungeonRoomData spawnRoom
            )
        )
        {
            // 多房间地牢必须使用菌团实际出生的房间边界，不能把整张
            // GroundTileMapLayer 的外接矩形误认为一个巨大房间。
            usedRect = spawnRoom.Bounds;
        }
        else
        {
            usedRect = groundLayer.GetUsedRect();
        }

        if (usedRect.Size.X < 3 || usedRect.Size.Y < 3)
            return;

        Vector2I topLeftCell = usedRect.Position;
        Vector2I bottomRightCell = new(
            usedRect.Position.X + usedRect.Size.X - 1,
            usedRect.Position.Y + usedRect.Size.Y - 1
        );
        Vector2 tileSize = new(
            groundLayer.TileSet.TileSize.X,
            groundLayer.TileSet.TileSize.Y
        );
        Vector2 halfTileSize = tileSize * 0.5f;
        Vector2 topLeftCenter = groundLayer.MapToLocal(topLeftCell);
        Vector2 bottomRightCenter = groundLayer.MapToLocal(bottomRightCell);

        // 边界取墙体朝向房间内部的边缘，而不是墙瓦片中心。
        float left = topLeftCenter.X + halfTileSize.X;
        float top = topLeftCenter.Y + halfTileSize.Y;
        float right = bottomRightCenter.X - halfTileSize.X;
        float bottom = bottomRightCenter.Y - halfTileSize.Y;

        if (right <= left || bottom <= top)
            return;

        Vector2 topLeft = groundLayer.ToGlobal(new Vector2(left, top));
        Vector2 topRight = groundLayer.ToGlobal(new Vector2(right, top));
        Vector2 bottomLeft = groundLayer.ToGlobal(new Vector2(left, bottom));
        Vector2 bottomRight = groundLayer.ToGlobal(new Vector2(right, bottom));

        AddSegmentSettlementCandidates(topLeft, topRight);
        AddSegmentSettlementCandidates(topLeft, bottomLeft);
        AddSegmentSettlementCandidates(topRight, bottomRight);
        AddSegmentSettlementCandidates(bottomLeft, bottomRight);
    }

    private void CollectWorldBoundaryCandidates(Node searchRoot)
    {
        foreach (
            Node node in searchRoot.FindChildren(
                "*",
                "CollisionShape2D",
                true,
                false
            )
        )
        {
            if (
                node is not CollisionShape2D collisionShape ||
                collisionShape.Disabled ||
                collisionShape.GetParent() is not CollisionObject2D owner ||
                !owner.GetCollisionLayerValue(1)
            )
            {
                continue;
            }

            switch (collisionShape.Shape)
            {
                case SegmentShape2D segmentShape:
                    AddSegmentSettlementCandidates(
                        collisionShape.ToGlobal(segmentShape.A),
                        collisionShape.ToGlobal(segmentShape.B)
                    );
                    break;

                case RectangleShape2D rectangleShape:
                    Vector2 halfSize = rectangleShape.Size * 0.5f;
                    AddWallPointCandidate(
                        collisionShape.ToGlobal(new Vector2(-halfSize.X, -halfSize.Y))
                    );
                    AddWallPointCandidate(
                        collisionShape.ToGlobal(new Vector2(halfSize.X, -halfSize.Y))
                    );
                    AddWallPointCandidate(
                        collisionShape.ToGlobal(new Vector2(halfSize.X, halfSize.Y))
                    );
                    AddWallPointCandidate(
                        collisionShape.ToGlobal(new Vector2(-halfSize.X, halfSize.Y))
                    );
                    break;
            }
        }
    }

    private void AddSegmentSettlementCandidates(Vector2 from, Vector2 to)
    {
        Vector2 segment = to - from;
        float length = segment.Length();

        if (length <= 0.001f)
            return;

        Vector2 along = segment / length;
        int slotCount = Mathf.Max(SettlementSlotsPerBoundaryEnd, 1);
        float spacing = Mathf.Max(SettlementSlotSpacing, 1.0f);
        float maximumOffset = length * 0.4f;

        for (int slot = 0; slot < slotCount; slot++)
        {
            float offset = Mathf.Min(slot * spacing, maximumOffset);
            AddWallPointCandidate(from + along * offset);
            AddWallPointCandidate(to - along * offset);
        }
    }

    private void AddWallPointCandidate(Vector2 wallPoint)
    {
        Vector2 inwardDirection = wallPoint.DirectionTo(GlobalPosition);

        if (inwardDirection.IsZeroApprox())
            inwardDirection = Vector2.One.Normalized();

        Vector2 candidate = wallPoint +
            inwardDirection * Mathf.Max(SettlementWallClearance, 4.0f);
        float minimumDuplicateDistanceSquared = 4.0f * 4.0f;

        foreach (Vector2 existing in _settlementCandidates)
        {
            if (
                existing.DistanceSquaredTo(candidate) <=
                minimumDuplicateDistanceSquared
            )
            {
                return;
            }
        }

        _settlementCandidates.Add(candidate);
    }

    private Vector2 SelectLeastOccupiedSettlementTarget()
    {
        Vector2 bestCandidate = _settlementCandidates[0];
        float bestScore = float.NegativeInfinity;

        foreach (Vector2 candidate in _settlementCandidates)
        {
            float minimumOtherDistanceSquared = float.PositiveInfinity;
            bool foundOtherFungus = false;

            foreach (Node node in GetTree().GetNodesInGroup("enemy"))
            {
                if (
                    node == this ||
                    node is not TheMoldSpotFungus other ||
                    !GodotObject.IsInstanceValid(other)
                )
                {
                    continue;
                }

                Vector2 occupiedPosition =
                    !other._isAwake && other._settlementTargetResolved
                        ? other._settlementTarget
                        : other.GlobalPosition;
                minimumOtherDistanceSquared = Mathf.Min(
                    minimumOtherDistanceSquared,
                    candidate.DistanceSquaredTo(occupiedPosition)
                );
                foundOtherFungus = true;
            }

            float travelDistanceSquared =
                GlobalPosition.DistanceSquaredTo(candidate);
            float score = foundOtherFungus
                ? minimumOtherDistanceSquared - travelDistanceSquared * 0.05f
                : -travelDistanceSquared;

            if (score <= bestScore)
                continue;

            bestScore = score;
            bestCandidate = candidate;
        }

        return bestCandidate;
    }

    private void MoveToSettlementTarget()
    {
        if (!_settlementTargetResolved)
            return;

        Vector2 toTarget = _settlementTarget - GlobalPosition;
        float arrivalDistance =
            Mathf.Max(SettlementArrivalDistance, 1.0f);

        if (toTarget.LengthSquared() <= arrivalDistance * arrivalDistance)
        {
            CompleteSettlement();
            return;
        }

        Vector2 movementDirection = toTarget.Normalized() +
            CalculateEnemySeparation() *
            Mathf.Max(SettlementSeparationStrength, 0.0f);

        if (movementDirection.IsZeroApprox())
            movementDirection = toTarget.Normalized();

        Velocity = movementDirection.Normalized() *
            Mathf.Max(SettlementMoveSpeed, 1.0f) *
            GetSkillSpeedMultiplier() +
            ExternalPushVelocity;

        MoveAndSlide();
        UpdateFacingFromDirection(toTarget);

        if (
            GlobalPosition.DistanceSquaredTo(_settlementTarget) <=
            arrivalDistance * arrivalDistance
        )
        {
            CompleteSettlement();
        }
    }

    private void CompleteSettlement()
    {
        _isSettled = true;
        Velocity = Vector2.Zero;
    }

    private void UpdateSporePulse(float delta)
    {
        _sporePulseTimeRemaining -= delta;

        if (_sporePulseTimeRemaining > 0.0f)
            return;

        SpawnSporePulse();
        _sporePulseTimeRemaining =
            Mathf.Max(SporePulseInterval, 0.1f);
    }

    private void SpawnSporePulse()
    {
        if (SporeScene == null || !IsInsideTree())
            return;

        Node parent = GetTree().CurrentScene ?? GetParent();

        if (parent == null)
            return;

        int count = Mathf.Max(SporeCount, 1);
        float step = Mathf.Tau / count;
        float startAngle = _pulseAngleOffset;

        if (GodotObject.IsInstanceValid(Target))
        {
            Vector2 toTarget = Target.GlobalPosition - GlobalPosition;

            if (!toTarget.IsZeroApprox())
                startAngle += toTarget.Angle();
        }

        for (int index = 0; index < count; index++)
        {
            Node instance = SporeScene.Instantiate();

            if (instance is not MoldSporeProjectile spore)
            {
                instance?.QueueFree();
                GD.PushWarning(
                    $"{Name} 的 SporeScene 根节点不是 MoldSporeProjectile。"
                );
                return;
            }

            Vector2 direction =
                Vector2.FromAngle(startAngle + step * index);

            spore.Configure(
                direction,
                this,
                SporeDamage,
                SporeSpeed,
                SporeLifetime,
                PoisonDuration,
                PoisonTickInterval,
                PoisonDamagePerTick
            );
            parent.AddChild(spore);
            spore.GlobalPosition =
                GlobalPosition + direction * Mathf.Max(SporeSpawnRadius, 0.0f);
        }

        // 每轮错开半个弹道间隔，避免所有脉冲完全重叠。
        _pulseAngleOffset = Mathf.PosMod(
            _pulseAngleOffset + step * 0.5f,
            Mathf.Tau
        );
    }

    private void UpdatePollutionRegeneration(float delta)
    {
        _regenerationAccumulator += delta;
        float interval = Mathf.Max(RegenerationInterval, 0.1f);

        if (_regenerationAccumulator < interval)
            return;

        _regenerationAccumulator = Mathf.PosMod(
            _regenerationAccumulator,
            interval
        );

        if (!GodotObject.IsInstanceValid(_pollutionField))
            ResolvePollutionField();

        if (
            !GodotObject.IsInstanceValid(_pollutionField) ||
            CurrentHealth >= MaxHealth ||
            !_pollutionField.HasPollutionNear(
                GlobalPosition,
                PollutionDetectionRadius
            )
        )
        {
            return;
        }

        float healedAmount = Mathf.Min(
            Mathf.Max(RegenerationPerTick, 0.0f),
            MaxHealth - CurrentHealth
        );

        if (healedAmount <= 0.0f)
            return;

        CurrentHealth += healedAmount;
        RefreshHealthBar();
        FloatingDamageNumber.SpawnHealing(
            this,
            healedAmount,
            RegenerationNumberColor,
            DamageNumberOffset,
            DamageNumberRiseDistance,
            DamageNumberDuration,
            DamageNumberFontSize
        );
    }

    private void ResolvePollutionField()
    {
        Node currentScene = GetTree()?.CurrentScene;

        if (currentScene == null)
        {
            _pollutionField = null;
            return;
        }

        _pollutionField = currentScene.FindChild(
            "InkPollutionField",
            true,
            false
        ) as InkPollutionField;
    }

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

        UpdateFacingFromDirection(new Vector2(horizontalDistance, 0.0f));
    }

    private void UpdateFacingFromDirection(Vector2 direction)
    {
        if (
            !GodotObject.IsInstanceValid(_animatedSprite) ||
            Mathf.Abs(direction.X) <= Mathf.Max(FacingDeadZone, 0.0f)
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

    private void SpawnAwakeningWarning()
    {
        if (WarningIconTexture == null || !IsInsideTree())
            return;

        Node parent = GetTree().CurrentScene ?? GetParent();

        if (parent == null)
            return;

        Sprite2D warningIcon = new()
        {
            Name = "TheMoldSpotFungusWarningIcon",
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

    protected override void FlashDamage()
    {
        if (!GodotObject.IsInstanceValid(_animatedSprite))
        {
            _animatedSprite =
                GetNodeOrNull<AnimatedSprite2D>("AnimatedSprite2D");
        }

        if (!GodotObject.IsInstanceValid(_animatedSprite))
            return;

        ulong flashVersion = ++_damageFlashVersion;
        _animatedSprite.Modulate = DamageFlashColor;

        GetTree()
            .CreateTimer(Mathf.Max(DamageFlashDuration, 0.01))
            .Timeout += () =>
            {
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
}
