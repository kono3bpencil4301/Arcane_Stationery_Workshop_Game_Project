using Godot;
using System;
using System.Collections.Generic;

/// <summary>
/// 从当前 TileMapLayer 已绘制的格子中选择位置，并按照怪潮时间轴持续生成敌人。
/// </summary>
[GlobalClass]
public partial class EnemySpawner : TileMapLayer
{
    [Signal]
    public delegate void WaveChangedEventHandler(
        string eventName,
        EnemyWaveType eventType,
        float progressPercent
    );

    [Signal]
    public delegate void EnemySpawnedEventHandler(
        Node2D enemy,
        string eventName
    );

    [ExportCategory("Wave Timeline")]

    [Export]
    public EnemyWaveTimelineData Timeline { get; set; }

    [Export]
    public bool AutoStart { get; set; } = true;

    [ExportCategory("Spawn Target")]

    [Export]
    public NodePath EnemyParentPath { get; set; } = new("..");

    [Export]
    public bool HideSpawnMarkersAtRuntime { get; set; } = true;

    [Export(PropertyHint.Range, "1,128,1")]
    public int MaxCatchUpSpawnsPerFrame { get; set; } = 8;

    public bool IsRunning { get; private set; }

    public float CurrentProgressPercent { get; private set; }

    public double ElapsedSeconds => _elapsedSeconds;

    public double CycleDurationSeconds =>
        Timeline == null
            ? 0.0
            : Math.Max(Timeline.CycleDurationSeconds, 1.0f);

    public EnemyWaveEvent CurrentEvent { get; private set; }

    private readonly List<EnemyWaveEvent> _orderedEvents = new();
    private readonly List<Vector2I> _spawnCells = new();
    private readonly RandomNumberGenerator _random = new();

    private Node _enemyParent;
    private double _elapsedSeconds;
    private double _spawnCountdown;
    private int _currentEventIndex = -1;

    public override void _Ready()
    {
        if(HideSpawnMarkersAtRuntime)
            Visible = false;

        _random.Randomize();
        CacheSpawnCells();
        ResolveEnemyParent();
        RebuildTimeline();

        if(AutoStart)
            StartSpawning();
    }

    public override void _Process(double delta)
    {
        if(!IsRunning || CurrentEvent?.Wave == null)
            return;

        AdvanceTimeline(delta);

        if(!IsRunning || CurrentEvent?.Wave == null)
            return;

        _spawnCountdown -= delta;
        int spawnedThisFrame = 0;
        int spawnLimit = Math.Max(MaxCatchUpSpawnsPerFrame, 1);

        while(_spawnCountdown <= 0.0 && spawnedThisFrame < spawnLimit)
        {
            SpawnOneEnemy();
            _spawnCountdown += Math.Max(CurrentEvent.Wave.SpawnInterval, 0.05f);
            spawnedThisFrame++;
        }

        // 防止暂停或卡顿后在单帧中无限追赶积压的生成次数。
        if(spawnedThisFrame >= spawnLimit && _spawnCountdown <= 0.0)
            _spawnCountdown = Math.Max(CurrentEvent.Wave.SpawnInterval, 0.05f);
    }

    public void StartSpawning()
    {
        if(_spawnCells.Count == 0)
        {
            GD.PushWarning($"{Name} 没有已绘制的生成格子，敌人生成器未启动。");
            IsRunning = false;
            return;
        }

        if(_orderedEvents.Count == 0)
        {
            GD.PushWarning($"{Name} 没有有效的怪潮事件，敌人生成器未启动。");
            IsRunning = false;
            return;
        }

        if(!GodotObject.IsInstanceValid(_enemyParent))
            ResolveEnemyParent();

        if(!GodotObject.IsInstanceValid(_enemyParent))
        {
            GD.PushWarning($"{Name} 找不到敌人父节点: {EnemyParentPath}");
            IsRunning = false;
            return;
        }

        _elapsedSeconds = 0.0;
        CurrentProgressPercent = 0.0f;
        _currentEventIndex = -1;
        CurrentEvent = null;
        IsRunning = true;
        ActivateEventForProgress(CurrentProgressPercent, true);
    }

    public void StopSpawning()
    {
        IsRunning = false;
    }

    public void RestartTimeline()
    {
        RebuildTimeline();
        StartSpawning();
    }

