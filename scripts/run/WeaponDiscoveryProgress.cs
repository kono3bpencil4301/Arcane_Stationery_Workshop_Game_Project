using Godot;
using System.Collections.Generic;

public enum WeaponExtractionRewardKind
{
    WeaponUnlocked,
    BlueprintUnlocked,
    BlueprintGranted
}

public sealed class WeaponExtractionReward
{
    public string WeaponId { get; }
    public WeaponExtractionRewardKind Kind { get; }

    public WeaponExtractionReward(
        string weaponId,
        WeaponExtractionRewardKind kind
    )
    {
        WeaponId = weaponId;
        Kind = kind;
    }
}

[GlobalClass]
public partial class WeaponDiscoveryProgress : Node
{
    [Signal]
    public delegate void RewardGrantedEventHandler(string weaponId, int rewardKind);

    private readonly Dictionary<string, int> _extractionCounts = new();
    private readonly HashSet<string> _committedExtractionIds = new();

    public int GetSuccessfulExtractionCount(string weaponId)
    {
        return _extractionCounts.TryGetValue(weaponId, out int count)
            ? count
            : 0;
    }

    public IReadOnlyList<WeaponExtractionReward> CommitSuccessfulExtraction(
        string extractionId,
        IEnumerable<InventoryWeaponEntry> carriedWeapons
    )
    {
        List<WeaponExtractionReward> rewards = new();

        if (
            string.IsNullOrWhiteSpace(extractionId) ||
            carriedWeapons == null ||
            !_committedExtractionIds.Add(extractionId)
        )
        {
            return rewards;
        }

        foreach (InventoryWeaponEntry entry in carriedWeapons)
        {
            string weaponId = entry?.Data?.ItemId;
            if (string.IsNullOrWhiteSpace(weaponId))
                continue;

            int count = GetSuccessfulExtractionCount(weaponId) + 1;
            _extractionCounts[weaponId] = count;

            WeaponExtractionRewardKind kind = count switch
            {
                1 => WeaponExtractionRewardKind.WeaponUnlocked,
                2 => WeaponExtractionRewardKind.BlueprintUnlocked,
                _ => WeaponExtractionRewardKind.BlueprintGranted
            };

            rewards.Add(new WeaponExtractionReward(weaponId, kind));
            EmitSignal(SignalName.RewardGranted, weaponId, (int)kind);
        }

        return rewards;
    }
}
