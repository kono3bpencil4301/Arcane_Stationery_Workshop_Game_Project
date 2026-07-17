using Godot;

[GlobalClass]
public partial class WeaponData : Resource
{
    [ExportGroup("基础信息")]
    [Export] public string WeaponId { get; set; } = "";
    [Export] public string DisplayName { get; set; } = "";
    [Export] public Texture2D? Icon { get; set; }

    // 对应 PencilWeapon.tscn、ChalkWeapon.tscn 等
#pragma warning disable CS8632 // 只能在 "#nullable" 注释上下文内的代码中使用可为 null 的引用类型的注释。

    [Export] public PackedScene? ControllerScene { get; set; }
#pragma warning restore CS8632 // 只能在 "#nullable" 注释上下文内的代码中使用可为 null 的引用类型的注释。


    [ExportGroup("物品属性")]
    [Export] public float Weight { get; set; } = 1f;

    // 任务武器或特殊剧情武器可以设为 false
    [Export] public bool AllowDrop { get; set; } = true;

    [ExportGroup("基础战斗")]
    [Export] public float AttackInterval { get; set; } = 1f;
    [Export] public float BaseDamage { get; set; } = 10f;
    [Export] public float AttackRange { get; set; } = 320f;

    [ExportGroup("技能")]
    [Export] public float MaxCharge { get; set; } = 100f;
    [Export] public float SkillCost { get; set; } = 100f;

    [ExportGroup("运行规则")]
    [Export]
    public WeaponOperationMode OperationMode { get; set; }
    = WeaponOperationMode.SelectedOnly;

    [Export]
    public float PassiveAttackInterval { get; set; } = 1f;

    [Export]
    public bool ShowVisualWhenUnselected { get; set; } = false;
}
