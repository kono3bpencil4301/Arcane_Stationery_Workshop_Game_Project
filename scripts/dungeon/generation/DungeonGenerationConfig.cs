using Godot;

[GlobalClass]
public partial class DungeonGenerationConfig : Resource
{
    public const int CorridorDistanceLowerBound = 4;
    public const int CorridorDistanceUpperBound = 10;

    [Export(PropertyHint.Range, "10,16,1")]
    public int MinimumRoomCount { get; set; } = 10;

    [Export(PropertyHint.Range, "10,16,1")]
    public int MaximumRoomCount { get; set; } = 16;

    [Export(PropertyHint.Range, "16,26,1")]
    public int MinimumRoomLength { get; set; } = 16;

    [Export(PropertyHint.Range, "16,26,1")]
    public int MaximumRoomLength { get; set; } = 26;

    [Export(PropertyHint.Range, "10,18,1")]
    public int MinimumRoomWidth { get; set; } = 10;

    [Export(PropertyHint.Range, "10,18,1")]
    public int MaximumRoomWidth { get; set; } = 18;

    [Export(PropertyHint.Range, "4,10,1")]
    public int MinimumCorridorDistance { get; set; } = 4;

    [Export(PropertyHint.Range, "4,10,1")]
    public int MaximumCorridorDistance { get; set; } = 10;

    [Export(PropertyHint.Range, "1,3,1")]
    public int ExtraCrossConnectionCount { get; set; } = 1;

    [Export(PropertyHint.Range, "1,100,1")]
    public int MaximumGenerationAttempts { get; set; } = 20;
}
