using Godot;

public abstract partial class WeaponBase :
    Node2D,
    IPaintSkillWeapon,
    ISharedSkillChargeSource
{
    [ExportGroup("Shared Skill Charge")]
    [Export(PropertyHint.Range, "0,100,0.1")]
    public float NormalHitChargePercent { get; set; } = 2.5f;

    [Export(PropertyHint.Range, "0,100,0.1")]
    public float SkillHitChargePercent { get; set; } = 1.0f;

    [ExportGroup("Paint Skill")]
    [Export(PropertyHint.Range, "0,120,0.1")]
    public float SkillCooldownDuration { get; set; } = 8.0f;

    [Export]
    public PaintBrushData PaintBrushData { get; set; }

    /// <summary>
    /// 当前武器主动技能生成的持续伤害区域场景。
    /// Pencil 和 Chalk 分别在各自场景中配置不同派生区域。
    /// </summary>
    [Export]
    public PackedScene SkillDamageAreaScene { get; set; }

    [Export]
    public AudioStream SkillLoopSFX { get; set; }

    public WeaponInstance Instance { get; private set; } = null!;

    public WeaponData Data => Instance.Data;

    public bool IsSelected { get; private set; }

    private float _attackTimer;

    // 具体武器可以覆盖自己的自动攻击冷却；默认使用通用武器数据。
    protected virtual float AutoAttackCooldown => Data.AttackInterval;

    public bool IsEquipped { get; private set; }

    private bool _externalAttackBlocked;
    private bool _isUsingPaintSkill;
    private bool _paintSkillChargeConsumedOnBegin;
    private SharedSkillCharge _sharedSkillCharge;
    private AudioStreamPlayer _skillLoopPlayer;
    private bool _skillLoopRequested;

    /// <summary>
    /// 实时生效的技能需要在开始时消耗充能，避免通过短划或取消无限使用。
    /// 普通绘制武器仍在有效笔迹提交时消耗。
    /// </summary>
    protected virtual bool ConsumePaintSkillOnBegin => false;

    public bool IsUsingPaintSkill => _isUsingPaintSkill;
    public float CurrentSkillCharge =>
        _sharedSkillCharge == null ? 0.0f : _sharedSkillCharge.CurrentCharge;
    public float SkillCooldownRemaining =>
        Instance == null ? 0.0f : Instance.SkillCooldownRemaining;
    public float SkillCooldownRatio =>
        Instance == null ? 0.0f : Instance.SkillCooldownRatio;

    public float SharedSkillChargePercent =>
        Mathf.Max(NormalHitChargePercent, 0.0f);

    public Node SharedSkillChargeOwner => FindSkillDamageSource();

    public void SetEquipped(bool equipped)
    {
        if (IsEquipped == equipped)
            return;

        IsEquipped = equipped;
        RefreshVisibility();
    }

    public void Initialize(WeaponInstance instance)
    {
        Instance = instance;
        SyncSkillCooldownDuration();
        _attackTimer = instance.CooldownRemaining;
        _sharedSkillCharge = FindSharedSkillCharge();

        OnInitialized();
    }

    public override void _Process(double delta)
    {
        // 武器尚未初始化（Initialize 未调用）时跳过处理，避免空引用
        if (Instance == null)
            return;

        float deltaTime = (float)delta;

        // Keep runtime Inspector edits and the weapon-slot cooldown mask in sync.
        SyncSkillCooldownDuration();

        Instance.SkillCooldownRemaining = Mathf.Max(
            0.0f,
            Instance.SkillCooldownRemaining - deltaTime
        );

        // 未选中时，冷却仍然继续计算。
        // 否则玩家可以通过切武器冻结冷却。
        _attackTimer = Mathf.Max(
            0f,
            _attackTimer - deltaTime
        );

        Instance.CooldownRemaining = _attackTimer;

        if (!ShouldRunAutoAttack())
            return;

        if (IsAttackBlocked)
            return;

        if (_attackTimer > 0f)
            return;

        AutoAttack();

        _attackTimer = Mathf.Max(0f, AutoAttackCooldown);
        Instance.CooldownRemaining = _attackTimer;
    }

    public void SetSelected(bool selected)
    {
        if (IsSelected != selected)
        {
            IsSelected = selected;

            if (!selected)
                CancelSkill();

            OnSelectionChanged(selected);
        }

        RefreshVisibility();
    }

    protected virtual void OnInitialized()
    {
    }

    protected virtual void OnSelectionChanged(bool selected)
    {
    }

    protected virtual void CancelSkill()
    {
        CancelPaint();
    }

    protected abstract void AutoAttack();

    public bool CanBeginPaint()
    {
        return Instance != null &&
               IsSelected &&
               !_isUsingPaintSkill &&
               PaintBrushData != null &&
               Instance.SkillCooldownRemaining <= 0.0f &&
               _sharedSkillCharge != null &&
               _sharedSkillCharge.IsReady;
    }

    public void BeginPaint()
    {
        if (!CanBeginPaint())
            return;

        _paintSkillChargeConsumedOnBegin = false;

        if (ConsumePaintSkillOnBegin)
        {
            if (_sharedSkillCharge == null || !_sharedSkillCharge.TryConsume())
                return;

            _paintSkillChargeConsumedOnBegin = true;
        }

        _isUsingPaintSkill = true;
        SetExternalAttackBlocked(true);

        StartSkillLoopSFX();
        OnPaintSkillStarted();
    }

    public void CommitPaint(PaintStrokeResult result)
    {
        if (!_isUsingPaintSkill)
            return;

        if (
            !_paintSkillChargeConsumedOnBegin &&
            (_sharedSkillCharge == null || !_sharedSkillCharge.TryConsume())
        )
        {
            CancelPaint();
            return;
        }

        StartSkillCooldown();

        _isUsingPaintSkill = false;
        _paintSkillChargeConsumedOnBegin = false;
        SetExternalAttackBlocked(false);

        StopSkillLoopSFX();


        GD.Print(
            $"{Data.DisplayName}绘制完成：长度 {result.TotalLength:F1}，" +
            $"持续 {result.Duration:F2} 秒，" +
            $"平均速度 {result.AverageSpeed:F1}"
        );

        OnPaintSkillCommitted(result);
    }


    public void CancelPaint()
    {
        bool wasUsingPaintSkill = _isUsingPaintSkill;
        bool shouldStartCooldown =
            wasUsingPaintSkill && _paintSkillChargeConsumedOnBegin;

        _isUsingPaintSkill = false;
        _paintSkillChargeConsumedOnBegin = false;
        SetExternalAttackBlocked(false);

        StopSkillLoopSFX();

        if (shouldStartCooldown)
            StartSkillCooldown();

        if (wasUsingPaintSkill)
            OnPaintSkillCancelled();
    }

    protected virtual void OnPaintSkillStarted()
    {
    }

    protected virtual void OnPaintSkillCommitted(
        PaintStrokeResult result
    )
    {
        SpawnSkillDamageArea(result);
    }

    /// <summary>
    /// 技能笔迹有效提交后生成独立伤害区域。
    /// 这里只负责生成；伤害计时、减速和销毁由 SkillDamageArea 自己处理。
    /// </summary>
    private void SpawnSkillDamageArea(
        PaintStrokeResult result
    )
    {
        if (SkillDamageAreaScene == null || result == null)
        {
            GD.PushWarning(
                $"{Name} 没有配置 SkillDamageAreaScene。"
            );
            return;
        }


        Node instance = SkillDamageAreaScene.Instantiate();


        if (instance is not SkillDamageArea skillArea)
        {
            GD.PushError(
                $"{Name} 的技能场景根节点必须继承 SkillDamageArea。"
            );
            instance.QueueFree();
            return;
        }


        skillArea.Initialize(
            FindSkillDamageSource(),
            result,
            Mathf.Max(SkillHitChargePercent, 0.0f)
        );


        GetTree().CurrentScene.AddChild(skillArea);
    }


    private Node FindSkillDamageSource()
    {
        Node ancestor = GetParent();


        while (ancestor != null)
        {
            if (ancestor is CharacterBody2D)
                return ancestor;


            ancestor = ancestor.GetParent();
        }


        return this;
    }

    protected virtual void OnPaintSkillCancelled()
    {
    }

    public void SetExternalAttackBlocked(
        bool blocked
    )
    {
        _externalAttackBlocked = blocked;
    }

    protected bool IsAttackBlocked =>
        _externalAttackBlocked;

    private SharedSkillCharge FindSharedSkillCharge()
    {
        Node ancestor = GetParent();

        while (ancestor != null)
        {
            SharedSkillCharge charge =
                ancestor.GetNodeOrNull<SharedSkillCharge>("SharedSkillCharge");

            if (charge != null)
                return charge;

            ancestor = ancestor.GetParent();
        }

        GD.PushWarning($"{Name} 找不到 SharedSkillCharge，主动技能暂不可用。");
        return null;
    }

    private void SyncSkillCooldownDuration()
    {
        if (Instance == null)
            return;

        Instance.SkillCooldownDuration = Mathf.Max(
            SkillCooldownDuration,
            0.0f
        );
    }

    private void StartSkillCooldown()
    {
        SyncSkillCooldownDuration();
        Instance.SkillCooldownRemaining = Instance.SkillCooldownDuration;
    }

    private bool ShouldRunAutoAttack()
    {
        if (!IsEquipped || Instance == null)
            return false;

        if (IsSelected)
            return true;

        return Data.OperationMode == WeaponOperationMode.PassiveOnly ||
               Data.OperationMode == WeaponOperationMode.Hybrid;
    }

    private void RefreshVisibility()
    {
        if (!IsEquipped || Instance == null)
        {
            Visible = false;
            return;
        }

        Visible = IsSelected || Data.ShowVisualWhenUnselected;
    }

    private void EnsureSkillLoopPlayer()
    {
        if (GodotObject.IsInstanceValid(_skillLoopPlayer))
            return;

        _skillLoopPlayer = new AudioStreamPlayer
        {
            Name = "SkillLoopSFXPlayer",
            Bus = "SFX",
            ProcessMode = ProcessModeEnum.Always
        };

        AddChild(_skillLoopPlayer);

        _skillLoopPlayer.Finished +=
            OnSkillLoopSFXFinished;
    }
    private void StartSkillLoopSFX()
    {
        if (SkillLoopSFX == null)
            return;

        EnsureSkillLoopPlayer();

        _skillLoopRequested = true;
        _skillLoopPlayer.Stream = SkillLoopSFX;
        _skillLoopPlayer.Play();
    }

    private void OnSkillLoopSFXFinished()
    {
        if (
            !_skillLoopRequested ||
            !_isUsingPaintSkill ||
            !GodotObject.IsInstanceValid(_skillLoopPlayer) ||
            _skillLoopPlayer.Stream == null
        )
        {
            return;
        }

        _skillLoopPlayer.Play();
    }

    private void StopSkillLoopSFX()
    {
        _skillLoopRequested = false;

        if (!GodotObject.IsInstanceValid(_skillLoopPlayer))
            return;

        _skillLoopPlayer.Stop();
        _skillLoopPlayer.Stream = null;
    }

}
