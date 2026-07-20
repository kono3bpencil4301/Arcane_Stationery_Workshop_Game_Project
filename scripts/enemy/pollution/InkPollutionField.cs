using Godot;
using System.Collections.Generic;

/// <summary>
/// 房间级墨迹污染管理器。保存被污染的地面格，并统一处理玩家减速、
/// 暴露计时与持续伤害。所有墨晕团共享同一个实例。
/// </summary>
public partial class InkPollutionField : Node2D
{
    private const float DamageInterval = 1.0f;

    [Export(PropertyHint.Range, "0,10,1")]
    public int EraseLevel { get; set; } = 1;

    [Export(PropertyHint.Range, "0.05,2,0.05")]
    public float EraseSuppressionDuration { get; set; } = 0.35f;

    private sealed class PollutedCellState
    {
        public Polygon2D Overlay;
        public float RemainingDuration;
    }

    private readonly Dictionary<Vector2I, PollutedCellState>
        _pollutedCells = new();
    private readonly List<Vector2I> _expiredCells = new();
    private readonly Dictionary<Vector2I, float> _eraseSuppressedCells = new();
    private readonly List<Vector2I> _expiredSuppressionCells = new();

    private TileMapLayer _groundLayer;
    private Player _player;
    private RunHudState _runHudState;
    private Color _pollutionColor = new(0.0f, 0.0f, 0.0f, 0.42f);
    private float _pollutionDuration = 5.0f;
    private float _playerSpeedMultiplier = 0.8f;
    private float _exposureGraceDuration = 3.0f;
    private float _damagePerSecond = 2.0f;
    private float _exposureDuration;
    private float _damageAccumulator;
    private bool _playerWasPolluted;

    public int PollutedCellCount => _pollutedCells.Count;

    public void Initialize(
        TileMapLayer groundLayer,
        Color pollutionColor,
        float pollutionDuration,
        float playerSpeedMultiplier,
        float exposureGraceDuration,
        float damagePerSecond
    )
    {
        _groundLayer = groundLayer;
        _pollutionColor = pollutionColor;
        _pollutionDuration = Mathf.Max(pollutionDuration, 0.0f);
        _playerSpeedMultiplier = Mathf.Clamp(
            playerSpeedMultiplier,
            0.0f,
            1.0f
        );
        _exposureGraceDuration = Mathf.Max(
            exposureGraceDuration,
            0.0f
        );
        _damagePerSecond = Mathf.Max(damagePerSecond, 0.0f);

        // 地面 < 污染遮罩 < 角色。
        if (GodotObject.IsInstanceValid(_groundLayer))
        {
            _groundLayer.ZIndex = Mathf.Min(_groundLayer.ZIndex, -10);
            ZIndex = _groundLayer.ZIndex + 1;
        }
    }

    public override void _Ready()
    {
        // 在 Player 之前更新，使本物理帧立即使用减速倍率。
        ProcessPhysicsPriority = -100;
        ResolvePlayer();
    }

    public override void _PhysicsProcess(double delta)
    {
        if (!GodotObject.IsInstanceValid(_groundLayer))
            return;

        UpdateEraseSuppression((float)delta);
        UpdatePollutedCells((float)delta);

        if (!GodotObject.IsInstanceValid(_player))
            ResolvePlayer();

        if (!GodotObject.IsInstanceValid(_player))
            return;

        Vector2I playerCell = WorldToCell(_player.GlobalPosition);
        bool isPolluted = _pollutedCells.ContainsKey(playerCell);

        _player.SetPollutionMovementMultiplier(
            isPolluted ? _playerSpeedMultiplier : 1.0f
        );

        if (!isPolluted)
        {
            ResetExposure();
            return;
        }

        float previousExposure = _exposureDuration;
        _exposureDuration += (float)delta;

        _runHudState?.SetPollution(
            Mathf.Min(_exposureDuration, _exposureGraceDuration),
            Mathf.Max(_exposureGraceDuration, 1.0f)
        );

        if (_exposureDuration < _exposureGraceDuration)
            return;

        // 跨过 3 秒阈值时立即结算第一跳，随后每秒结算一次。
        if (previousExposure < _exposureGraceDuration)
            _damageAccumulator = DamageInterval;
        else
            _damageAccumulator += (float)delta;

        while (_damageAccumulator >= DamageInterval)
        {
            _damageAccumulator -= DamageInterval;
            _player.TakeContinuousDamage(
                _damagePerSecond * DamageInterval,
                this
            );

            if (_player.IsDead)
                break;
        }

        _playerWasPolluted = true;
    }

