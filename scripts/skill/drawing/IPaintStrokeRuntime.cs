using Godot;

/// <summary>
/// 为需要复用绘画输入、长度和冷却流程，但拥有自定义实时表现的武器提供扩展点。
/// </summary>
public interface IPaintStrokeRuntime
{
    bool ShouldStampPaintCanvas { get; }

    Vector2 ConstrainPaintSkillPoint(Vector2 globalPoint);

    void OnPaintSkillPointAccepted(Vector2 globalPoint);
}
