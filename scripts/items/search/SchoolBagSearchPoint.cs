using Godot;
using System.Collections.Generic;

/// <summary>
/// 战斗房清理后出现的书包搜索点。玩家靠近并左键点击书包后，
/// 将创口贴和随机材料直接放入玩家背包。
/// </summary>
public partial class SchoolBagSearchPoint : Area2D
{
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

    public bool Searched { get; private set; }

    private readonly RandomNumberGenerator _random = new();
    private Sprite2D _sprite;
    private Label _prompt;
    private Player _nearbyPlayer;

    public override void _Ready()
    {
        CollisionLayer = 0;
        CollisionMask = 0;
        SetCollisionMaskValue(2, true);
        Monitoring = true;
        Monitorable = true;
        SetProcessUnhandledInput(true);
        _random.Randomize();

        _sprite = GetNodeOrNull<Sprite2D>("Sprite2D");
        _prompt = GetNodeOrNull<Label>("Prompt");

        if(_prompt != null)
        {
            _prompt.Visible = false;
            _prompt.AddThemeFontOverride("font", UiPalette.PixelFont);
            _prompt.AddThemeColorOverride("font_color", Colors.White);
            _prompt.AddThemeColorOverride(
                "font_outline_color",
                Colors.Black
            );
            _prompt.AddThemeConstantOverride("outline_size", 3);
        }

        BodyEntered += OnBodyEntered;
        BodyExited += OnBodyExited;
    }

    public override void _PhysicsProcess(double delta)
    {
        if(_nearbyPlayer != null || Searched)
            return;

        foreach(Node2D body in GetOverlappingBodies())
        {
            if(body is not Player player)
                continue;

            SetNearbyPlayer(player);
            break;
        }
    }

    public override void _UnhandledInput(InputEvent inputEvent)
    {
        if(
            Searched ||
            inputEvent is not InputEventMouseButton mouseButton ||
            !mouseButton.Pressed ||
            mouseButton.ButtonIndex != MouseButton.Left ||
            !IsMouseOverBag()
        )
        {
            return;
        }

        if(!GodotObject.IsInstanceValid(_nearbyPlayer))
        {
            FloatingDamageNumber.SpawnMessage(
                this,
                "请靠近书包后搜索",
                UiPalette.WarningCoral,
                new Vector2(0.0f, -44.0f),
                18.0f,
                0.9f,
                13
            );
            return;
        }

        Search(_nearbyPlayer);
        GetViewport().SetInputAsHandled();
    }

    public bool Search(Player player)
    {
        if(Searched || !GodotObject.IsInstanceValid(player))
            return false;

        InventoryModel inventory =
            player.GetNodeOrNull<InventoryModel>("Inventory");

        if(inventory == null)
            return false;

        List<string> acquiredNames = new();
        int acquiredCount = 0;

        if(BandageItem != null && inventory.TryAddItem(BandageItem, 1))
        {
            acquiredNames.Add(BandageItem.DisplayName);
            acquiredCount++;
        }

        if(MaterialItems.Count > 0)
        {
            int minimumRolls = Mathf.Max(MinimumMaterialRolls, 1);
            int maximumRolls = Mathf.Max(
                MaximumMaterialRolls,
                minimumRolls
            );
            int rollCount = _random.RandiRange(
                minimumRolls,
                maximumRolls
            );

            for(int roll = 0; roll < rollCount; roll++)
            {
                InventoryItemData material = MaterialItems[
                    _random.RandiRange(0, MaterialItems.Count - 1)
                ];

                if(material == null)
                    continue;

                int quantity = _random.RandiRange(
                    1,
                    Mathf.Max(MaximumMaterialQuantity, 1)
                );

                if(!inventory.TryAddItem(material, quantity))
                    continue;

                acquiredNames.Add($"{material.DisplayName}×{quantity}");
                acquiredCount += quantity;
            }
        }

        if(acquiredCount <= 0)
        {
            FloatingDamageNumber.SpawnMessage(
                this,
                "背包空间或负重不足",
                UiPalette.WarningCoral,
                new Vector2(0.0f, -44.0f),
                18.0f,
                1.0f,
                13
            );
            return false;
        }

        Searched = true;

        if(_sprite != null && OpenedTexture != null)
            _sprite.Texture = OpenedTexture;

        if(_prompt != null)
        {
            _prompt.Text = "已搜索";
            _prompt.Visible = true;
        }

        FloatingDamageNumber.SpawnMessage(
            this,
            $"获得：{string.Join("、", acquiredNames)}",
            UiPalette.ChargeGold.Lightened(0.2f),
            new Vector2(0.0f, -48.0f),
            20.0f,
            1.2f,
            13
        );
        EmitSignal(SignalName.SearchCompleted, acquiredCount);
        return true;
    }

    private bool IsMouseOverBag()
    {
        if(_sprite?.Texture == null)
            return false;

        Vector2 localMouse = ToLocal(GetGlobalMousePosition()) -
            _sprite.Position;
        Vector2 scale = new(
            Mathf.Abs(_sprite.Scale.X),
            Mathf.Abs(_sprite.Scale.Y)
        );
        Vector2 halfSize = _sprite.Texture.GetSize() * scale * 0.5f;

        return Mathf.Abs(localMouse.X) <= halfSize.X &&
            Mathf.Abs(localMouse.Y) <= halfSize.Y;
    }

    private void OnBodyEntered(Node2D body)
    {
        if(body is Player player)
            SetNearbyPlayer(player);
    }

    private void OnBodyExited(Node2D body)
    {
        if(body != _nearbyPlayer)
            return;

        _nearbyPlayer = null;

        if(_prompt != null && !Searched)
            _prompt.Visible = false;
    }

    private void SetNearbyPlayer(Player player)
    {
        _nearbyPlayer = player;

        if(_prompt != null && !Searched)
        {
            _prompt.Text = "左键搜索书包";
            _prompt.Visible = true;
        }
    }

    public override void _ExitTree()
    {
        BodyEntered -= OnBodyEntered;
        BodyExited -= OnBodyExited;
    }
}
