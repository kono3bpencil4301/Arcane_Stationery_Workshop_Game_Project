namespace scripts.items.search;

using Godot;

[GlobalClass]
public partial class SearchPointData : Area2D
{

    public enum SearchState
    {
        Unsearched,
        Searching,
        Searched

    }
    [Signal]
    public delegate void SearchCompletedEventHandler(int itemCount);

    [ExportCategory("Visual")]

    [Export]
    public Texture2D OpenedTexture { get; set; }

    [ExportCategory("Loot")]

    [Export]
    public InventoryItemData BandageItem { get; set; }

    [Export]
    public Godot.Collections.Array<InventoryItemData> MaterialItems
    { get; set; } = new();

    [Export(PropertyHint.Range, "1,10,1")]
    public int MinimumMaterialRolls { get; set; } = 2;

    [Export(PropertyHint.Range, "1,10,1")]
    public int MaximumMaterialRolls { get; set; } = 4;

    [Export(PropertyHint.Range, "1,10,1")]
    public int MaximumMaterialQuantity { get; set; } = 2;

    public bool Searched { get; set; }

    [ExportCategory("SearchTime")]

    [Export(PropertyHint.Range, "0.1,10,0.1")]
    public float SearchTime { get; set; } = 1.5f;

    [ExportCategory("Progress Ring")]

    [Export]
    public Texture2D RingBackTexture { get; set; }

    [Export]
    public Texture2D RingFillTexture { get; set; }

    [ExportCategory("SFX")]

    [Export]
    public AudioStream SearchingSFX { get; set; }

    [Export]
    public AudioStream SearchCompleteSFX { get; set; }
}
