using Godot;
using System.Collections.Generic;

/// <summary>
/// 半常驻辅助武器：装备后持续公转并造成接触伤害；选中时主橡皮可擦除；
/// 按住右键会生成一块受距离限制的小橡皮，沿鼠标轨迹进行更高等级的擦除与伤害判定。
/// </summary>
public partial class EraserWeapon : WeaponBase, IPaintStrokeRuntime
{
    private static readonly StringName ErasableGroup = new("erasable");
    private static readonly StringName EnemyProjectileGroup = new("enemy_projectile");
    private static readonly StringName EraseLevelMeta = new("erase_level");

    [ExportCategory("Orbit")]

    [Export]
    public float eraserRotateSpeed = 120.0f;

    [Export]
    public float eraserRotateRadius = 150.0f;

    [Export]
    public bool eraserSelfRotationEnabled = true;

    [Export(PropertyHint.Range, "-180,180,1")]
    public float eraserSelfRotationOffsetDegrees = 0.0f;

    [ExportCategory("Orbit Contact")]

    [Export]
    public float eraserThrustDamage = 10.0f;

    [Export(PropertyHint.Range, "1,128,1")]
    public float eraserContactRadius = 18.0f;

    [Export(PropertyHint.Range, "0.05,5,0.05")]
    public float eraserDamageInterval = 0.45f;

    [Export]
    public float eraserRepelDistance = 30.0f;

    [ExportCategory("Erasing")]

    [Export(PropertyHint.Range, "0,10,1")]
    public int eraserEraseLevel = 2;

    [Export(PropertyHint.Range, "1,128,1")]
    public float eraserEraseRadius = 22.0f;

    [ExportCategory("Mini Eraser Skill")]

    [Export(PropertyHint.Range, "0,10,1")]
    public int SkillEraseLevel { get; set; } = 3;

    [Export]
    public float MiniEraserDamage { get; set; } = 24.0f;

    [Export(PropertyHint.Range, "16,600,1")]
    public float MiniEraserMaxRange { get; set; } = 220.0f;

    [Export(PropertyHint.Range, "4,96,1")]
    public float MiniEraserSize { get; set; } = 18.0f;

    [Export(PropertyHint.Range, "1,128,1")]
    public float MiniEraserRadius { get; set; } = 16.0f;

    [Export(PropertyHint.Range, "0.05,5,0.05")]
    public float MiniEraserDamageInterval { get; set; } = 0.25f;

    private readonly Dictionary<ulong, ulong> _orbitHitTimes = new();
    private readonly Dictionary<ulong, ulong> _miniHitTimes = new();
    private readonly HashSet<ulong> _eraseCandidateIds = new();

    private Node2D _player;
    private InkPollutionField _pollutionField;
    private Node2D _miniEraser;

    private float _orbitAngle;
    private bool _hasPreviousOrbitPosition;
    private Vector2 _previousOrbitPosition;
    private bool _hasPreviousMiniPosition;
    private Vector2 _previousMiniPosition;

    protected override bool ConsumePaintSkillOnBegin => true;

    public bool ShouldStampPaintCanvas => false;

    protected override void OnInitialized()
    {
        Data.OperationMode = WeaponOperationMode.Hybrid;
        Data.ShowVisualWhenUnselected = true;
        ResolvePlayer();
    }

    public override void _Process(double delta)
    {
        base._Process(delta);

        if (Instance == null || !IsEquipped)
        {
            _hasPreviousOrbitPosition = false;
            return;
        }

        UpdateOrbit((float)delta);
    }

    public override void _PhysicsProcess(double delta)
    {
        if (Instance == null || !IsEquipped)
            return;

        Vector2 orbitPosition = GlobalPosition;
        Vector2 orbitFrom = _hasPreviousOrbitPosition
            ? _previousOrbitPosition
            : orbitPosition;

        DamageEnemiesAlongSegment(
            orbitFrom,
            orbitPosition,
            eraserContactRadius,
            eraserThrustDamage,
            eraserDamageInterval,
            _orbitHitTimes
        );

        if (IsSelected)
        {
            EraseAlongSegment(
                orbitFrom,
                orbitPosition,
                eraserEraseRadius,
                eraserEraseLevel
            );
        }

        _previousOrbitPosition = orbitPosition;
        _hasPreviousOrbitPosition = true;
    }

    protected override void AutoAttack()
    {
        // 接触伤害由物理帧中的轨迹判定持续处理，不需要离散的自动攻击动作。
    }

    protected override void CancelSkill()
    {
        base.CancelSkill();
        EndMiniEraser();
    }

    protected override void OnPaintSkillStarted()
    {
        BeginMiniEraser();
    }

