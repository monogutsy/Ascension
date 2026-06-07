using System.Collections.Concurrent;

using ClassPrestige.Config;
using ClassPrestige.Models;

using TShockAPI;

using PlayerData = ClassPrestige.Models.PlayerData;

namespace ClassPrestige.Managers;
public sealed class RewardManager(PlayerManager playerManager, PluginConfig config)
{
    private readonly ConcurrentQueue<PendingReward> _pendingRewards = new();
    private static readonly int[] MilestoneThresholds = [10, 25, 50, 75, 100];
    public void CheckMilestones(PlayerData data, TSPlayer player, ClassType classType, int newLevel)
    {
        if (!IsMilestoneLevel(newLevel))
            return;

        if (!config.Milestones.TryGetValue(newLevel, out var rewards) || rewards is not { Length: > 0 })
            return;

        bool changed = false;

        for (int i = 0; i < rewards.Length; i++)
        {
            var reward = rewards[i];

            string rewardKey = $"{classType}_{newLevel}_{i}";

            if (data.UnlockedRewards.Contains(rewardKey))
                continue;

            bool granted = GrantReward(data, player, reward, rewardKey, classType, newLevel);

            if (granted)
            {
                data.UnlockedRewards.Add(rewardKey);
                changed = true;

                SendRewardNotification(player, classType, newLevel, reward);
            }
        }

        if (changed)
        {
            data.IsDirty = true;
        }
    }
    private bool GrantReward(PlayerData data, TSPlayer player, MilestoneReward reward, string rewardKey, ClassType classType, int milestoneLevel)
    {
        return reward.Type.ToLowerInvariant() switch
        {
            "item" or "crate" => GrantItemReward(player, data, reward, rewardKey),
            "title" => GrantTitleReward(data, reward),
            _ => LogUnknownRewardType(reward.Type, milestoneLevel)
        };
    }
    private bool GrantItemReward(TSPlayer player, PlayerData data, MilestoneReward reward, string rewardKey)
    {
        if (!HasInventorySpace(player))
        {
            _pendingRewards.Enqueue(new PendingReward
            {
                PlayerUUID = data.UUID,
                RewardKey = rewardKey,
                Type = reward.Type,
                ItemId = reward.ItemId,
                Quantity = reward.Quantity,
                QueuedAt = DateTime.UtcNow
            });

            player.SendInfoMessage("[ClassPrestige] Your inventory is full. The reward will be delivered when space is available.");
            TShock.Log.ConsoleInfo($"[ClassPrestige] Queued pending reward for {data.PlayerName}: {reward.Type} x{reward.Quantity} (ItemId: {reward.ItemId})");
        }
        else
        {
            player.GiveItem(reward.ItemId, reward.Quantity);
        }

        return true;
    }
    private static bool HasInventorySpace(TSPlayer player)
    {
        for (int i = 0; i < 50; i++)
        {
            if (player.TPlayer.inventory[i] == null || player.TPlayer.inventory[i].type == 0)
                return true;
        }

        return false;
    }
    private static bool GrantTitleReward(PlayerData data, MilestoneReward reward)
    {
        if (!string.IsNullOrEmpty(reward.Title) && !data.UnlockedTitles.Contains(reward.Title))
        {
            data.UnlockedTitles.Add(reward.Title);
        }

        return true;
    }
    private static bool LogUnknownRewardType(string type, int milestoneLevel)
    {
        TShock.Log.ConsoleError($"[ClassPrestige] Unknown reward type '{type}' at milestone {milestoneLevel}.");
        return false;
    }
    private static void SendRewardNotification(TSPlayer player, ClassType classType, int milestoneLevel, MilestoneReward reward)
    {
        string rewardDescription = reward.Type.ToLowerInvariant() switch
        {
            "item" => $"item x{reward.Quantity}",
            "crate" => $"crate x{reward.Quantity}",
            "title" => $"title \"{reward.Title}\"",
            _ => "reward"
        };

        player.SendInfoMessage($"[ClassPrestige] Milestone reached! {classType} Level {milestoneLevel} â€” Reward: {rewardDescription}");
    }
    public void RetryPendingRewards(TSPlayer player)
    {
        if (_pendingRewards.IsEmpty)
            return;

        var playerData = playerManager.GetPlayer(player.Account?.Name ?? string.Empty);
        string playerUUID = playerData?.UUID ?? player.Account?.Name ?? string.Empty;

        var stillPending = new List<PendingReward>();

        int count = _pendingRewards.Count;
        for (int i = 0; i < count; i++)
        {
            if (!_pendingRewards.TryDequeue(out var pending))
                break;

            if (pending.PlayerUUID != playerUUID)
            {
                stillPending.Add(pending);
                continue;
            }

            if (!HasInventorySpace(player))
            {
                stillPending.Add(pending);
            }
            else
            {
                player.GiveItem(pending.ItemId, pending.Quantity);
                player.SendInfoMessage($"[ClassPrestige] Pending reward delivered: {pending.Type} x{pending.Quantity}");
            }
        }

        foreach (var pending in stillPending)
        {
            _pendingRewards.Enqueue(pending);
        }
    }
    public void OnPlayerLogin(TSPlayer player)
    {
        RetryPendingRewards(player);
    }
    public static bool IsMilestoneLevel(int level)
    {
        for (int i = 0; i < MilestoneThresholds.Length; i++)
        {
            if (MilestoneThresholds[i] == level)
                return true;
        }

        return false;
    }
    public int PendingRewardCount => _pendingRewards.Count;
}
