using Godot;

/// <summary>
/// 武器掉落物，丢弃后生成在地面上，玩家进入范围后可拾取。
/// 拾取范围独立于后续其他拾取系统（如材料、经验等）。
/// </summary>
public partial class WeaponPickup : Area2D
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
        Monitoring = true;
        CollisionLayer = 0;
        CollisionMask = 0;
        SetCollisionMaskValue(2, true);

        AddToGroup("weapon_pickups");
        _pickupCooldown = 0.5f;
        _playerInRange = false;

        BodyEntered += OnBodyEntered;
        BodyExited += OnBodyExited;
    }

    public override void _Process(double delta)
    {
        if (_pickupCooldown > 0f)
            _pickupCooldown -= (float)delta;
    }

    public override void _PhysicsProcess(double delta)
    {
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
    /// 设置掉落物的武器数据和全局位置
    /// </summary>
    public void Setup(WeaponInstance instance, Vector2 globalPosition)
    {
        _instance = instance;
        GlobalPosition = globalPosition;

        var sprite = GetNodeOrNull<Sprite2D>("Visual");
        if (sprite != null && _instance.Data.Icon != null)
        {
            sprite.Texture = _instance.Data.Icon;
        }
    }

    public WeaponInstance GetInstance() => _instance;

    private void OnBodyEntered(Node2D body)
    {
        if (body is Player)
            _playerInRange = true;
    }

    private void OnBodyExited(Node2D body)
    {
        if (body is Player)
            _playerInRange = false;
    }
}
