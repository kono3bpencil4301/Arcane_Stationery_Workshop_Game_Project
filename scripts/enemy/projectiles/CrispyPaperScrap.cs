using Godot;

/// <summary>
/// 脆页偶死亡爆炸后飞出的短距离纸片。
/// 会伤害玩家与其他敌人，也能被橡皮擦除。
/// </summary>
public partial class CrispyPaperScrap : CharacterBody2D, IErasable
{
    private static readonly StringName ErasableGroup = new("erasable");
    private static readonly StringName EnemyProjectileGroup = new("enemy_projectile");

    [ExportCategory("Scrap Combat")]

    public int EraseLevel => 1;

    [Export(PropertyHint.Range, "0,1000,0.5")]
    public float Damage { get; set; } = 5.0f;

    [Export(PropertyHint.Range, "1,1000,1")]
    public float Speed { get; set; } = 180.0f;

    [Export(PropertyHint.Range, "0.05,10,0.05")]
    public float Lifetime { get; set; } = 0.75f;

    [ExportCategory("Scrap Visuals")]

    [Export]
    public Texture2D ScrapTexture01 { get; set; }

    [Export]
    public Texture2D ScrapTexture02 { get; set; }

    [Export]
    public Texture2D ScrapTexture03 { get; set; }

    [Export(PropertyHint.Range, "0,30,0.1")]
    public float MaximumSpinSpeed { get; set; } = 10.0f;

    private readonly RandomNumberGenerator _random = new();
    private Vector2 _direction = Vector2.Right;
    private Node _source;
    private float _remainingLifetime;
    private float _spinSpeed;
    private bool _isConsumed;

    /// <summary>
    /// 必须在 AddChild 前调用，确保 _Ready 时就能添加来源碰撞例外。
    /// </summary>
    public void Configure(
        Vector2 direction,
        Node source,
        float damage,
        float speed,
        float lifetime
    )
    {
        _direction = direction.IsZeroApprox()
            ? Vector2.Right
            : direction.Normalized();
        _source = source;
        Damage = Mathf.Max(damage, 0.0f);
        Speed = Mathf.Max(speed, 1.0f);
        Lifetime = Mathf.Max(lifetime, 0.05f);
    }

    public override void _Ready()
    {
        AddToGroup(ErasableGroup);
        AddToGroup(EnemyProjectileGroup);

        _random.Randomize();
        _remainingLifetime = Mathf.Max(Lifetime, 0.05f);
        Rotation = _direction.Angle();
        _spinSpeed = _random.RandfRange(
            -Mathf.Max(MaximumSpinSpeed, 0.0f),
            Mathf.Max(MaximumSpinSpeed, 0.0f)
        );

        SelectRandomScrapTexture();

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

        Rotation += _spinSpeed * physicsDelta;

        Vector2 intendedMotion = _direction * Speed * physicsDelta;
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

        if(IsDamageTarget(collider) && collider is IDamageable damageable)
        {
            damageable.TakeDamage(
                Damage,
                this,
                collision.GetPosition(),
                _direction
            );
        }

        // 碎片为一次性投射物：撞到角色、敌人或墙壁都会消失。
        Consume();
    }

    public void Erase(Node source)
    {
        Consume();
    }

    private void SelectRandomScrapTexture()
    {
        Sprite2D sprite = GetNodeOrNull<Sprite2D>("Sprite2D");

        if(sprite == null)
            return;

        Texture2D[] availableTextures =
        {
            ScrapTexture01,
            ScrapTexture02,
            ScrapTexture03
        };
        int firstIndex = _random.RandiRange(0, availableTextures.Length - 1);

        for(int offset = 0; offset < availableTextures.Length; offset++)
        {
            Texture2D texture = availableTextures[
                (firstIndex + offset) % availableTextures.Length
            ];

            if(texture == null)
                continue;

            sprite.Texture = texture;
            return;
        }
    }

    private static bool IsDamageTarget(Node collider)
    {
        if(collider is not IDamageable)
            return false;

        return
            collider is Player ||
            collider.IsInGroup("player") ||
            collider.IsInGroup("Player") ||
            collider.IsInGroup("enemy");
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
