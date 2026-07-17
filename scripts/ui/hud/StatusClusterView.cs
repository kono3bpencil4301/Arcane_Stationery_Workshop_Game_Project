using Godot;

public partial class StatusClusterView : Control
{
    private Player _player;
    private SharedSkillCharge _skillCharge;
    private RunHudState _runState;
    private Label _floorLabel;
    private Label _healthLabel;
    private Label _pollutionLabel;
    private Label _skillLabel;
    private ShaderMaterial _healthMaterial;
    private ShaderMaterial _pollutionMaterial;
    private ShaderMaterial _skillMaterial;

    public void Bind(
        Player player,
        SharedSkillCharge skillCharge,
        RunHudState runState
    )
    {
        _player = player;
        _skillCharge = skillCharge;
        _runState = runState;
    }

    public override void _Ready()
    {
        CustomMinimumSize = new Vector2(276, 105);
        Size = CustomMinimumSize;
        MouseFilter = MouseFilterEnum.Ignore;
        TextureFilter = TextureFilterEnum.Nearest;

        // Draw the three slanted bars first. Their left tips intentionally sit
        // behind the floor diamond, matching the original HUD silhouette.
        AddTexture("res://assets/ui/HUD/HUD-01_0003s_0002_MP-B.png");
        AddTexture("res://assets/ui/HUD/HUD-01_0001s_0002_HP-B.png");
        AddTexture("res://assets/ui/HUD/HUD-01_0002s_0002_PP-B.png");

        _pollutionMaterial = AddProgressTexture(
            "res://assets/ui/HUD/HUD-01_0003s_0001_MP-A.png",
            UiPalette.ArcaneViolet,
            69.0f / 276.0f,
            197.0f / 276.0f
        );
        _healthMaterial = AddProgressTexture(
            "res://assets/ui/HUD/HUD-01_0001s_0001_HP-A.png",
            UiPalette.HealthMagenta,
            88.0f / 276.0f,
            267.0f / 276.0f
        );
        _skillMaterial = AddProgressTexture(
            "res://assets/ui/HUD/HUD-01_0002s_0001_PP-A.png",
            UiPalette.ChargeGold,
            70.0f / 276.0f,
            248.0f / 276.0f
        );

        AddTexture("res://assets/ui/HUD/HUD-01_0003s_0000_MP-Line.png");
        AddTexture("res://assets/ui/HUD/HUD-01_0001s_0000_HP-Line.png");
        AddTexture("res://assets/ui/HUD/HUD-01_0002s_0000_PP-Line.png");

        // The diamond is the foreground cap: keep its complete layer stack
        // above every bar so no fill or outline cuts across it.
        AddTexture("res://assets/ui/HUD/HUD-01_0000s_0002_EXP-B.png");
        AddTexture("res://assets/ui/HUD/HUD-01_0000s_0001_EXP-A.png");
        AddTexture("res://assets/ui/HUD/HUD-01_0000s_0000_EXP-Line.png");

        _floorLabel = MakeLabel(new Rect2(9, 40, 88, 20), 11);
        _floorLabel.HorizontalAlignment = HorizontalAlignment.Center;
        _floorLabel.VerticalAlignment = VerticalAlignment.Center;

        _healthLabel = MakeLabel(new Rect2(102, 40, 154, 22), 12);
        _healthLabel.HorizontalAlignment = HorizontalAlignment.Left;
        _healthLabel.VerticalAlignment = VerticalAlignment.Center;

        _pollutionLabel = MakeLabel(new Rect2(102, 20, 92, 18), 9);
        _pollutionLabel.HorizontalAlignment = HorizontalAlignment.Left;
        _pollutionLabel.VerticalAlignment = VerticalAlignment.Center;

        _skillLabel = MakeLabel(new Rect2(102, 66, 142, 18), 9);
        _skillLabel.HorizontalAlignment = HorizontalAlignment.Left;
        _skillLabel.VerticalAlignment = VerticalAlignment.Center;

        Refresh();
    }

    public override void _Process(double delta)
    {
        Refresh();
    }

    private void Refresh()
    {
        if (_floorLabel == null)
            return;

        int floor = _runState?.FloorNumber ?? 1;
        _floorLabel.Text = $"楼层 {floor:00}";

        float health = _player?.CurrentHealth ?? 0.0f;
        float maxHealth = _player?.MaxHealth ?? 1.0f;
        _healthLabel.Text = $"HP  {Mathf.CeilToInt(health)} / {Mathf.CeilToInt(maxHealth)}";
        _healthMaterial?.SetShaderParameter(
            "ratio",
            maxHealth <= 0.0f ? 0.0f : health / maxHealth
        );

        float pollutionRatio = _runState?.PollutionRatio ?? 0.0f;
        _pollutionMaterial?.SetShaderParameter("ratio", pollutionRatio);
        _pollutionLabel.Text =
            $"污染 {Mathf.RoundToInt(pollutionRatio * 100.0f)}%";

        float chargeRatio = _skillCharge?.ChargeRatio ?? 0.0f;
        _skillMaterial?.SetShaderParameter("ratio", chargeRatio);
        _skillLabel.Text = _skillCharge?.IsReady == true
            ? "技能 就绪"
            : $"技能 {Mathf.RoundToInt(chargeRatio * 100.0f)}%";
    }

    private void AddTexture(string path)
    {
        TextureRect textureRect = new()
        {
            Texture = GD.Load<Texture2D>(path),
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.Keep,
            MouseFilter = MouseFilterEnum.Ignore,
            Position = Vector2.Zero,
            Size = new Vector2(276, 105),
            TextureFilter = TextureFilterEnum.Nearest
        };
        AddChild(textureRect);
    }

    private ShaderMaterial AddProgressTexture(
        string path,
        Color color,
        float fillStart,
        float fillEnd
    )
    {
        ShaderMaterial material = new()
        {
            Shader = GD.Load<Shader>(
                "res://shaders/ui/slanted_progress_fill.gdshader"
            )
        };
        material.SetShaderParameter("ratio", 0.0f);
        material.SetShaderParameter("fill_start", fillStart);
        material.SetShaderParameter("fill_end", fillEnd);
        material.SetShaderParameter("fill_color", color);

        TextureRect textureRect = new()
        {
            Texture = GD.Load<Texture2D>(path),
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.Keep,
            MouseFilter = MouseFilterEnum.Ignore,
            Position = Vector2.Zero,
            Size = new Vector2(276, 105),
            Material = material,
            TextureFilter = TextureFilterEnum.Nearest
        };
        AddChild(textureRect);
        return material;
    }

    private Label MakeLabel(Rect2 rect, int fontSize)
    {
        Label label = new()
        {
            Position = rect.Position,
            Size = rect.Size,
            MouseFilter = MouseFilterEnum.Ignore
        };
        UiPalette.StyleLabel(label, fontSize);
        AddChild(label);
        return label;
    }
}
