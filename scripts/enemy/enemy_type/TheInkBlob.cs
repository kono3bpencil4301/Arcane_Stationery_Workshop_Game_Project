using Godot;


[GlobalClass]
public partial class TheInkBlob : EnemyBase
{


    [ExportCategory("Pollution Trail")]


    [Export]
    public float PollutionRadius = 40f;


    /// <summary>单个瓦片离开墨晕团后保持污染的秒数；0 表示永久污染。</summary>
    [Export(PropertyHint.Range, "0,60,0.1")]
    public float PollutionDuration = 5.0f;


    [Export]
    public Color PollutionColor = new Color(0.0f, 0.0f, 0.0f, 0.42f);


    [Export(PropertyHint.Range, "0.1,1,0.05")]
    public float PollutedPlayerSpeedMultiplier = 0.8f;


    [Export(PropertyHint.Range, "0,30,0.1")]
    public float PollutionGraceDuration = 3.0f;


    [Export(PropertyHint.Range, "0,100,0.5")]
    public float PollutionDamagePerSecond = 2.0f;


    [ExportCategory("Ink Blob")]


    [Export]
    public StringName MoveAnimation = "default";


    /// <summary>
    /// 与玩家的水平距离小于该值时保持上一次朝向，避免在玩家附近反复翻转。
    /// </summary>
    [Export(PropertyHint.Range, "0,64,1")]
    public float FacingDeadZone = 8.0f;


    [ExportCategory("Damage Feedback")]


    [Export]
    public Color DamageFlashColor = new Color(1.0f, 0.25f, 0.25f, 1.0f);


    [Export(PropertyHint.Range, "0.01,1,0.01")]
    public double DamageFlashDuration = 0.05;


    private AnimatedSprite2D _animatedSprite;


    private ulong _damageFlashVersion;


    private Color _normalSpriteColor = Colors.White;


    private bool _isFacingLeft;


    private InkPollutionField _pollutionField;


    public TheInkBlob()
    {
        // 墨团的默认基础属性；场景 Inspector 中保存的值仍可覆盖它们。
        MaxHealth = 40f;
        MoveSpeed = 60f;
        MinimumInkCoinReward = 1;
        MaximumInkCoinReward = 3;
    }


    public override void _Ready()
    {
        base._Ready();


        _animatedSprite =
            GetNodeOrNull<AnimatedSprite2D>("AnimatedSprite2D");


        if(_animatedSprite != null)
        {
            // 固定保存正常颜色，避免连续伤害把闪烁色误当作恢复色。
            _normalSpriteColor = _animatedSprite.Modulate;
            _isFacingLeft = _animatedSprite.FlipH;
        }


        if(
            _animatedSprite != null &&
            _animatedSprite.SpriteFrames != null &&
            _animatedSprite.SpriteFrames.HasAnimation(MoveAnimation)
        )
        {
            _animatedSprite.Play(MoveAnimation);
        }
        else
        {
            GD.PushWarning(
                $"{Name} 找不到移动动画: {MoveAnimation}"
            );
        }


        ResolvePollutionField();
        PolluteCurrentPosition();
    }


    public override void _PhysicsProcess(double delta)
    {
        base._PhysicsProcess(delta);


        if(!IsDead)
            PolluteCurrentPosition();
    }



    protected override void MoveToTarget()
    {
        base.MoveToTarget();


        UpdateFacingDirection();
    }


    /// <summary>
    /// 只翻转视觉节点，不修改 CharacterBody2D 与碰撞体的 Transform。
    /// 死区内沿用上一次朝向，避免分离力和玩家推动造成逐帧抖动。
    /// </summary>
    private void UpdateFacingDirection()
    {
        if(
            !GodotObject.IsInstanceValid(_animatedSprite) ||
            !GodotObject.IsInstanceValid(Target)
        )
        {
            return;
        }


        float horizontalDistance =
            Target.GlobalPosition.X - GlobalPosition.X;


        if(Mathf.Abs(horizontalDistance) <= Mathf.Max(FacingDeadZone, 0.0f))
            return;


        bool shouldFaceLeft = horizontalDistance < 0.0f;


        if(shouldFaceLeft == _isFacingLeft)
            return;


        _isFacingLeft = shouldFaceLeft;
        _animatedSprite.FlipH = _isFacingLeft;
    }


    protected override void FlashDamage()
    {
        if(!GodotObject.IsInstanceValid(_animatedSprite))
        {
            _animatedSprite =
                GetNodeOrNull<AnimatedSprite2D>("AnimatedSprite2D");
        }


        if(_animatedSprite == null)
        {
            return;
        }


        ulong flashVersion = ++_damageFlashVersion;


        _animatedSprite.Modulate = DamageFlashColor;


        GetTree()
            .CreateTimer(Mathf.Max(DamageFlashDuration, 0.01))
            .Timeout += () =>
            {
                // 连续受击时只允许最后一次计时恢复颜色。
                if(
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
        SpawnInkEffect();


        DropMaterial();


        base.Die();
    }



    private void SpawnInkEffect()
    {
        PolluteCurrentPosition();


        GD.Print(
            "Ink Blob leaves ink stain"
        );
    }


    private void ResolvePollutionField()
    {
        Node currentScene = GetTree().CurrentScene;


        if(currentScene == null)
            return;


        _pollutionField = currentScene
            .GetNodeOrNull<InkPollutionField>("InkPollutionField");


        if(GodotObject.IsInstanceValid(_pollutionField))
            return;


        TileMapLayer groundLayer = currentScene
            .GetNodeOrNull<TileMapLayer>("GroundTileMapLayer");


        if(groundLayer == null)
        {
            GD.PushWarning(
                $"{Name} 找不到 GroundTileMapLayer，无法生成污染瓦片。"
            );
            return;
        }


        _pollutionField = new InkPollutionField
        {
            Name = "InkPollutionField"
        };
        _pollutionField.Initialize(
            groundLayer,
            PollutionColor,
            PollutionDuration,
            PollutedPlayerSpeedMultiplier,
            PollutionGraceDuration,
            PollutionDamagePerSecond
        );
        currentScene.AddChild(_pollutionField);
    }


    private void PolluteCurrentPosition()
    {
        if(!GodotObject.IsInstanceValid(_pollutionField))
            ResolvePollutionField();


        _pollutionField?.PolluteAt(
            GlobalPosition,
            Mathf.Max(PollutionRadius, 0.0f)
        );
    }



    private void DropMaterial()
    {
        GD.Print(
            "Drop: 潮化墨滴"
        );


        /*
        后续:
        生成掉落物
        */
    }
}
