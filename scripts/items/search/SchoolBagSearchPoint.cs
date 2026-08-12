using Godot;
using scripts.items.search;
using System;
using System.Collections.Generic;

/// <summary>
/// 战斗房清理后出现的书包搜索点。
/// 玩家靠近并点击后，经过一段搜索时间，再将物品放入背包。
/// </summary>
public partial class SchoolBagSearchPoint : SearchPointData
{
    private readonly RandomNumberGenerator _random = new();

    private Sprite2D _sprite;
    private Label _prompt;

    private TextureProgressBar _searchProgressRing;

    private Timer _searchTimer;
    private AudioStreamPlayer _searchingSFXPlayer;

    // 当前位于书包附近的玩家
    private Player _nearbyPlayer;

    // 真正执行本次搜索的玩家
    private Player _searchingPlayer;

    private SearchState _state = SearchState.Unsearched;

    // 当前搜索实际使用的时间
    private double _activeSearchDuration;

    private float _moveSpeedBeforeSearch;
    private bool _moveSpeedReduced;

    [ExportCategory("Ink Coin Rewards")]

    [Export(PropertyHint.Range, "0,999,1")]
    public int EarlyMinimumInkCoins { get; set; } = 3;

    [Export(PropertyHint.Range, "0,999,1")]
    public int EarlyMaximumInkCoins { get; set; } = 6;

    [Export(PropertyHint.Range, "0,999,1")]
    public int MiddleMinimumInkCoins { get; set; } = 6;

    [Export(PropertyHint.Range, "0,999,1")]
    public int MiddleMaximumInkCoins { get; set; } = 10;

    [Export(PropertyHint.Range, "0,999,1")]
    public int LateMinimumInkCoins { get; set; } = 10;

    [Export(PropertyHint.Range, "0,999,1")]
    public int LateMaximumInkCoins { get; set; } = 15;

    public int RewardRoomId { get; private set; } = -1;
    public DungeonEncounterDifficulty RewardDifficulty { get; private set; } =
        DungeonEncounterDifficulty.Early;

    public void ConfigureRoomReward(
        int roomId,
        DungeonEncounterDifficulty difficulty
    )
    {
        RewardRoomId = roomId;
        RewardDifficulty = difficulty;
    }

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

        SetupPrompt();
        CreateProgressRing();
        SetupSearchTimer();
        SetupSearchAudio();

        BodyEntered += OnBodyEntered;
        BodyExited += OnBodyExited;