    protected override void OnPaintSkillCommitted(PaintStrokeResult result)
    {
        EndMiniEraser();
    }

    protected override void OnPaintSkillCancelled()
    {
        EndMiniEraser();
    }

    private void UpdateOrbit(float delta)
    {
        _orbitAngle = Mathf.PosMod(
            _orbitAngle + Mathf.DegToRad(eraserRotateSpeed) * delta,
            Mathf.Tau
        );

        Position = Vector2.FromAngle(_orbitAngle) *
            Mathf.Max(eraserRotateRadius, 0.0f);

        if (!eraserSelfRotationEnabled)
            return;
        
        Rotation = _orbitAngle + Mathf.DegToRad(eraserSelfRotationOffsetDegrees);
    }

    private void BeginMiniEraser()
    {
        if (GodotObject.IsInstanceValid(_miniEraser))
            return;

        Node currentScene = GetTree().CurrentScene;
        if (currentScene == null)
            return;

        float halfSize = Mathf.Max(MiniEraserSize, 4.0f) * 0.5f;
        Polygon2D placeholder = new()
        {
            Name = "Placeholder",
            Polygon = new Vector2[]
            {
                new(-halfSize, -halfSize),
                new(halfSize, -halfSize),
                new(halfSize, halfSize),
                new(-halfSize, halfSize)
            },
            Color = Colors.White
        };

        _miniEraser = new Node2D
        {
            Name = "MiniEraser",
            ZIndex = ZIndex + 1
        };
        _miniEraser.AddChild(placeholder);
        currentScene.AddChild(_miniEraser);

        _miniEraser.GlobalPosition = ConstrainPaintSkillPoint(
            GetGlobalMousePosition()
        );
        _previousMiniPosition = _miniEraser.GlobalPosition;
        _hasPreviousMiniPosition = true;
        _miniHitTimes.Clear();
    }

    public Vector2 ConstrainPaintSkillPoint(Vector2 globalPoint)
    {
        if (!GodotObject.IsInstanceValid(_player))
            ResolvePlayer();

        Vector2 center = GodotObject.IsInstanceValid(_player)
            ? _player.GlobalPosition
            : GetOrbitCenter();
        Vector2 offset = globalPoint - center;
        float maximumRange = Mathf.Max(MiniEraserMaxRange, 0.0f);

        if (offset.LengthSquared() <= maximumRange * maximumRange)
            return globalPoint;

        return offset.IsZeroApprox()
            ? center
            : center + offset.Normalized() * maximumRange;
    }

    public void OnPaintSkillPointAccepted(Vector2 globalPoint)
    {
        if (!GodotObject.IsInstanceValid(_miniEraser))
            return;

        Vector2 from = _hasPreviousMiniPosition
            ? _previousMiniPosition
            : globalPoint;

        _miniEraser.GlobalPosition = globalPoint;

        EraseAlongSegment(
            from,
            globalPoint,
            MiniEraserRadius,
            SkillEraseLevel
        );
        DamageEnemiesAlongSegment(
            from,
            globalPoint,
            MiniEraserRadius,
            MiniEraserDamage,
            MiniEraserDamageInterval,
            _miniHitTimes
        );

        _previousMiniPosition = globalPoint;
        _hasPreviousMiniPosition = true;
    }

    private Vector2 GetOrbitCenter()
    {
        return GetParent() is Node2D weaponMount
            ? weaponMount.GlobalPosition
            : GlobalPosition;
    }

    private void DamageEnemiesAlongSegment(
        Vector2 from,
        Vector2 to,
        float radius,
        float damage,
        float damageInterval,
        Dictionary<ulong, ulong> hitTimes
    )
    {
        float safeRadius = Mathf.Max(radius, 0.0f);
        float radiusSquared = safeRadius * safeRadius;
        ulong now = Time.GetTicksMsec();
        ulong intervalMilliseconds = (ulong)Mathf.CeilToInt(
            Mathf.Max(damageInterval, 0.0f) * 1000.0f
        );

        foreach (Node node in GetTree().GetNodesInGroup("enemy"))
        {
            if (
                node is not Node2D enemy ||
                node is not IDamageable damageable ||
                !GodotObject.IsInstanceValid(enemy)
            )
            {
                continue;
            }

            Vector2 closestPoint = ClosestPointOnSegment(
                enemy.GlobalPosition,
                from,
                to
            );

            if (
                closestPoint.DistanceSquaredTo(enemy.GlobalPosition) >
                radiusSquared
            )
            {
                continue;
            }

            ulong enemyId = enemy.GetInstanceId();
            if (
                hitTimes.TryGetValue(enemyId, out ulong lastHitTime) &&
                now - lastHitTime < intervalMilliseconds
            )
            {
                continue;
            }

            hitTimes[enemyId] = now;

            Vector2 hitDirection = closestPoint.DirectionTo(
                enemy.GlobalPosition
            );
            if (hitDirection.IsZeroApprox())
            {
                hitDirection = GetOrbitCenter().DirectionTo(
                    enemy.GlobalPosition
                );
            }
            if (hitDirection.IsZeroApprox())
                hitDirection = Vector2.Right;

            damageable.TakeDamage(
                Mathf.Max(damage, 0.0f),
                this,
                closestPoint,
                hitDirection
            );

            if (enemy is EnemyBase enemyBase)
                RepelEnemy(enemyBase, hitDirection);
        }
    }

