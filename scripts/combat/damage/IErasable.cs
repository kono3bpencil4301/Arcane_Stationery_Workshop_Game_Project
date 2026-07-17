using Godot;

/// <summary>
/// 可被橡皮擦清除的弹幕、污染物或异常效果。
/// 实现节点还应加入 "erasable" 组，供橡皮擦进行范围查询。
/// </summary>
public interface IErasable
{
    int EraseLevel { get; }

    void Erase(Node source);
}
