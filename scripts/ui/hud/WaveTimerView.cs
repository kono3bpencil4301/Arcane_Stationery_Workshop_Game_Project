using Godot;

public partial class WaveTimerView : PanelContainer
{
    private EnemySpawner _spawner;
    private Label _timeLabel;

    public void Bind(EnemySpawner spawner)
    {
        _spawner = spawner;
    }

    public override void _Ready()
    {
        CustomMinimumSize = new Vector2(112, 34);
        MouseFilter = MouseFilterEnum.Ignore;
        AddThemeStyleboxOverride(
            "panel",
            UiPalette.MakePanel(
                new Color(UiPalette.Panel, 0.88f),
                UiPalette.WarningCoral
            )
        );

        _timeLabel = new Label
        {
            Text = "00:00",
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            MouseFilter = MouseFilterEnum.Ignore
        };
        UiPalette.StyleLabel(_timeLabel, 20);
        AddChild(_timeLabel);
    }

    public override void _Process(double delta)
    {
        if (_timeLabel == null)
            return;

        if (
            _spawner?.Timeline == null ||
            !_spawner.EncounterActive
        )
        {
            _timeLabel.Text = "--:--";
            return;
        }

        int totalSeconds = Mathf.Max(
            0,
            Mathf.CeilToInt(_spawner.RemainingSeconds)
        );
        int minutes = totalSeconds / 60;
        int seconds = totalSeconds % 60;
        _timeLabel.Text = $"{minutes:00}:{seconds:00}";
    }
}
