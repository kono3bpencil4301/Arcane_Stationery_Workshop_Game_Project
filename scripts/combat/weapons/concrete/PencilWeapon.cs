using Godot;
using System.Collections.Generic;

public partial class PencilWeapon : WeaponBase
{
    
    [Export]
    public float pencilThrustRange = 100.0f; // 铅笔 thrust 范围
    [Export]
    public float pencilThrustDamage = 10.0f; // 铅笔 thrust 伤害
    [Export]
    public float pencilThrustSpeed = 150.0f; // 铅笔 thrust 速度
    [Export]
    public float pencilThrustCooldown = 1.0f; // 铅笔 thrust 冷却时间
    [Export]
    public float pencilOrbitRadius = 32.0f; // 待机瞄准时与角色的距离
    [Export]
    public bool pencilSelfRotationEnabled = true; // 铅笔头是否自转朝向鼠标
    [Export(PropertyHint.Range, "-180,180,1")]
    public float pencilSelfRotationOffsetDegrees = 0.0f; // 贴图朝向修正

    private PencilState currentState = PencilState.Idle; // 铅笔当前状态
    private Vector2 pencilAimDirection = Vector2.Right;
    private float pencilThrustDistance = 0.0f; // 铅笔 thrust 距离
    private Area2D pencilHitArea;
    private readonly HashSet<ulong> hitEnemyIds = new();

    public override void _Ready()
    {
        pencilHitArea = GetNodeOrNull<Area2D>("Area2D");

        if (pencilHitArea == null)
        {
            GD.PushWarning("PencilWeapon 缺少 Area2D 命中区域");
            return;
        }

        pencilHitArea.Monitoring = true;
        pencilHitArea.BodyEntered += OnHitAreaBodyEntered;
    }

    protected override float AutoAttackCooldown => pencilThrustCooldown;

    protected override void AutoAttack()
    {
        if (currentState != PencilState.Idle)
            return;

        StartThrust();
        GD.Print("铅笔开始 thrust");
    }

    public override void _Process(double delta)
    {
        // 先更新鼠标瞄准，确保本帧触发攻击时使用最新方向。
        if (IsSelected)
            UpdateOrbitAim();

        // 调用基类 _Process，处理冷却计时和自动攻击。
        base._Process(delta);

        if (!IsSelected)
            return;

        float deltaTime = (float)delta;

        switch (currentState)
        {
            case PencilState.Idle:
                UpdateOrbitPosition();
                break;
            case PencilState.Thrust:
                UpdateThrust(deltaTime);
                break;
            case PencilState.Return:
                ReturnThrust(deltaTime);
                break;
        }
    }

    private void UpdateOrbitAim()
    {
        if (GetParent() is not Node2D weaponMount)
            return;

        // 鼠标控制的是铅笔绕角色的方位，不修改铅笔自身 Rotation。
        Vector2 localMousePosition =
            weaponMount.ToLocal(GetGlobalMousePosition());
        if (!localMousePosition.IsZeroApprox())
        {
            pencilAimDirection = localMousePosition.Normalized();

            if (pencilSelfRotationEnabled)
            {
                Rotation = pencilAimDirection.Angle() +
                    Mathf.DegToRad(pencilSelfRotationOffsetDegrees);
            }
        }
    }

    private void UpdateOrbitPosition()
    {
        Position = pencilAimDirection * Mathf.Max(pencilOrbitRadius, 0.0f);
    }


    void StartThrust()
    {
        // 每次新突刺都允许重新伤害敌人。
        hitEnemyIds.Clear();
        currentState = PencilState.Thrust;
        pencilThrustDistance = 0.0f;
    }

    private void OnHitAreaBodyEntered(Node2D body)
    {
        // Idle 状态只是围绕玩家瞄准，不造成接触伤害。
        // Return 仍属于同一次突刺，但 HashSet 会阻止重复伤害。
        if (!IsSelected || currentState == PencilState.Idle)
            return;

        TryDamageEnemy(body);
    }

    private void DamageOverlappingEnemies()
    {
        if (pencilHitArea == null)
            return;

        foreach (Node2D body in pencilHitArea.GetOverlappingBodies())
            TryDamageEnemy(body);
    }

    private void TryDamageEnemy(Node2D body)
    {
        if (!body.IsInGroup("enemy") || body is not IDamageable damageable)
            return;

        ulong enemyId = body.GetInstanceId();
        if (!hitEnemyIds.Add(enemyId))
            return;

        Vector2 hitDirection = GetGlobalThrustDirection();

        damageable.TakeDamage(
            Mathf.Max(pencilThrustDamage, 0.0f),
            this,
            GlobalPosition,
            hitDirection
        );
    }

    private Vector2 GetGlobalThrustDirection()
    {
        if (GetParent() is Node2D weaponMount)
        {
            Vector2 globalAimPoint =
                weaponMount.ToGlobal(pencilAimDirection);
            Vector2 direction =
                globalAimPoint - weaponMount.GlobalPosition;

            if (!direction.IsZeroApprox())
                return direction.Normalized();
        }

        return pencilAimDirection.Normalized();
    }

    void UpdateThrust(float delta)
    {
        float orbitRadius = Mathf.Max(pencilOrbitRadius, 0.0f);
        float thrustRange = Mathf.Max(pencilThrustRange, 0.0f);
        float moveSpeed = Mathf.Abs(pencilThrustSpeed);

        pencilThrustDistance = Mathf.Min(
            pencilThrustDistance + moveSpeed * delta,
            thrustRange
        );
        Position = pencilAimDirection * (orbitRadius + pencilThrustDistance);
        DamageOverlappingEnemies();

        if (Mathf.IsEqualApprox(pencilThrustDistance, thrustRange))
            currentState = PencilState.Return;
    }

    void ReturnThrust(float delta)
    {
        float orbitRadius = Mathf.Max(pencilOrbitRadius, 0.0f);
        pencilThrustDistance = Mathf.MoveToward(
            pencilThrustDistance,
            0.0f,
            Mathf.Abs(pencilThrustSpeed) * delta
        );
        Position = pencilAimDirection * (orbitRadius + pencilThrustDistance);
        DamageOverlappingEnemies();

        if (Mathf.IsZeroApprox(pencilThrustDistance))
            EndThrust();
    }

    void EndThrust()
    {
        pencilThrustDistance = 0.0f;
        currentState = PencilState.Idle;
        UpdateOrbitPosition();
    }

    protected override void CancelSkill()
    {
        base.CancelSkill();
        EndThrust();
    }
}
