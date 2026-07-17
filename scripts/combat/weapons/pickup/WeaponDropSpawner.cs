using Godot;

/// <summary>
/// 武器掉落物，丢弃后生成在地面上。
/// 拥有独立的拾取范围（PickupRadius），与后续其他拾取系统隔离。
/// 所有内部节点在 _Ready 中自行创建，不依赖外部。
/// </summary>
public partial class WeaponDropSpawner : Area2D
{
    /// <summary>
    /// 拾取检测半径（仅用于武器掉落物的玩家交互）
    /// </summary>
    [Export] public float PickupRadius { get; set; } = 25f;

    private WeaponInstance _instance = null!;

    // 丢弃后短暂冷却，防止刚丢下立即被拾取
    private float _pickupCooldown;

    // 玩家是否在拾取范围内
    private bool _playerInRange;

    /// <summary>
    /// 玩家在范围内且冷却已过时可拾取
    /// </summary>
    public bool CanPickup => _pickupCooldown <= 0f && _playerInRange;

    public override void _Ready()
    {
        // 确保 Area2D 启用监测
        Monitoring = true;

        AddToGroup("weapon_drops");
        _pickupCooldown = 0.5f;
        _playerInRange = false;

        // 不参与物理碰撞，只检测玩家所在的物理层 2。
        CollisionLayer = 0;
        CollisionMask = 0;
        SetCollisionMaskValue(2, true);

        // 创建拾取范围碰撞体
        var collisionShape = new CollisionShape2D();
        collisionShape.Name = "PickupArea";
        var shape = new CircleShape2D();
        shape.Radius = PickupRadius;
        collisionShape.Shape = shape;
        AddChild(collisionShape);

        // 连接 Area2D 信号
        BodyEntered += OnBodyEntered;
        BodyExited += OnBodyExited;

        // 如果 Initialize 在 _Ready 之前调用过，此时应用视觉
        if (_instance != null)
            ApplyVisual();

        GD.Print($"[WeaponDrop] _Ready 完成，位置: {GlobalPosition}");
    }

    public override void _Process(double delta)
    {
        if (_pickupCooldown > 0f)
            _pickupCooldown -= (float)delta;
    }

    public override void _PhysicsProcess(double delta)
    {
        // 动态掉落物会先加入场景树再移动到玩家位置。主动查询重叠可避免
        // 初始位置变化导致 BodyEntered 没有及时触发。
        _playerInRange = false;

        foreach (Node2D body in GetOverlappingBodies())
        {
            if (body is not Player)
                continue;

            _playerInRange = true;
            break;
        }
    }

    /// <summary>
    /// 初始化掉落物：设置武器数据和全局位置
    /// 在 AddChild 之前调用，_Ready 中会自动应用视觉
    /// </summary>
    public void Initialize(WeaponInstance instance, Vector2 globalPosition)
    {
        _instance = instance;
        GlobalPosition = globalPosition;

        // 如果 _Ready 已执行（节点已在场景树中），立即应用视觉
        if (IsInsideTree())
            ApplyVisual();
    }

    /// <summary>
    /// 应用视觉显示：优先用 Icon，其次从 ControllerScene 提取纹理
    /// </summary>
    private void ApplyVisual()
    {
        // 优先使用 Icon
        if (_instance.Data.Icon != null)
        {
            EnsureSprite().Texture = _instance.Data.Icon;
            return;
        }

        // 没有 Icon，从 ControllerScene 实例化提取第一个 Sprite2D 的纹理
        if (_instance.Data.ControllerScene != null)
        {
            Node temp = _instance.Data.ControllerScene.Instantiate();
            if (temp is Node2D tempNode)
            {
                // 深度搜索第一个有纹理的 Sprite2D
                var found = FindSpriteWithTexture(tempNode);
                if (found != null)
                {
                    EnsureSprite().Texture = found.Texture;
                    GD.Print($"[WeaponDrop] 从场景提取纹理: {found.Texture?.ResourcePath}");
                }
                temp.QueueFree();
            }
        }
    }

    /// <summary>
    /// 深度优先搜索节点树中第一个有纹理的 Sprite2D
    /// </summary>
    private Sprite2D? FindSpriteWithTexture(Node root)
    {
        if (root is Sprite2D sprite && sprite.Texture != null)
            return sprite;

        foreach (Node child in root.GetChildren())
        {
            var found = FindSpriteWithTexture(child);
            if (found != null)
                return found;
        }
        return null;
    }

    /// <summary>
    /// 掉落物视觉缩放比例
    /// </summary>
    [Export] public float VisualScale { get; set; } = 0.5f;

    /// <summary>
    /// 确保存在 Visual Sprite2D 子节点
    /// </summary>
    private Sprite2D EnsureSprite()
    {
        var sprite = GetNodeOrNull<Sprite2D>("Visual");
        if (sprite == null)
        {
            sprite = new Sprite2D();
            sprite.Name = "Visual";
            sprite.Scale = new Vector2(VisualScale, VisualScale);
            AddChild(sprite);
        }
        return sprite;
    }

    public WeaponInstance GetInstance() => _instance;

    private void OnBodyEntered(Node2D body)
    {
        if (body is Player)
        {
            _playerInRange = true;
            GD.Print("[WeaponDrop] 玩家进入拾取范围");
        }
    }

    private void OnBodyExited(Node2D body)
    {
        if (body is Player)
            _playerInRange = false;
    }
}