        UpdateState(SearchState.Unsearched);
    }

    private void SetupPrompt()
    {
        if(_prompt == null)
            return;

        _prompt.Visible = false;

        _prompt.AddThemeFontOverride(
            "font",
            UiPalette.PixelFont
        );

        _prompt.AddThemeColorOverride(
            "font_color",
            Colors.White
        );

        _prompt.AddThemeColorOverride(
            "font_outline_color",
            Colors.Black
        );

        _prompt.AddThemeConstantOverride(
            "outline_size",
            3
        );
    }

    private void SetupSearchTimer()
    {
        // 优先寻找场景中的 SearchTimer 节点
        _searchTimer = GetNodeOrNull<Timer>("SearchTimer");

        // 如果场景中没有，就在代码里创建
        if(_searchTimer == null)
        {
            _searchTimer = new Timer
            {
                Name = "SearchTimer",
                OneShot = true
            };

            AddChild(_searchTimer);
        }

        _searchTimer.OneShot = true;
        _searchTimer.Timeout += OnSearchTimerTimeout;
    }

    private void SetupSearchAudio()
    {
        _searchingSFXPlayer = new AudioStreamPlayer
        {
            Name = "SearchingSFXPlayer",
            Bus = "SFX",
            Stream = SearchingSFX
        };
        AddChild(_searchingSFXPlayer);
        _searchingSFXPlayer.Finished += OnSearchingSFXFinished;
    }

    public override void _Process(double delta)
    {
        if(_state != SearchState.Searching)
            return;

        UpdateSearchProgress();
    }

    public override void _PhysicsProcess(double delta)
    {
        // 搜索期间或者已经搜索完成时，不再检测新玩家
        if(
            _state != SearchState.Unsearched ||
            _nearbyPlayer != null
        )
        {
            return;
        }

        foreach(Node2D body in GetOverlappingBodies())
        {
            if(body is not Player player)
                continue;

            SetNearbyPlayer(player);
            break;
        }
    }

    public override void _UnhandledInput(
        InputEvent inputEvent
    )
    {
        // Searching 状态下也禁止再次点击
        if(
            _state != SearchState.Unsearched ||
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

        StartSearch(_nearbyPlayer);

        GetViewport().SetInputAsHandled();
    }

    /// <summary>
    /// 点击书包后开始搜索。
    /// 此处不发放物品，只启动倒计时。
    /// </summary>
    public bool StartSearch(Player player)
    {
        if(
            _state != SearchState.Unsearched ||
            !GodotObject.IsInstanceValid(player)
        )
        {
            return false;
        }

        InventoryModel inventory =
            player.GetNodeOrNull<InventoryModel>("Inventory");

        if(inventory == null)
        {
            GD.PushWarning(
                "玩家节点下没有找到 InventoryModel。"
            );

            return false;
        }

        _searchingPlayer = player;
        _moveSpeedBeforeSearch = player.MoveSpeed;
        player.MoveSpeed *= 0.35f;
        _moveSpeedReduced = true;

        // 防止 SearchTime 为 0 或负数
        _activeSearchDuration = Math.Max(
            SearchTime,
            0.1
        );

        UpdateState(SearchState.Searching);

        StartSearchingSFX();

        _searchTimer.Start(_activeSearchDuration);

        return true;
    }

    /// <summary>
    /// 每帧刷新搜索倒计时和进度。
    /// </summary>
    private void UpdateSearchProgress()
    {
        if(
            _searchTimer == null ||
            _searchTimer.IsStopped()
        )
        {
            return;
        }

        double remainingTime =
            Math.Max(_searchTimer.TimeLeft, 0.0);

        double progress =
            1.0 -
            remainingTime / _activeSearchDuration;

        progress = Math.Clamp(
            progress,
            0.0,
            1.0
        );

        int remainingSeconds =
            (int)Math.Ceiling(remainingTime);

        int progressPercent =
            (int)Math.Round(progress * 100.0);

        if(_prompt != null)
        {
            _prompt.Visible = true;
            _prompt.Text =
                $"搜索中……{remainingSeconds}秒  {progressPercent}%";
        }

        if(_searchProgressRing != null)
        {
            _searchProgressRing.Visible = true;
            _searchProgressRing.Value =
                progress * 100.0;
        }
    }

    /// <summary>
    /// Timer 倒计时结束。
    /// </summary>
    private void OnSearchTimerTimeout()
    {
        if(_state != SearchState.Searching)
            return;

        if(!GodotObject.IsInstanceValid(_searchingPlayer))
        {
            CancelSearch("搜索失败");
            return;
        }

        CompleteSearch(_searchingPlayer);
    }

    /// <summary>
    /// 搜索结束后，真正生成并发放物品。
    /// </summary>
    private bool CompleteSearch(Player player)
    {
        InventoryModel inventory =
            player.GetNodeOrNull<InventoryModel>("Inventory");

        if(inventory == null)
        {
            CancelSearch("无法找到玩家背包");
            return false;
        }

        List<string> acquiredNames = new();
        int acquiredCount = 0;

        // 发放创口贴
        if(
            BandageItem != null &&
            inventory.TryAddItem(BandageItem, 1)
        )
        {
            acquiredNames.Add(
                BandageItem.DisplayName
            );

            acquiredCount++;
        }

        // 发放随机材料
        if(MaterialItems.Count > 0)
        {
            int minimumRolls = Mathf.Max(
                MinimumMaterialRolls,
                1
            );

            int maximumRolls = Mathf.Max(
                MaximumMaterialRolls,
                minimumRolls
            );

            int rollCount = _random.RandiRange(
                minimumRolls,
                maximumRolls
            );

            for(
                int roll = 0;
                roll < rollCount;
                roll++
            )
            {
                InventoryItemData material =
                    MaterialItems[
                        _random.RandiRange(
                            0,
                            MaterialItems.Count - 1
                        )
                    ];

                if(material == null)
                    continue;

                int quantity = _random.RandiRange(
                    1,
                    Mathf.Max(
                        MaximumMaterialQuantity,
                        1
                    )
                );

                if(
                    !inventory.TryAddItem(
                        material,
                        quantity
                    )
                )
                {
                    continue;
                }

                acquiredNames.Add(
                    $"{material.DisplayName}×{quantity}"
                );

                acquiredCount += quantity;
            }
        }

        int acquiredCoins = AwardRoomInkCoins(player);

        if (acquiredCoins > 0)
            acquiredNames.Add($"灵墨币×{acquiredCoins}");

        // 一件物品都没有放进去
        if(acquiredCount <= 0 && acquiredCoins <= 0)
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

            // 允许玩家整理背包后重新搜索
            ResetSearch();

            return false;
        }

        Searched = true;

        UpdateState(SearchState.Searched);

        if(
            _sprite != null &&
            OpenedTexture != null
        )
        {
            _sprite.Texture = OpenedTexture;
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

        EmitSignal(
            SignalName.SearchCompleted,
            acquiredCount
        );

        StopSearchingSFX();
        PlaySFX(SearchCompleteSFX);

        RestoreSearchMovementSpeed();
        _searchingPlayer = null;

        return true;
    }

    private int AwardRoomInkCoins(Player player)
    {
        InkCoinWallet wallet = player?
            .GetNodeOrNull<InkCoinWallet>("InkCoinWallet");

        if (wallet == null)
        {
            GD.PushWarning("搜索书包时无法找到玩家的灵墨币钱包。");
            return 0;
        }

        Vector2I rewardRange = GetInkCoinRewardRange();
        int reward = _random.RandiRange(rewardRange.X, rewardRange.Y);
        int added = wallet.AddCoins(reward);

        GD.Print(
            $"[DungeonReward] 房间 {RewardRoomId} " +
            $"({RewardDifficulty}) 书包提供灵墨币 {added}。"
        );
        return added;
    }

    public Vector2I GetInkCoinRewardRange()
    {
        int minimum;
        int maximum;

        switch (RewardDifficulty)
        {
            case DungeonEncounterDifficulty.Middle:
                minimum = MiddleMinimumInkCoins;
                maximum = MiddleMaximumInkCoins;
                break;
            case DungeonEncounterDifficulty.Late:
                minimum = LateMinimumInkCoins;
                maximum = LateMaximumInkCoins;
                break;
            default:
                minimum = EarlyMinimumInkCoins;
                maximum = EarlyMaximumInkCoins;
                break;
        }

        minimum = Mathf.Max(minimum, 0);
        maximum = Mathf.Max(maximum, minimum);
        return new Vector2I(minimum, maximum);
    }

    public void UpdateState(SearchState state)
    {
        _state = state;

        switch(_state)
        {
            case SearchState.Unsearched:
                if(_searchProgressRing != null)
                {
                    _searchProgressRing.Value = 0;
                    _searchProgressRing.Visible = false;
                }

                if(_prompt != null)
                {
                    bool playerNearby =
                        GodotObject.IsInstanceValid(
                            _nearbyPlayer
                        );

                    _prompt.Text =
                        "左键搜索书包";

                    _prompt.Visible =
                        playerNearby;
                }

                break;

            case SearchState.Searching:
                if(_prompt != null)
                {
                    _prompt.Text = "搜索中……";
                    _prompt.Visible = true;
                }

                if(_searchProgressRing != null)
                {
                    _searchProgressRing.Value = 0;
                    _searchProgressRing.Visible = true;
                }

                break;

            case SearchState.Searched:
                if(_prompt != null)
                {
                    _prompt.Text = "已搜索";
                    _prompt.Visible = true;
                }

                if(_searchProgressRing != null)
                {
                    _searchProgressRing.Value = 100;
                    _searchProgressRing.Visible = false;
                }

                break;
        }
    }

    private void CancelSearch(string message)
    {
        _searchTimer?.Stop();

        FloatingDamageNumber.SpawnMessage(
            this,
            message,
            UiPalette.WarningCoral,
            new Vector2(0.0f, -44.0f),
            18.0f,
            0.9f,
            13
        );

        ResetSearch();
    }

    private void ResetSearch()
    {
        StopSearchingSFX();
        RestoreSearchMovementSpeed();
        _searchingPlayer = null;
        Searched = false;
        UpdateState(SearchState.Unsearched);
    }

    private void StartSearchingSFX()
    {
        if (_searchingSFXPlayer == null || SearchingSFX == null)
            return;

        _searchingSFXPlayer.Stream = SearchingSFX;
        _searchingSFXPlayer.Play();
    }

    private void StopSearchingSFX()
    {
        _searchingSFXPlayer?.Stop();
    }

    private void OnSearchingSFXFinished()
    {
        if (_state == SearchState.Searching)
            _searchingSFXPlayer?.Play();
    }

    private void PlaySFX(AudioStream stream)
    {
        if (stream == null)
            return;

        GetNodeOrNull<AudioManager>("/root/AudioManager")?.PlaySFX(stream);
    }

    private void CreateProgressRing()
    {
        if(RingBackTexture == null || RingFillTexture == null)
        {
            GD.PushWarning("SchoolBagSearchPoint 缺少环形进度纹理。");
            return;
        }

        Vector2 size = RingBackTexture.GetSize();
        _searchProgressRing = new TextureProgressBar
        {
            Name = "SearchProgressRing",
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
        AddChild(_searchProgressRing);
    }

    private void RestoreSearchMovementSpeed()
    {
        if(
            !_moveSpeedReduced ||
            !GodotObject.IsInstanceValid(_searchingPlayer)
        )
        {
            _moveSpeedReduced = false;
            return;
        }

        _searchingPlayer.MoveSpeed = _moveSpeedBeforeSearch;
        _moveSpeedReduced = false;
    }

    private bool IsMouseOverBag()
    {
        if(_sprite?.Texture == null)
            return false;

        Vector2 localMouse =
            ToLocal(GetGlobalMousePosition()) -
            _sprite.Position;

        Vector2 scale = new(
            Mathf.Abs(_sprite.Scale.X),
            Mathf.Abs(_sprite.Scale.Y)
        );

        Vector2 halfSize =
            _sprite.Texture.GetSize() *
            scale *
            0.5f;

        return
            Mathf.Abs(localMouse.X) <= halfSize.X &&
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

        // 搜索已经开始时，继续显示搜索进度
        if(
            _prompt != null &&
            _state == SearchState.Unsearched
        )
        {
            _prompt.Visible = false;
        }
    }

    private void SetNearbyPlayer(Player player)
    {
        _nearbyPlayer = player;

        if(
            _prompt != null &&
            _state == SearchState.Unsearched
        )
        {
            _prompt.Text = "左键搜索书包";
            _prompt.Visible = true;
        }
    }

    public override void _ExitTree()
    {
        RestoreSearchMovementSpeed();

        BodyEntered -= OnBodyEntered;
        BodyExited -= OnBodyExited;

        if(_searchTimer != null)
            _searchTimer.Timeout -= OnSearchTimerTimeout;

        if(_searchingSFXPlayer != null)
        {
            _searchingSFXPlayer.Stop();
            _searchingSFXPlayer.Finished -= OnSearchingSFXFinished;
        }
    }
}