    public void PolluteAt(Vector2 globalPosition, float radius)
    {
        if (!GodotObject.IsInstanceValid(_groundLayer))
            return;

        Vector2 localPosition = _groundLayer.ToLocal(globalPosition);
        Vector2I centerCell = _groundLayer.LocalToMap(localPosition);
        Vector2 tileSize = GetTileSize();
        float safeRadius = Mathf.Max(radius, 0.0f);
        int cellRadius = Mathf.CeilToInt(
            safeRadius / Mathf.Max(Mathf.Min(tileSize.X, tileSize.Y), 1.0f)
        ) + 1;

        for (int y = -cellRadius; y <= cellRadius; y++)
        {
            for (int x = -cellRadius; x <= cellRadius; x++)
            {
                Vector2I cell = centerCell + new Vector2I(x, y);

                if (_groundLayer.GetCellSourceId(cell) < 0)
                    continue;

                Vector2 cellCenter = _groundLayer.MapToLocal(cell);

                if (!CircleTouchesCell(
                    localPosition,
                    safeRadius,
                    cellCenter,
                    tileSize
                ))
                {
                    continue;
                }

                AddPollutedCell(cell, cellCenter, tileSize);
            }
        }
    }

    /// <summary>
    /// 查询指定世界坐标附近是否至少存在一个污染瓦片。
    /// 供依赖污染环境的敌人使用，不暴露污染字典本身。
    /// </summary>
    public bool HasPollutionNear(Vector2 globalPosition, float radius)
    {
        if (
            !GodotObject.IsInstanceValid(_groundLayer) ||
            _pollutedCells.Count == 0
        )
        {
            return false;
        }

        Vector2 localPosition = _groundLayer.ToLocal(globalPosition);
        Vector2 tileSize = GetTileSize();
        float expandedRadius = Mathf.Max(radius, 0.0f) +
            tileSize.Length() * 0.5f;
        float radiusSquared = expandedRadius * expandedRadius;

        foreach (Vector2I cell in _pollutedCells.Keys)
        {
            Vector2 cellCenter = _groundLayer.MapToLocal(cell);

            if (
                localPosition.DistanceSquaredTo(cellCenter) <=
                radiusSquared
            )
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// 擦除线段轨迹覆盖的污染格。只有擦除等级不低于污染等级时才会生效。
    /// </summary>
    public int EraseAlongSegment(
        Vector2 globalFrom,
        Vector2 globalTo,
        float radius,
        int eraserLevel
    )
    {
        if (
            eraserLevel < EraseLevel ||
            !GodotObject.IsInstanceValid(_groundLayer)
        )
        {
            return 0;
        }

        SuppressCellsAlongSegment(
            globalFrom,
            globalTo,
            Mathf.Max(radius, 0.0f)
        );

        if (_pollutedCells.Count == 0)
            return 0;

        Vector2 tileSize = GetTileSize();
        float expandedRadius = Mathf.Max(radius, 0.0f) +
            tileSize.Length() * 0.5f;
        float radiusSquared = expandedRadius * expandedRadius;

        _expiredCells.Clear();

        foreach (
            KeyValuePair<Vector2I, PollutedCellState> pair in _pollutedCells
        )
        {
            Vector2 cellCenter = _groundLayer.ToGlobal(
                _groundLayer.MapToLocal(pair.Key)
            );
            Vector2 closestPoint = ClosestPointOnSegment(
                cellCenter,
                globalFrom,
                globalTo
            );

            if (
                closestPoint.DistanceSquaredTo(cellCenter) <=
                radiusSquared
            )
            {
                _expiredCells.Add(pair.Key);
            }
        }

        foreach (Vector2I cell in _expiredCells)
        {
            PollutedCellState state = _pollutedCells[cell];
            if (GodotObject.IsInstanceValid(state.Overlay))
                state.Overlay.QueueFree();

            _pollutedCells.Remove(cell);
        }

        if (
            _expiredCells.Count > 0 &&
            GodotObject.IsInstanceValid(_player) &&
            !_pollutedCells.ContainsKey(
                WorldToCell(_player.GlobalPosition)
            )
        )
        {
            _player.SetPollutionMovementMultiplier(1.0f);
            ResetExposure();
        }

        return _expiredCells.Count;
    }

    private void SuppressCellsAlongSegment(
        Vector2 globalFrom,
        Vector2 globalTo,
        float radius
    )
    {
        Vector2 tileSize = GetTileSize();
        float sampleSpacing = Mathf.Max(
            Mathf.Min(tileSize.X, tileSize.Y) * 0.5f,
            1.0f
        );
        float distance = globalFrom.DistanceTo(globalTo);
        int sampleCount = Mathf.Max(
            1,
            Mathf.CeilToInt(distance / sampleSpacing)
        );
        int cellRadius = Mathf.CeilToInt(
            radius / Mathf.Max(Mathf.Min(tileSize.X, tileSize.Y), 1.0f)
        ) + 1;

        for (int sampleIndex = 0; sampleIndex <= sampleCount; sampleIndex++)
        {
            float ratio = (float)sampleIndex / sampleCount;
            Vector2 localPosition = _groundLayer.ToLocal(
                globalFrom.Lerp(globalTo, ratio)
            );
            Vector2I centerCell = _groundLayer.LocalToMap(localPosition);

            for (int y = -cellRadius; y <= cellRadius; y++)
            {
                for (int x = -cellRadius; x <= cellRadius; x++)
                {
                    Vector2I cell = centerCell + new Vector2I(x, y);
                    if (_groundLayer.GetCellSourceId(cell) < 0)
                        continue;

                    Vector2 cellCenter = _groundLayer.MapToLocal(cell);
                    if (!CircleTouchesCell(
                        localPosition,
                        radius,
                        cellCenter,
                        tileSize
                    ))
                    {
                        continue;
                    }

                    _eraseSuppressedCells[cell] = Mathf.Max(
                        EraseSuppressionDuration,
                        0.05f
                    );
                }
            }
        }
    }

    private void UpdateEraseSuppression(float delta)
    {
        if (_eraseSuppressedCells.Count == 0)
            return;

        _expiredSuppressionCells.Clear();
        foreach (Vector2I cell in _eraseSuppressedCells.Keys)
            _expiredSuppressionCells.Add(cell);

        foreach (Vector2I cell in _expiredSuppressionCells)
        {
            float remaining = _eraseSuppressedCells[cell] - delta;
            if (remaining <= 0.0f)
                _eraseSuppressedCells.Remove(cell);
            else
                _eraseSuppressedCells[cell] = remaining;
        }
    }

    private void AddPollutedCell(
        Vector2I cell,
        Vector2 localCellCenter,
        Vector2 tileSize
    )
    {
        if (_eraseSuppressedCells.ContainsKey(cell))
            return;

        if (_pollutedCells.TryGetValue(cell, out PollutedCellState state))
        {
            state.RemainingDuration = GetInitialCellDuration();

            if (GodotObject.IsInstanceValid(state.Overlay))
                state.Overlay.Color = _pollutionColor;

            return;
        }

        Vector2 halfSize = tileSize * 0.5f;
        Vector2 inset = Vector2.One * 0.75f;
        Vector2 visualHalfSize = new(
            Mathf.Max(halfSize.X - inset.X, 1.0f),
            Mathf.Max(halfSize.Y - inset.Y, 1.0f)
        );

        Polygon2D overlay = new()
        {
            Name = $"Pollution_{cell.X}_{cell.Y}",
            Position = ToLocal(
                _groundLayer.ToGlobal(localCellCenter)
            ),
            Polygon = new Vector2[]
            {
                new(-visualHalfSize.X, -visualHalfSize.Y),
                new(visualHalfSize.X, -visualHalfSize.Y),
                new(visualHalfSize.X, visualHalfSize.Y),
                new(-visualHalfSize.X, visualHalfSize.Y)
            },
            Color = _pollutionColor
        };

        AddChild(overlay);
        _pollutedCells.Add(
            cell,
            new PollutedCellState
            {
                Overlay = overlay,
                RemainingDuration = GetInitialCellDuration()
            }
        );
    }

    private float GetInitialCellDuration()
    {
        // 0 表示永久污染，使用负数作为内部永久标记。
        return _pollutionDuration <= 0.0f
            ? -1.0f
            : _pollutionDuration;
    }

    private void UpdatePollutedCells(float delta)
    {
        if (_pollutionDuration <= 0.0f || _pollutedCells.Count == 0)
            return;

        _expiredCells.Clear();

        foreach (
            KeyValuePair<Vector2I, PollutedCellState> pair in _pollutedCells
        )
        {
            PollutedCellState state = pair.Value;

            if (state.RemainingDuration < 0.0f)
                continue;

            state.RemainingDuration = Mathf.Max(
                state.RemainingDuration - delta,
                0.0f
            );

            if (state.RemainingDuration <= 0.0f)
            {
                if (GodotObject.IsInstanceValid(state.Overlay))
                    state.Overlay.QueueFree();

                _expiredCells.Add(pair.Key);
                continue;
            }

            if (GodotObject.IsInstanceValid(state.Overlay))
            {
                Color fadedColor = _pollutionColor;
                fadedColor.A *= Mathf.Clamp(
                    state.RemainingDuration / _pollutionDuration,
                    0.0f,
                    1.0f
                );
                state.Overlay.Color = fadedColor;
            }
        }

        foreach (Vector2I cell in _expiredCells)
            _pollutedCells.Remove(cell);
    }

    private Vector2I WorldToCell(Vector2 globalPosition)
    {
        return _groundLayer.LocalToMap(
            _groundLayer.ToLocal(globalPosition)
        );
    }

    private Vector2 GetTileSize()
    {
        if (_groundLayer?.TileSet == null)
            return new Vector2(48.0f, 48.0f);

        Vector2I size = _groundLayer.TileSet.TileSize;
        return new Vector2(
            Mathf.Max(size.X, 1),
            Mathf.Max(size.Y, 1)
        );
    }

    private static bool CircleTouchesCell(
        Vector2 circleCenter,
        float radius,
        Vector2 cellCenter,
        Vector2 cellSize
    )
    {
        Vector2 centerDelta = circleCenter - cellCenter;
        Vector2 distanceFromCenter = new(
            Mathf.Abs(centerDelta.X),
            Mathf.Abs(centerDelta.Y)
        );
        Vector2 halfSize = cellSize * 0.5f;
        Vector2 outsideDistance = new(
            Mathf.Max(distanceFromCenter.X - halfSize.X, 0.0f),
            Mathf.Max(distanceFromCenter.Y - halfSize.Y, 0.0f)
        );

        return outsideDistance.LengthSquared() <= radius * radius;
    }

    private static Vector2 ClosestPointOnSegment(
        Vector2 point,
        Vector2 from,
        Vector2 to
    )
    {
        Vector2 segment = to - from;
        float lengthSquared = segment.LengthSquared();
        if (lengthSquared <= 0.0001f)
            return from;

        float ratio = Mathf.Clamp(
            (point - from).Dot(segment) / lengthSquared,
            0.0f,
            1.0f
        );
        return from + segment * ratio;
    }

    private void ResolvePlayer()
    {
        _player = GetTree().GetFirstNodeInGroup("player") as Player;
        _runHudState = _player?
            .GetNodeOrNull<RunHudState>("RunHudState");
    }

    private void ResetExposure()
    {
        if (
            _exposureDuration <= 0.0f &&
            _damageAccumulator <= 0.0f &&
            !_playerWasPolluted
        )
        {
            return;
        }

        _exposureDuration = 0.0f;
        _damageAccumulator = 0.0f;
        _playerWasPolluted = false;
        _runHudState?.SetPollution(0.0f, _exposureGraceDuration);
    }

    public override void _ExitTree()
    {
        if (GodotObject.IsInstanceValid(_player))
            _player.SetPollutionMovementMultiplier(1.0f);
    }
}
