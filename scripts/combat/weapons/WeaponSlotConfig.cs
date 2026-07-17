using Godot;

/// <summary>
/// 武器槽位配置，在检查器中分配每个槽位的武器场景。
/// </summary>
[GlobalClass]
public partial class WeaponSlotConfig : Resource
{
    /// <summary>
    /// 武器场景（必须继承 WeaponBase）
    /// </summary>
    [Export] public PackedScene? ControllerScene { get; set; }

    /// <summary>
    /// 是否为默认武器（不可丢弃）
    /// </summary>
    [Export] public bool IsDefault { get; set; }
}
