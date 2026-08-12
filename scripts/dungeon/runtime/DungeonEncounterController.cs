using Godot;
using System.Collections.Generic;

public partial class DungeonEncounterController : Node
{
    [Signal]
    public delegate void EncounterStartedEventHandler(int roomId);

    [Signal]
    public delegate void EncounterClearedEventHandler(int roomId);

    [Signal]
    public delegate void DifficultyStateChangedEventHandler(
        int difficulty,
        float durationSeconds
    );

    public DungeonRoomData ActiveRoom { get; private set; }
    public DungeonEncounterDifficultyState CurrentDifficultyState =>
        _difficultyStateMachine.CurrentState;

    private readonly List<DungeonDoorController> _activeDoors = new();
    private readonly List<Node2D> _activeEnemies = new();
    private readonly DungeonEncounterDifficultyStateMachine
        _difficultyStateMachine = new();
    private EnemySpawner _spawner;
    private bool _spawnPhaseCompleted;

    public void Configure(EnemySpawner spawner)
    {
        if(GodotObject.IsInstanceValid(_spawner))
        {
            _spawner.EnemySpawned -= OnEnemySpawned;
            _spawner.TimelineCompleted -= OnTimelineCompleted;
        }

        _spawner = spawner;

        if(!GodotObject.IsInstanceValid(_spawner))
            return;

        _spawner.EnemySpawned += OnEnemySpawned;
        _spawner.TimelineCompleted += OnTimelineCompleted;
    }

    public bool StartEncounter(
        DungeonRoomData room,
        IReadOnlyList<DungeonDoorController> doors
    )
    {
        if(
            room == null ||
            !room.Type.IsCombatRoom() ||
            room.Cleared ||
            ActiveRoom != null ||
            !GodotObject.IsInstanceValid(_spawner)
        )
        {
            return false;
        }

        ActiveRoom = room;
        room.Visited = true;
        _spawnPhaseCompleted = false;
        _activeDoors.Clear();
        _activeEnemies.Clear();

        foreach(DungeonDoorController door in doors)
        {
            if(!GodotObject.IsInstanceValid(door))
                continue;

            _activeDoors.Add(door);
            door.SetLocked(true);
        }

        _spawner.ConfigureSpawnCells(room.EnemySpawnCells);
        DungeonEncounterDifficulty? previousDifficulty =
            _difficultyStateMachine.CurrentState?.Difficulty;
        DungeonEncounterDifficultyState difficultyState =
            _difficultyStateMachine.TransitionTo(
                room.EncounterDifficulty
            );

        if(previousDifficulty != difficultyState.Difficulty)
        {
            EmitSignal(
                SignalName.DifficultyStateChanged,
                (int)difficultyState.Difficulty,
                difficultyState.DurationSeconds
            );
        }

        if(!difficultyState.StartEncounter(_spawner, room))
        {
            GD.PushError(
                $"战斗房 {room.Id} 的怪潮启动失败，门已恢复开启。"
            );
            UnlockActiveDoors();
            ActiveRoom = null;
            return false;
        }

        EmitSignal(SignalName.EncounterStarted, room.Id);
        GD.Print(
            $"[DungeonEncounter] 战斗房 {room.Id} 已封门，" +
            $"难度={difficultyState.Difficulty}，" +
            $"时长={difficultyState.DurationSeconds:0} 秒。"
        );
        return true;
    }

    public override void _Process(double delta)
    {
        if(ActiveRoom == null || !_spawnPhaseCompleted)
            return;

        for(int index = _activeEnemies.Count - 1; index >= 0; index--)
        {
            Node2D enemy = _activeEnemies[index];

            if(
                !GodotObject.IsInstanceValid(enemy) ||
                !enemy.IsInGroup("enemy")
            )
            {
                _activeEnemies.RemoveAt(index);
            }
        }

        if(_activeEnemies.Count == 0)
            CompleteEncounter();
    }

    private void OnEnemySpawned(Node2D enemy, string eventName)
    {
        if(ActiveRoom == null || !GodotObject.IsInstanceValid(enemy))
            return;

        _activeEnemies.Add(enemy);
    }

    private void OnTimelineCompleted()
    {
        if(ActiveRoom == null)
            return;

        _spawnPhaseCompleted = true;

        ForceAwakenRemainingFungi();

        GD.Print(
            $"[DungeonEncounter] 战斗房 {ActiveRoom.Id} 计时结束，" +
            "等待清除现存敌人。"
        );
    }

    private void ForceAwakenRemainingFungi()
    {
        foreach(Node2D enemy in _activeEnemies)
        {
            if(!GodotObject.IsInstanceValid(enemy))
                continue;

            if(enemy is TheMoldSpotFungus fungus)
                fungus.ForceAwaken();
        }
    }

    private void CompleteEncounter()
    {
        DungeonRoomData completedRoom = ActiveRoom;
        completedRoom.Cleared = true;
        UnlockActiveDoors();
        _spawner.EndEncounter();
        ActiveRoom = null;
        _spawnPhaseCompleted = false;
        _activeEnemies.Clear();

        EmitSignal(SignalName.EncounterCleared, completedRoom.Id);
        GD.Print(
            $"[DungeonEncounter] 战斗房 {completedRoom.Id} 已清理，门已开启。"
        );
    }

    private void UnlockActiveDoors()
    {
        foreach(DungeonDoorController door in _activeDoors)
        {
            if(GodotObject.IsInstanceValid(door))
                door.SetLocked(false);
        }

        _activeDoors.Clear();
    }

    public override void _ExitTree()
    {
        if(!GodotObject.IsInstanceValid(_spawner))
            return;

        _spawner.EnemySpawned -= OnEnemySpawned;
        _spawner.TimelineCompleted -= OnTimelineCompleted;
    }
}
