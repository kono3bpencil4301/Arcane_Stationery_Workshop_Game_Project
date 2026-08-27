using Godot;
using System;

public partial class WeaponSlotView : PanelContainer
{
    private WeaponManager _manager;
    private int _slotIndex;
    private TextureRect _icon;
    private Label _cooldownLabel;
    private Label _keyLabel;
    private ShaderMaterial _cooldownMaterial;
    private WeaponInstance _lastInstance;
    private bool _lastSelected;

    public event Action<string, int> DropRequested;

    public void Bind(WeaponManager manager, int slotIndex)
    {
        _manager = manager;
        _slotIndex = slotIndex;
    }

    public override void _Ready()
    {
        CustomMinimumSize = new Vector2(58, 58);
        MouseDefaultCursorShape = CursorShape.PointingHand;
        UiPalette.BindIconControlAudio(this);
        AddThemeStyleboxOverride(
            "panel",
            UiPalette.MakePanel(UiPalette.Panel, UiPalette.ArchiveBlue, 1)
        );

        Control content = new();
        content.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        content.MouseFilter = MouseFilterEnum.Ignore;
        AddChild(content);

        _icon = new TextureRect
        {
            Position = new Vector2(7, 7),
            Size = new Vector2(44, 44),
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
            MouseFilter = MouseFilterEnum.Ignore,
            TextureFilter = TextureFilterEnum.Nearest
        };
        content.AddChild(_icon);

        _cooldownMaterial = new ShaderMaterial
        {
            Shader = GD.Load<Shader>(
                "res://shaders/ui/radial_cooldown_mask.gdshader"
            )
        };
        _cooldownMaterial.SetShaderParameter("ratio", 0.0f);

        ColorRect cooldownOverlay = new()
        {
            Position = new Vector2(7, 7),
            Size = new Vector2(44, 44),
            Color = Colors.White,
            Material = _cooldownMaterial,
            MouseFilter = MouseFilterEnum.Ignore
        };
        content.AddChild(cooldownOverlay);

        _cooldownLabel = new Label
        {
            Position = new Vector2(2, 0),
            Size = new Vector2(34, 17),
            MouseFilter = MouseFilterEnum.Ignore
        };
        UiPalette.StyleLabel(_cooldownLabel, 10);
        content.AddChild(_cooldownLabel);

        _keyLabel = new Label
        {
            Text = (_slotIndex + 1).ToString(),
            Position = new Vector2(39, 37),
            Size = new Vector2(16, 18),
            HorizontalAlignment = HorizontalAlignment.Center,
            MouseFilter = MouseFilterEnum.Ignore
        };
        UiPalette.StyleLabel(_keyLabel, 11);
        content.AddChild(_keyLabel);

        GuiInput += OnGuiInput;
        Refresh(true);
    }

    public override void _Process(double delta)
    {
        Refresh(false);
    }

    public override bool _CanDropData(Vector2 atPosition, Variant data)
    {
        return data.VariantType == Variant.Type.String;
    }

    public override void _DropData(Vector2 atPosition, Variant data)
    {
        DropRequested?.Invoke(data.AsString(), _slotIndex);
    }

    private void OnGuiInput(InputEvent inputEvent)
    {
        if (
            inputEvent is InputEventMouseButton mouseButton &&
            mouseButton.Pressed &&
            mouseButton.ButtonIndex == MouseButton.Left
        )
        {
            _manager?.SwitchWeapon(_slotIndex);
            AcceptEvent();
        }
    }

    private void Refresh(bool force)
    {
        if (_icon == null)
            return;

        WeaponInstance instance = _manager?.GetWeapon(_slotIndex);
        bool selected = _manager?.SelectedSlot == _slotIndex;

        if (force || instance != _lastInstance || selected != _lastSelected)
        {
            _icon.Texture = instance?.Data.Icon;
            _icon.Modulate = instance == null
                ? new Color(1, 1, 1, 0.15f)
                : Colors.White;

            Color border = instance == null
                ? UiPalette.ArchiveBlue.Darkened(0.35f)
                : selected
                    ? UiPalette.WarningCoral
                    : UiPalette.RingBlue;

            AddThemeStyleboxOverride(
                "panel",
                UiPalette.MakePanel(
                    selected ? UiPalette.PanelSoft : UiPalette.Panel,
                    border,
                    selected ? 2 : 1
                )
            );

            _lastInstance = instance;
            _lastSelected = selected;
        }

        float remaining = instance?.SkillCooldownRemaining ?? 0.0f;
        float duration = instance?.SkillCooldownDuration ?? 0.0f;
        float ratio = instance?.SkillCooldownRatio ?? 0.0f;
        _cooldownMaterial?.SetShaderParameter("ratio", ratio);
        _cooldownLabel.Text = remaining > 0.0f
            ? remaining.ToString("0.0")
            : "";
        TooltipText = instance == null
            ? $"武器槽 {_slotIndex + 1}（空）"
            : remaining > 0.0f
                ? $"{instance.Data.DisplayName}\n技能 CD {duration:0.0}s（剩余 {remaining:0.0}s）"
                : $"{instance.Data.DisplayName}\n技能 CD {duration:0.0}s（就绪）";
    }
}
