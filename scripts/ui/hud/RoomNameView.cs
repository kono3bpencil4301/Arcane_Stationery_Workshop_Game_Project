using Godot;

/// <summary>
/// 显示玩家当前所在房间；玩家进入走廊后自动隐藏。
/// </summary>
public partial class RoomNameView : Label
{
    private DungeonGenerator _dungeonGenerator;
    private int _visibleRoomId = -1;

    public void Bind(DungeonGenerator dungeonGenerator)
    {
        if(GodotObject.IsInstanceValid(_dungeonGenerator))
        {
            _dungeonGenerator.PlayerEnteredRoom -= OnPlayerEnteredRoom;
            _dungeonGenerator.PlayerExitedRoom -= OnPlayerExitedRoom;
        }

        _dungeonGenerator = dungeonGenerator;

        if(!GodotObject.IsInstanceValid(_dungeonGenerator))
            return;

        _dungeonGenerator.PlayerEnteredRoom += OnPlayerEnteredRoom;
        _dungeonGenerator.PlayerExitedRoom += OnPlayerExitedRoom;
    }

    public override void _Ready()
    {
        Text = string.Empty;
        Visible = false;
        MouseFilter = MouseFilterEnum.Ignore;
        HorizontalAlignment = HorizontalAlignment.Center;
        VerticalAlignment = VerticalAlignment.Center;
        CustomMinimumSize = new Vector2(240, 24);
        UiPalette.StyleLabel(this, 15);
    }

    private void OnPlayerEnteredRoom(
        int roomId,
        string roomName,
        int roomType
    )
    {
        _visibleRoomId = roomId;
        Text = roomName;
        Visible = true;
        AddThemeColorOverride(
            "font_color",
            GetRoomColor((DungeonRoomType)roomType)
        );
    }

    private void OnPlayerExitedRoom(int roomId)
    {
        if(_visibleRoomId != roomId)
            return;

        _visibleRoomId = -1;
        Text = string.Empty;
        Visible = false;
    }

    private static Color GetRoomColor(DungeonRoomType roomType)
    {
        return roomType switch
        {
            DungeonRoomType.Monster =>
                UiPalette.ArcaneViolet.Lightened(0.35f),
            DungeonRoomType.Shop =>
                UiPalette.ChargeGold.Lightened(0.12f),
            DungeonRoomType.Start => Colors.White,
            DungeonRoomType.Extraction => Colors.White,
            _ => Colors.White
        };
    }

    public override void _ExitTree()
    {
        if(!GodotObject.IsInstanceValid(_dungeonGenerator))
            return;

        _dungeonGenerator.PlayerEnteredRoom -= OnPlayerEnteredRoom;
        _dungeonGenerator.PlayerExitedRoom -= OnPlayerExitedRoom;
    }
}
