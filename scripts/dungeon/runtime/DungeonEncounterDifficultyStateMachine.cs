using System;
using System.Collections.Generic;

public enum DungeonEncounterDifficulty
{
    Early,
    Middle,
    Late
}

/// <summary>
/// A difficulty state owns every rule that changes with dungeon progression.
/// Enemy count and enemy-pool rules can be added to PrepareSpawner without
/// spreading difficulty switches through the encounter controller.
/// </summary>
public abstract class DungeonEncounterDifficultyState
{
    public abstract DungeonEncounterDifficulty Difficulty { get; }
    public abstract float DurationSeconds { get; }

    public bool StartEncounter(
        EnemySpawner spawner,
        DungeonRoomData room
    )
    {
        PrepareSpawner(spawner, room);
        return spawner.RestartTimeline(DurationSeconds);
    }

    protected virtual void PrepareSpawner(
        EnemySpawner spawner,
        DungeonRoomData room
    )
    {
    }
}

public sealed class DungeonEncounterDifficultyStateMachine
{
    private sealed class EarlyState : DungeonEncounterDifficultyState
    {
        public override DungeonEncounterDifficulty Difficulty =>
            DungeonEncounterDifficulty.Early;
        public override float DurationSeconds => 60.0f;
    }

    private sealed class MiddleState : DungeonEncounterDifficultyState
    {
        public override DungeonEncounterDifficulty Difficulty =>
            DungeonEncounterDifficulty.Middle;
        public override float DurationSeconds => 75.0f;
    }

    private sealed class LateState : DungeonEncounterDifficultyState
    {
        public override DungeonEncounterDifficulty Difficulty =>
            DungeonEncounterDifficulty.Late;
        public override float DurationSeconds => 90.0f;
    }

    private readonly Dictionary<
        DungeonEncounterDifficulty,
        DungeonEncounterDifficultyState
    > _states = new()
    {
        [DungeonEncounterDifficulty.Early] = new EarlyState(),
        [DungeonEncounterDifficulty.Middle] = new MiddleState(),
        [DungeonEncounterDifficulty.Late] = new LateState()
    };

    public DungeonEncounterDifficultyState CurrentState { get; private set; }

    public DungeonEncounterDifficultyState TransitionTo(
        DungeonEncounterDifficulty difficulty
    )
    {
        CurrentState = _states[difficulty];
        return CurrentState;
    }

    /// <summary>
    /// Assigns the generated monster rooms to the 30% / 40% / 30% states in
    /// exploration order. Both 30% edge groups use the same rounded size so
    /// small generated dungeons stay symmetric.
    /// </summary>
    public static void AssignRoomStates(DungeonLayout layout)
    {
        if(layout == null)
            return;

        List<DungeonRoomData> monsterRooms = new();

        foreach(DungeonRoomData room in layout.Rooms)
        {
            if(room.Type == DungeonRoomType.Monster)
                monsterRooms.Add(room);
            else if(room.Type == DungeonRoomType.Extraction)
                room.EncounterDifficulty = DungeonEncounterDifficulty.Late;
        }

        monsterRooms.Sort((left, right) => left.Id.CompareTo(right.Id));

        int edgeRoomCount = (int)Math.Round(
            monsterRooms.Count * 0.3,
            MidpointRounding.AwayFromZero
        );
        int lateStartIndex = monsterRooms.Count - edgeRoomCount;

        for(int index = 0; index < monsterRooms.Count; index++)
        {
            monsterRooms[index].EncounterDifficulty = index < edgeRoomCount
                ? DungeonEncounterDifficulty.Early
                : index < lateStartIndex
                    ? DungeonEncounterDifficulty.Middle
                    : DungeonEncounterDifficulty.Late;
        }
    }
}
