using Godot;

/// <summary>
/// 处理底部快捷槽消耗品的持续使用、移动锁定与环形进度表现。
/// </summary>
public partial class ConsumableUseController : Node2D
{
    private const string BandageItemId = "bandage";

    [ExportCategory("Bandage")]

    [Export(PropertyHint.Range, "0.1,10,0.1")]
    public float UseDurationSeconds { get; set; } = 1.5f;

    [Export(PropertyHint.Range, "1,1000,1")]
    public float HealAmount { get; set; } = 35.0f;

    [ExportCategory("Progress Ring")]

    [Export]
    public Texture2D RingBackTexture { get; set; }

    [Export]
    public Texture2D RingFillTexture { get; set; }

    public bool IsUsing { get; private set; }

    private Player _player;
    private EquipmentModel _equipment;
    private TextureProgressBar _progressRing;
    private InventoryEntry _activeEntry;
    private int _activeSlot = -1;
    private double _elapsedSeconds;

    public override void _Ready()
    {
        SetProcessInput(true);
        _player = GetParentOrNull<Player>();
        _equipment = _player?.GetNodeOrNull<EquipmentModel>("Equipment");
        CreateProgressRing();
    }

    public bool TryUseSlot(int slotIndex)
    {
        if(
            IsUsing ||
            !GodotObject.IsInstanceValid(_player) ||
            _player.IsDead ||
            !GodotObject.IsInstanceValid(_equipment)
        )
        {
            return false;
        }

        InventoryEntry entry = _equipment.GetSlot(slotIndex);

        if(
            entry?.Data == null ||
            entry.Data.Category != InventoryItemCategory.Consumable ||
            !entry.Data.IsQuickUsable
        )
        {
            return false;
        }

        if(entry.Data.ItemId != BandageItemId)
        {
            ShowMessage("该消耗品暂时无法使用");
            return false;
        }

        if(_player.CurrentHealth >= _player.MaxHealth - 0.001f)
        {
            ShowMessage("当前血量已满，无法使用");
            return false;
        }

        _activeEntry = entry;
        _activeSlot = slotIndex;
        _elapsedSeconds = 0.0;
        IsUsing = true;
        _player.SetMovementLocked(true);

        if(_progressRing != null)
        {
            _progressRing.Value = 0.0;
            _progressRing.Visible = true;
        }

        return true;
    }

    public override void _Process(double delta)
    {
        if(!IsUsing)
            return;

        if(
            !GodotObject.IsInstanceValid(_player) ||
            _player.IsDead ||
            !GodotObject.IsInstanceValid(_equipment) ||
            _equipment.GetSlot(_activeSlot) != _activeEntry
        )
        {
            FinishUse(false);
            return;
        }

        double duration = Mathf.Max(UseDurationSeconds, 0.1f);
        _elapsedSeconds = Mathf.Min(
            _elapsedSeconds + delta,
            duration
        );
        float ratio = (float)(_elapsedSeconds / duration);

        if(_progressRing != null)
            _progressRing.Value = ratio * 100.0f;

        if(_elapsedSeconds >= duration)
            CompleteUse();
    }

    private void CompleteUse()
    {
        if(_player.CurrentHealth >= _player.MaxHealth - 0.001f)
        {
            ShowMessage("当前血量已满，无法使用");
            FinishUse(false);
            return;
        }

        if(!_equipment.TryConsumeFromSlot(_activeSlot, 1))
        {
            FinishUse(false);
            return;
        }

        _player.RestoreHealth(HealAmount);
        FinishUse(true);
    }

    private void FinishUse(bool completed)
    {
        IsUsing = false;
        _activeEntry = null;
        _activeSlot = -1;
        _elapsedSeconds = 0.0;

        if(GodotObject.IsInstanceValid(_player))
            _player.SetMovementLocked(false);

        if(_progressRing != null)
        {
            _progressRing.Visible = false;
            _progressRing.Value = 0.0;
        }
    }

    public override void _Input(InputEvent inputEvent)
    {
        if(
            !IsUsing ||
            inputEvent is not InputEventMouseButton mouseButton ||
            !mouseButton.Pressed ||
            mouseButton.ButtonIndex != MouseButton.Right
        )
        {
            return;
        }

        FinishUse(false);
        GetViewport()?.SetInputAsHandled();
    }

    private void CreateProgressRing()
    {
        if(RingBackTexture == null || RingFillTexture == null)
        {
            GD.PushWarning("ConsumableUseController 缺少环形进度纹理。");
            return;
        }

        Vector2 size = RingBackTexture.GetSize();
        _progressRing = new TextureProgressBar
        {
            Name = "UseProgressRing",
            TextureUnder = RingBackTexture,
            TextureProgress = RingFillTexture,
            FillMode = (int)TextureProgressBar.FillModeEnum.Clockwise,
            RadialFillDegrees = 360.0f,
            RadialCenterOffset = Vector2.Zero,
            MaxValue = 100.0,
            Value = 0.0,
            MouseFilter = Control.MouseFilterEnum.Ignore,
            Position = -size * 0.5f,
            Size = size,
            PivotOffset = size * 0.5f,
            ZIndex = 80,
            Visible = false
        };
        AddChild(_progressRing);
    }

    private void ShowMessage(string message)
    {
        FloatingDamageNumber.SpawnMessage(
            _player,
            message,
            UiPalette.WarningCoral.Lightened(0.2f),
            new Vector2(0.0f, -48.0f),
            24.0f,
            1.1f,
            14
        );
    }

    public override void _ExitTree()
    {
        if(IsUsing && GodotObject.IsInstanceValid(_player))
            _player.SetMovementLocked(false);
    }
}
