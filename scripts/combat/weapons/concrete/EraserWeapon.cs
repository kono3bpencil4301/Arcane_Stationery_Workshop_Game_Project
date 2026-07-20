using Godot;
using System.Collections.Generic;

/// <summary>
/// 半常驻辅助武器：装备后三块橡皮等间隔公转，常驻擦除弹幕、污染并击退敌人；
/// 只有被选中时，三块轨道橡皮才会造成接触伤害；
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

    [ExportCategory("Orbit Erasers")]

    [Export]
    public Node2D OrbitEraser1 { get; set; }

    [Export]
    public Node2D OrbitEraser2 { get; set; }

    [Export]
    public Node2D OrbitEraser3 { get; set; }

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

    private readonly Dictionary<ulong, ulong> _orbitDamageHitTimes = new();
    private readonly Dictionary<ulong, ulong> _orbitRepelHitTimes = new();
    private readonly Dictionary<ulong, ulong> _miniHitTimes = new();
    private readonly HashSet<ulong> _eraseCandidateIds = new();
    private readonly List<Node2D> _orbitErasers = new(3);
    private readonly Vector2[] _previousOrbitPositions = new Vector2[3];
    private readonly bool[] _hasPreviousOrbitPositions = new bool[3];

    private Node2D _player;
    private InkPollutionField _pollutionField;
    private Node2D _miniEraser;

    private float _orbitAngle;
    private bool _hasPreviousMiniPosition;
    private Vector2 _previousMiniPosition;

    protected override bool ConsumePaintSkillOnBegin => true;

    public bool ShouldStampPaintCanvas => false;

    protected override void OnInitialized()
    {
        Data.OperationMode = WeaponOperationMode.Hybrid;
        Data.ShowVisualWhenUnselected = true;
        ResolvePlayer();
        ResolveOrbitErasers();
        UpdateOrbit(0.0f);
    }

    public override void _Process(double delta)
    {
        base._Process(delta);

        if (Instance == null || !IsEquipped)
        {
            ResetOrbitTracking();
            return;
        }

        UpdateOrbit((float)delta);
    }

    public override void _PhysicsProcess(double delta)
    {
        if (Instance == null || !IsEquipped)
            return;

        for(int index = 0; index < _orbitErasers.Count; index++)
        {
            Node2D eraser = _orbitErasers[index];

            if(!GodotObject.IsInstanceValid(eraser))
                continue;

            Vector2 orbitPosition = eraser.GlobalPosition;
            Vector2 orbitFrom = _hasPreviousOrbitPositions[index]
                ? _previousOrbitPositions[index]
                : orbitPosition;

            AffectEnemiesAlongSegment(
                orbitFrom,
                orbitPosition,
                eraserContactRadius,
                eraserThrustDamage,
                eraserDamageInterval,
                IsSelected,
                _orbitDamageHitTimes,
                _orbitRepelHitTimes
            );

            EraseAlongSegment(
                orbitFrom,
                orbitPosition,
                eraserEraseRadius,
                eraserEraseLevel
            );

            _previousOrbitPositions[index] = orbitPosition;
            _hasPreviousOrbitPositions[index] = true;
        }
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
        Position = Vector2.Zero;
        Rotation = 0.0f;

        int eraserCount = _orbitErasers.Count;

        if(eraserCount == 0)
            return;

        float orbitRadius = Mathf.Max(eraserRotateRadius, 0.0f);
        float angleStep = Mathf.Tau / eraserCount;
        float rotationOffset = Mathf.DegToRad(
            eraserSelfRotationOffsetDegrees
        );

        for(int index = 0; index < eraserCount; index++)
        {
            Node2D eraser = _orbitErasers[index];

            if(!GodotObject.IsInstanceValid(eraser))
                continue;

            float angle = _orbitAngle + angleStep * index;
            eraser.Position = Vector2.FromAngle(angle) * orbitRadius;
            eraser.Rotation = eraserSelfRotationEnabled
                ? angle + rotationOffset
                : 0.0f;
        }
    }

    private void ResolveOrbitErasers()
    {
        OrbitEraser1 ??= GetNodeOrNull<Node2D>("Visual1");
        OrbitEraser2 ??= GetNodeOrNull<Node2D>("Visual2");
        OrbitEraser3 ??= GetNodeOrNull<Node2D>("Visual3");

        _orbitErasers.Clear();
        AddOrbitEraser(OrbitEraser1);
        AddOrbitEraser(OrbitEraser2);
        AddOrbitEraser(OrbitEraser3);

        if(_orbitErasers.Count == 3)
            return;

        GD.PushWarning(
            $"{Name} 需要绑定三块轨道橡皮，当前有效数量：{_orbitErasers.Count}。"
        );
    }

    private void AddOrbitEraser(Node2D eraser)
    {
        if(
            GodotObject.IsInstanceValid(eraser) &&
            !_orbitErasers.Contains(eraser)
        )
        {
            _orbitErasers.Add(eraser);
        }
    }

    private void ResetOrbitTracking()
    {
        for(int index = 0; index < _hasPreviousOrbitPositions.Length; index++)
            _hasPreviousOrbitPositions[index] = false;
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
        AffectEnemiesAlongSegment(
            from,
            globalPoint,
            MiniEraserRadius,
            MiniEraserDamage,
            MiniEraserDamageInterval,
            true,
            _miniHitTimes,
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

    private void AffectEnemiesAlongSegment(
        Vector2 from,
        Vector2 to,
        float radius,
        float damage,
        float damageInterval,
        bool canDealDamage,
        Dictionary<ulong, ulong> damageHitTimes,
        Dictionary<ulong, ulong> repelHitTimes
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
            bool shouldDamage =
                canDealDamage &&
                CanApplyAtInterval(
                    damageHitTimes,
                    enemyId,
                    now,
                    intervalMilliseconds
                );
            bool shouldRepel =
                enemy is EnemyBase &&
                CanApplyAtInterval(
                    repelHitTimes,
                    enemyId,
                    now,
                    intervalMilliseconds
                );

            if(!shouldDamage && !shouldRepel)
                continue;

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

            if(shouldDamage)
            {
                damageHitTimes[enemyId] = now;
                damageable.TakeDamage(
                    Mathf.Max(damage, 0.0f),
                    this,
                    closestPoint,
                    hitDirection
                );
            }

            if(shouldRepel && enemy is EnemyBase enemyBase)
            {
                repelHitTimes[enemyId] = now;
                RepelEnemy(enemyBase, hitDirection);
            }
        }
    }

    private static bool CanApplyAtInterval(
        Dictionary<ulong, ulong> hitTimes,
        ulong enemyId,
        ulong now,
        ulong intervalMilliseconds
    )
    {
        return
            !hitTimes.TryGetValue(enemyId, out ulong lastHitTime) ||
            now - lastHitTime >= intervalMilliseconds;
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
