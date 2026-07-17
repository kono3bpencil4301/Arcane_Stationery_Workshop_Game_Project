using Godot;
using System.Collections.Generic;

/// <summary>
/// 主动技能生成的持续伤害区域基类。
///
/// 职责：
/// 1. 维护当前进入区域的敌人；
/// 2. 使用 Timer 周期性造成伤害；
/// 3. 向 EnemyBase 注册减速区域；
/// 4. 持续时间结束后自动移除。
///
/// 普通攻击不使用该节点，确保技能伤害与普通攻击彼此独立。
/// </summary>
[GlobalClass]
public partial class SkillDamageArea :
    Area2D,
    ISharedSkillChargeSource
{
    [ExportCategory("Skill Damage")]

    /// <summary>区域存在时间，单位为秒。</summary>
    [Export(PropertyHint.Range, "0.1,30,0.1")]
    public float Duration { get; set; } = 4.0f;

    /// <summary>每个伤害周期对区域内每名敌人造成的伤害。</summary>
    [Export(PropertyHint.Range, "0,1000,0.5")]
    public float DamagePerTick { get; set; } = 3.0f;

    /// <summary>持续伤害的触发间隔，单位为秒。</summary>
    [Export(PropertyHint.Range, "0.05,10,0.05")]
    public float TickInterval { get; set; } = 0.5f;

    /// <summary>伤害类型，用于技能识别和后续抗性系统。</summary>
    [Export]
    public DamageType DamageType { get; set; } = DamageType.Physical;

    [ExportCategory("Area Effect")]

    /// <summary>
    /// 区域内敌人的移动速度倍率。0.9 表示降低 10%。
    /// 多个区域重叠时，由 EnemyBase 使用其中最强的减速。
    /// </summary>
    [Export(PropertyHint.Range, "0.1,1,0.05")]
    public float MovementSpeedMultiplier { get; set; } = 0.9f;

    /// <summary>技能线的碰撞宽度。</summary>
    [Export(PropertyHint.Range, "2,128,1")]
    public float AreaWidth { get; set; } = 18.0f;

    [ExportCategory("Tile Range")]

    /// <summary>地图单个瓦片的世界尺寸。当前地图使用 48×48。</summary>
    [Export(PropertyHint.Range, "8,256,1")]
    public float TileSize { get; set; } = 48.0f;

    /// <summary>瓦片网格在世界坐标中的原点。</summary>
    [Export]
    public Vector2 TileGridOrigin { get; set; } = Vector2.Zero;

    /// <summary>视觉覆盖与瓦片边缘之间的留白，不影响碰撞范围。</summary>
    [Export(PropertyHint.Range, "0,16,0.5")]
    public float TileVisualInset { get; set; } = 2.0f;

    [ExportCategory("Visual")]

    /// <summary>区域线条的颜色。</summary>
    [Export]
    public Color AreaColor { get; set; } = new(0.1f, 0.1f, 0.1f, 0.65f);

    [Export]
    public Color ParticleColor { get; set; } = new(0.15f, 0.15f, 0.15f, 0.8f);

    protected CollisionShape2D TemplateCollisionShape;
    protected Sprite2D AreaSprite;

    private Timer _damageTimer;
    private Node _damageSource;
    private float _remainingDuration;

    protected Node DamageSource => _damageSource;

    public float SharedSkillChargePercent { get; private set; }

    public Node SharedSkillChargeOwner => _damageSource;

    private readonly HashSet<Node2D> _enemiesInside = new();
    private readonly List<Node2D> _enemySnapshot = new();
    private readonly List<CollisionShape2D> _generatedCollisionShapes = new();

    /// <summary>
    /// 武器在把区域加入场景树之前调用，写入伤害来源和笔迹几何。
    /// </summary>
    public void Initialize(
        Node damageSource,
        PaintStrokeResult strokeResult,
        float sharedSkillChargePercent
    )
    {
        _damageSource = damageSource;
        SharedSkillChargePercent = Mathf.Max(
            sharedSkillChargePercent,
            0.0f
        );

        if (strokeResult == null || strokeResult.PointCount == 0)
            return;

        GlobalPosition = strokeResult.StartPoint;
        ConfigureFromStroke(strokeResult);
    }

    public override void _Ready()
    {
        TemplateCollisionShape =
            GetNodeOrNull<CollisionShape2D>("CollisionShape2D");
        AreaSprite = GetNodeOrNull<Sprite2D>("Sprite2D");
        _damageTimer = GetNodeOrNull<Timer>("Timer");

        BodyEntered += OnBodyEntered;
        BodyExited += OnBodyExited;

        _remainingDuration = Mathf.Max(Duration, 0.01f);

        if (_damageTimer == null)
        {
            GD.PushError($"{Name} 缺少 Timer，持续伤害无法运行。");
            return;
        }

        _damageTimer.OneShot = false;
        _damageTimer.WaitTime = Mathf.Max(TickInterval, 0.05f);
        _damageTimer.Timeout += ApplyDamageTick;
        _damageTimer.Start();

        // 兼容生成时已经站在区域内的敌人。
        CallDeferred(MethodName.RegisterInitialOverlaps);
    }

    public override void _Process(double delta)
    {
        _remainingDuration -= (float)delta;

        if (_remainingDuration <= 0.0f)
            QueueFree();
    }

    /// <summary>
    /// 默认沿玩家实际绘制的轨迹生成连续矩形碰撞段。
    /// 派生类可以改为单个矩形或其他形状。
    /// </summary>
    protected virtual void ConfigureFromStroke(
        PaintStrokeResult strokeResult
    )
    {
        BuildTileAlignedArea(strokeResult.GlobalPoints);
    }

    /// <summary>
    /// 根据鼠标轨迹采样其经过的瓦片。
    /// 每个唯一瓦片生成一个完整 48×48 碰撞区域和半透明范围提示。
    /// </summary>
    protected void BuildTileAlignedArea(
        IReadOnlyList<Vector2> globalPoints
    )
    {
        ClearGeneratedGeometry();

        if (globalPoints == null || globalPoints.Count < 2)
            return;

        float tileSize = Mathf.Max(TileSize, 1.0f);
        float sampleSpacing = Mathf.Max(tileSize * 0.2f, 1.0f);
        HashSet<Vector2I> affectedCells = new();

        for (int pointIndex = 1; pointIndex < globalPoints.Count; pointIndex++)
        {
            Vector2 from = globalPoints[pointIndex - 1];
            Vector2 to = globalPoints[pointIndex];
            float distance = from.DistanceTo(to);
            int sampleCount = Mathf.Max(
                1,
                Mathf.CeilToInt(distance / sampleSpacing)
            );

            for (int sampleIndex = 0; sampleIndex <= sampleCount; sampleIndex++)
            {
                float weight = (float)sampleIndex / sampleCount;
                Vector2 samplePoint = from.Lerp(to, weight);
                affectedCells.Add(WorldPointToTile(samplePoint, tileSize));
            }
        }

        if (affectedCells.Count == 0)
            return;

        if (AreaSprite != null)
            AreaSprite.Visible = false;

        Node2D tileEffects = new()
        {
            Name = "TileEffects"
        };
        AddChild(tileEffects);

        Vector2 minimum = new(float.MaxValue, float.MaxValue);
        Vector2 maximum = new(float.MinValue, float.MinValue);
        float visualSize = Mathf.Max(
            tileSize - Mathf.Max(TileVisualInset, 0.0f) * 2.0f,
            1.0f
        );
        Vector2 halfVisual = Vector2.One * visualSize * 0.5f;

        foreach (Vector2I cell in affectedCells)
        {
            Vector2 globalCenter = new(
                TileGridOrigin.X + (cell.X + 0.5f) * tileSize,
                TileGridOrigin.Y + (cell.Y + 0.5f) * tileSize
            );
            Vector2 localCenter = globalCenter - GlobalPosition;

            CollisionShape2D collisionShape = new()
            {
                Name = $"TileCollision_{cell.X}_{cell.Y}",
                Position = localCenter,
                Shape = new RectangleShape2D
                {
                    Size = Vector2.One * tileSize
                }
            };
            AddChild(collisionShape);
            _generatedCollisionShapes.Add(collisionShape);

            Polygon2D tileVisual = new()
            {
                Name = $"TileVisual_{cell.X}_{cell.Y}",
                Position = localCenter,
                Polygon = new Vector2[]
                {
                    new(-halfVisual.X, -halfVisual.Y),
                    new(halfVisual.X, -halfVisual.Y),
                    new(halfVisual.X, halfVisual.Y),
                    new(-halfVisual.X, halfVisual.Y)
                },
                Color = AreaColor,
                ZIndex = -1
            };
            tileEffects.AddChild(tileVisual);

            minimum.X = Mathf.Min(minimum.X, localCenter.X - tileSize * 0.5f);
            minimum.Y = Mathf.Min(minimum.Y, localCenter.Y - tileSize * 0.5f);
            maximum.X = Mathf.Max(maximum.X, localCenter.X + tileSize * 0.5f);
            maximum.Y = Mathf.Max(maximum.Y, localCenter.Y + tileSize * 0.5f);
        }

        // 在瓦片覆盖之上保留一条细线，明确显示原始鼠标轨迹。
        Color previewColor = AreaColor;
        previewColor.A = Mathf.Max(previewColor.A, 0.85f);

        Line2D previewLine = new()
        {
            Name = "DamageRangePreview",
            Width = 4.0f,
            DefaultColor = previewColor,
            Antialiased = true,
            ZIndex = 0
        };

        foreach (Vector2 globalPoint in globalPoints)
            previewLine.AddPoint(globalPoint - GlobalPosition);

        AddChild(previewLine);

        CreateParticleVisual(
            new Rect2(minimum, maximum - minimum)
        );
    }

    private Vector2I WorldPointToTile(
        Vector2 globalPoint,
        float tileSize
    )
    {
        Vector2 gridPoint = globalPoint - TileGridOrigin;
        return new Vector2I(
            Mathf.FloorToInt(gridPoint.X / tileSize),
            Mathf.FloorToInt(gridPoint.Y / tileSize)
        );
    }

    /// <summary>沿整条笔迹生成连续碰撞段和 Line2D 视觉。</summary>
    protected void BuildPolylineArea(
        IReadOnlyList<Vector2> globalPoints
    )
    {
        ClearGeneratedGeometry();

        if (globalPoints == null || globalPoints.Count < 2)
            return;

        Vector2 origin = globalPoints[0];

        for (int i = 1; i < globalPoints.Count; i++)
        {
            AddRectangleSegment(
                globalPoints[i - 1] - origin,
                globalPoints[i] - origin
            );
        }

        if (AreaSprite != null)
            AreaSprite.Visible = false;

        Line2D line = new()
        {
            Name = "SkillLine",
            Width = Mathf.Max(AreaWidth, 2.0f),
            DefaultColor = AreaColor,
            Antialiased = true,
            ZIndex = -1
        };

        for (int i = 0; i < globalPoints.Count; i++)
            line.AddPoint(globalPoints[i] - origin);

        AddChild(line);

        CreateParticleVisual(
            CalculateLocalBounds(globalPoints, origin)
        );
    }

    /// <summary>用首尾点生成一个矩形区域，供石墨轨迹使用。</summary>
    protected void BuildStraightRectangle(
        Vector2 globalStart,
        Vector2 globalEnd
    )
    {
        ClearGeneratedGeometry();

        Vector2 localStart = Vector2.Zero;
        Vector2 localEnd = globalEnd - globalStart;
        AddRectangleSegment(localStart, localEnd);

        if (AreaSprite == null)
            return;

        float length = Mathf.Max(localStart.DistanceTo(localEnd), 1.0f);
        AreaSprite.Visible = true;
        AreaSprite.Position = (localStart + localEnd) * 0.5f;
        AreaSprite.Rotation = (localEnd - localStart).Angle();
        AreaSprite.Scale = new Vector2(length, Mathf.Max(AreaWidth, 2.0f));
        AreaSprite.Modulate = AreaColor;

        Vector2 minimum = new(
            Mathf.Min(localStart.X, localEnd.X),
            Mathf.Min(localStart.Y, localEnd.Y)
        );
        Vector2 maximum = new(
            Mathf.Max(localStart.X, localEnd.X),
            Mathf.Max(localStart.Y, localEnd.Y)
        );
        Vector2 padding = Vector2.One * AreaWidth * 0.5f;

        CreateParticleVisual(
            new Rect2(
                minimum - padding,
                maximum - minimum + padding * 2.0f
            )
        );
    }

    private Rect2 CalculateLocalBounds(
        IReadOnlyList<Vector2> globalPoints,
        Vector2 origin
    )
    {
        Vector2 minimum = globalPoints[0] - origin;
        Vector2 maximum = minimum;

        for (int i = 1; i < globalPoints.Count; i++)
        {
            Vector2 point = globalPoints[i] - origin;
            minimum.X = Mathf.Min(minimum.X, point.X);
            minimum.Y = Mathf.Min(minimum.Y, point.Y);
            maximum.X = Mathf.Max(maximum.X, point.X);
            maximum.Y = Mathf.Max(maximum.Y, point.Y);
        }

        Vector2 padding = Vector2.One * AreaWidth * 0.5f;
        return new Rect2(
            minimum - padding,
            maximum - minimum + padding * 2.0f
        );
    }

    /// <summary>
    /// 创建轻量 CPU 粒子作为石墨颗粒或粉尘。
    /// 粒子只负责视觉，不参与伤害判定。
    /// </summary>
    private void CreateParticleVisual(Rect2 localBounds)
    {
        Node oldParticles = GetNodeOrNull("SkillParticles");
        oldParticles?.QueueFree();

        Image particleImage = Image.CreateEmpty(
            2,
            2,
            false,
            Image.Format.Rgba8
        );
        particleImage.Fill(Colors.White);

        CpuParticles2D particles = new()
        {
            Name = "SkillParticles",
            Position = localBounds.GetCenter(),
            Amount = 28,
            Lifetime = 1.2,
            Preprocess = 1.2,
            EmissionShape = CpuParticles2D.EmissionShapeEnum.Rectangle,
            EmissionRectExtents = new Vector2(
                Mathf.Max(localBounds.Size.X * 0.5f, 1.0f),
                Mathf.Max(localBounds.Size.Y * 0.5f, 1.0f)
            ),
            Direction = Vector2.Up,
            Spread = 180.0f,
            Gravity = Vector2.Zero,
            InitialVelocityMin = 3.0f,
            InitialVelocityMax = 10.0f,
            ScaleAmountMin = 0.5f,
            ScaleAmountMax = 1.25f,
            Color = ParticleColor,
            Texture = ImageTexture.CreateFromImage(particleImage),
            Emitting = true,
            ZIndex = 1
        };

        AddChild(particles);
    }

    private void AddRectangleSegment(Vector2 from, Vector2 to)
    {
        Vector2 segment = to - from;
        float length = segment.Length();

        if (length <= 0.001f)
            return;

        CollisionShape2D collisionShape = new()
        {
            Name = $"Segment{_generatedCollisionShapes.Count + 1}",
            Position = (from + to) * 0.5f,
            Rotation = segment.Angle(),
            Shape = new RectangleShape2D
            {
                Size = new Vector2(
                    length + Mathf.Max(AreaWidth, 2.0f),
                    Mathf.Max(AreaWidth, 2.0f)
                )
            }
        };

        AddChild(collisionShape);
        _generatedCollisionShapes.Add(collisionShape);
    }

    private void ClearGeneratedGeometry()
    {
        if (TemplateCollisionShape == null)
        {
            TemplateCollisionShape =
                GetNodeOrNull<CollisionShape2D>("CollisionShape2D");
        }

        if (AreaSprite == null)
            AreaSprite = GetNodeOrNull<Sprite2D>("Sprite2D");

        if (TemplateCollisionShape != null)
            TemplateCollisionShape.Disabled = true;

        foreach (CollisionShape2D shape in _generatedCollisionShapes)
        {
            if (GodotObject.IsInstanceValid(shape))
                shape.QueueFree();
        }

        _generatedCollisionShapes.Clear();

        Node oldLine = GetNodeOrNull("SkillLine");
        oldLine?.QueueFree();

        Node oldParticles = GetNodeOrNull("SkillParticles");
        oldParticles?.QueueFree();

        Node oldTileEffects = GetNodeOrNull("TileEffects");
        oldTileEffects?.QueueFree();

        Node oldPreview = GetNodeOrNull("DamageRangePreview");
        oldPreview?.QueueFree();
    }

    private void RegisterInitialOverlaps()
    {
        foreach (Node2D body in GetOverlappingBodies())
            RegisterEnemy(body);
    }

    private void OnBodyEntered(Node2D body)
    {
        RegisterEnemy(body);
    }

    private void OnBodyExited(Node2D body)
    {
        UnregisterEnemy(body);
    }

    private void RegisterEnemy(Node2D body)
    {
        if (
            !GodotObject.IsInstanceValid(body) ||
            !body.IsInGroup("enemy") ||
            body is not IDamageable
        )
        {
            return;
        }

        if (!_enemiesInside.Add(body))
            return;

        if (body is EnemyBase enemy)
            enemy.RegisterSkillArea(this);
    }

    private void UnregisterEnemy(Node2D body)
    {
        if (!_enemiesInside.Remove(body))
            return;

        if (GodotObject.IsInstanceValid(body) && body is EnemyBase enemy)
            enemy.UnregisterSkillArea(this);
    }

    private void ApplyDamageTick()
    {
        _enemySnapshot.Clear();
        _enemySnapshot.AddRange(_enemiesInside);

        foreach (Node2D enemyNode in _enemySnapshot)
        {
            if (
                !GodotObject.IsInstanceValid(enemyNode) ||
                !enemyNode.IsInsideTree() ||
                enemyNode is not IDamageable damageable
            )
            {
                _enemiesInside.Remove(enemyNode);
                continue;
            }

            Vector2 hitDirection = Vector2.Zero;

            if (_damageSource is Node2D sourceNode)
            {
                hitDirection = sourceNode.GlobalPosition
                    .DirectionTo(enemyNode.GlobalPosition);
            }

            damageable.TakeDamage(
                Mathf.Max(DamagePerTick, 0.0f),
                this,
                enemyNode.GlobalPosition,
                hitDirection
            );

            if (DamageType == DamageType.Graphite)
                SpawnGraphiteAfterimage(enemyNode);
        }
    }

    /// <summary>
    /// 石墨轨迹中的敌人在每次伤害周期留下短暂黑色残影。
    /// 使用轻量 Polygon2D，避免复制敌人脚本或碰撞节点。
    /// </summary>
    private void SpawnGraphiteAfterimage(Node2D enemyNode)
    {
        Node currentScene = GetTree().CurrentScene;

        if (currentScene == null)
            return;

        Polygon2D afterimage = new()
        {
            Name = "GraphiteAfterimage",
            Polygon = new Vector2[]
            {
                new(-9.0f, -12.0f),
                new(9.0f, -12.0f),
                new(12.0f, 0.0f),
                new(8.0f, 12.0f),
                new(-8.0f, 12.0f),
                new(-12.0f, 0.0f)
            },
            Color = new Color(0.015f, 0.015f, 0.015f, 0.4f),
            ZIndex = -1
        };

        currentScene.AddChild(afterimage);
        afterimage.GlobalPosition = enemyNode.GlobalPosition;

        Tween fadeTween = afterimage.CreateTween();
        fadeTween.TweenProperty(
            afterimage,
            "modulate:a",
            0.0f,
            0.55
        );
        fadeTween.TweenCallback(
            Callable.From(afterimage.QueueFree)
        );
    }

    public override void _ExitTree()
    {
        _enemySnapshot.Clear();
        _enemySnapshot.AddRange(_enemiesInside);

        foreach (Node2D enemyNode in _enemySnapshot)
        {
            if (GodotObject.IsInstanceValid(enemyNode))
                UnregisterEnemy(enemyNode);
        }

        _enemiesInside.Clear();
    }
}