    private void RepelEnemy(EnemyBase enemy, Vector2 direction)
    {
        float distance = Mathf.Max(eraserRepelDistance, 0.0f);
        if (distance <= 0.0f)
            return;

        float repelSpeed = Mathf.Sqrt(
            2.0f *
            Mathf.Max(enemy.PushDeceleration, 1.0f) *
            distance
        );
        enemy.ApplyPlayerPush(direction, repelSpeed);
    }

    private void EraseAlongSegment(
        Vector2 from,
        Vector2 to,
        float radius,
        int eraseLevel
    )
    {
        ResolvePollutionField();
        _pollutionField?.EraseAlongSegment(
            from,
            to,
            Mathf.Max(radius, 0.0f),
            Mathf.Max(eraseLevel, 0)
        );

        _eraseCandidateIds.Clear();
        EraseGroupAlongSegment(
            ErasableGroup,
            from,
            to,
            radius,
            eraseLevel
        );
        EraseGroupAlongSegment(
            EnemyProjectileGroup,
            from,
            to,
            radius,
            eraseLevel
        );
    }

    private void EraseGroupAlongSegment(
        StringName group,
        Vector2 from,
        Vector2 to,
        float radius,
        int eraseLevel
    )
    {
        float safeRadius = Mathf.Max(radius, 0.0f);
        float radiusSquared = safeRadius * safeRadius;

        foreach (Node node in GetTree().GetNodesInGroup(group))
        {
            if (
                node is not Node2D erasableNode ||
                !GodotObject.IsInstanceValid(erasableNode) ||
                erasableNode.IsQueuedForDeletion() ||
                !_eraseCandidateIds.Add(erasableNode.GetInstanceId())
            )
            {
                continue;
            }

            Vector2 closestPoint = ClosestPointOnSegment(
                erasableNode.GlobalPosition,
                from,
                to
            );
            if (
                closestPoint.DistanceSquaredTo(
                    erasableNode.GlobalPosition
                ) > radiusSquared
            )
            {
                continue;
            }

            if (erasableNode is IErasable erasable)
            {
                if (eraseLevel >= erasable.EraseLevel)
                    erasable.Erase(this);

                continue;
            }

            if (!erasableNode.HasMeta(EraseLevelMeta))
                continue;

            int requiredLevel = erasableNode
                .GetMeta(EraseLevelMeta)
                .AsInt32();

            if (eraseLevel >= requiredLevel)
                erasableNode.QueueFree();
        }
    }

    private void ResolvePlayer()
    {
        Node ancestor = GetParent();
        while (ancestor != null)
        {
            if (ancestor is Player player)
            {
                _player = player;
                return;
            }

            ancestor = ancestor.GetParent();
        }

        _player = GetTree()?.GetFirstNodeInGroup("player") as Node2D;
    }

    private void ResolvePollutionField()
    {
        if (GodotObject.IsInstanceValid(_pollutionField))
            return;

        Node currentScene = GetTree()?.CurrentScene;
        if (currentScene == null)
            return;

        _pollutionField =
            currentScene.GetNodeOrNull<InkPollutionField>("InkPollutionField") ??
            currentScene.FindChild(
                "InkPollutionField",
                true,
                false
            ) as InkPollutionField;
    }

    private static Vector2 ClosestPointOnSegment(
        Vector2 point,
        Vector2 from,
        Vector2 to
    )
    {
        Vector2 segment = to - from;
        float lengthSquared = segment.LengthSquared();
        if (lengthSquared <= 0.0001f)
            return from;

        float ratio = Mathf.Clamp(
            (point - from).Dot(segment) / lengthSquared,
            0.0f,
            1.0f
        );
        return from + segment * ratio;
    }

    private void EndMiniEraser()
    {
        if (GodotObject.IsInstanceValid(_miniEraser))
            _miniEraser.QueueFree();

        _miniEraser = null;
        _hasPreviousMiniPosition = false;
        _miniHitTimes.Clear();
    }

    public override void _ExitTree()
    {
        EndMiniEraser();
    }
}