    public Node2D SpawnOneEnemy()
    {
        EnemyWaveData wave = CurrentEvent?.Wave;
        PackedScene enemyScene = ChooseWeightedEnemyScene(wave);

        if(enemyScene == null || _spawnCells.Count == 0)
            return null;

        Node instance = enemyScene.Instantiate();

        if(instance is not Node2D enemy)
        {
            GD.PushWarning($"敌人场景 {enemyScene.ResourcePath} 的根节点不是 Node2D。");
            instance.Free();
            return null;
        }

        Vector2I cell = _spawnCells[_random.RandiRange(0, _spawnCells.Count - 1)];
        Vector2 spawnPosition = ToGlobal(MapToLocal(cell));

        _enemyParent.AddChild(enemy);
        enemy.GlobalPosition = spawnPosition;

        EmitSignal(SignalName.EnemySpawned, enemy, wave.EventName);
        return enemy;
    }

    private void AdvanceTimeline(double delta)
    {
        double duration = Math.Max(Timeline.CycleDurationSeconds, 1.0f);
        _elapsedSeconds += delta;

        if(Timeline.LoopTimeline)
        {
            bool wrapped = _elapsedSeconds >= duration;
            _elapsedSeconds %= duration;
            CurrentProgressPercent = (float)(_elapsedSeconds / duration * 100.0);

            if(wrapped)
            {
                _currentEventIndex = -1;
                CurrentEvent = null;
                ActivateEventForProgress(CurrentProgressPercent, true);
                return;
            }
        }
        else
        {
            _elapsedSeconds = Math.Min(_elapsedSeconds, duration);
            CurrentProgressPercent = (float)(_elapsedSeconds / duration * 100.0);
        }

        ActivateEventForProgress(CurrentProgressPercent, false);
    }

    private void ActivateEventForProgress(float progressPercent, bool force)
    {
        int matchingIndex = -1;

        for(int index = 0; index < _orderedEvents.Count; index++)
        {
            if(_orderedEvents[index].ProgressPercent > progressPercent)
                break;

            matchingIndex = index;
        }

        if(matchingIndex < 0)
        {
            // 即使首个配置略大于 0%，也先采用它，避免时间轴开头完全停刷。
            matchingIndex = 0;
        }

        if(!force && matchingIndex == _currentEventIndex)
            return;

        _currentEventIndex = matchingIndex;
        CurrentEvent = _orderedEvents[matchingIndex];
        _spawnCountdown = 0.0;

        EmitSignal(
            SignalName.WaveChanged,
            CurrentEvent.Wave.EventName,
            (int)CurrentEvent.EventType,
            CurrentEvent.ProgressPercent
        );

        GD.Print(
            $"[{Name}] 激活怪潮: {CurrentEvent.Wave.EventName} " +
            $"({CurrentEvent.EventType}, {CurrentEvent.ProgressPercent:0.#}%)"
        );
    }

    private PackedScene ChooseWeightedEnemyScene(EnemyWaveData wave)
    {
        if(wave?.EnemyQueue == null || wave.EnemyQueue.Count == 0)
        {
            GD.PushWarning($"怪潮 {wave?.EventName ?? "<null>"} 没有敌人队列。");
            return null;
        }

        float totalWeight = 0.0f;

        foreach(EnemySpawnEntry entry in wave.EnemyQueue)
        {
            if(entry?.EnemyScene != null && entry.Weight > 0.0f)
                totalWeight += entry.Weight;
        }

        if(totalWeight <= 0.0f)
        {
            GD.PushWarning($"怪潮 {wave.EventName} 没有权重大于 0 的有效敌人。");
            return null;
        }

        float roll = _random.RandfRange(0.0f, totalWeight);

        foreach(EnemySpawnEntry entry in wave.EnemyQueue)
        {
            if(entry?.EnemyScene == null || entry.Weight <= 0.0f)
                continue;

            roll -= entry.Weight;

            if(roll <= 0.0f)
                return entry.EnemyScene;
        }

        return null;
    }

    private void CacheSpawnCells()
    {
        _spawnCells.Clear();

        foreach(Vector2I cell in GetUsedCells())
            _spawnCells.Add(cell);
    }

    private void ResolveEnemyParent()
    {
        _enemyParent = EnemyParentPath.IsEmpty
            ? GetParent()
            : GetNodeOrNull(EnemyParentPath);
    }

    private void RebuildTimeline()
    {
        _orderedEvents.Clear();

        if(Timeline?.WaveEvents == null)
            return;

        foreach(EnemyWaveEvent waveEvent in Timeline.WaveEvents)
        {
            if(waveEvent?.Wave == null)
                continue;

            waveEvent.ProgressPercent = Mathf.Clamp(waveEvent.ProgressPercent, 0.0f, 100.0f);
            _orderedEvents.Add(waveEvent);
        }

        _orderedEvents.Sort(
            (left, right) => left.ProgressPercent.CompareTo(right.ProgressPercent)
        );
    }
}
