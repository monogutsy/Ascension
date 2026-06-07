using ClassPrestige.Config;
using ClassPrestige.Models;
using ClassPrestige.UI;

using Terraria;

using TShockAPI;

using PlayerData = ClassPrestige.Models.PlayerData;

namespace ClassPrestige.Managers;
public sealed class ExpManager(PlayerManager playerManager, AntiAbuseManager antiAbuseManager, PluginConfig config, PrestigeManager? prestigeManager = null)
{
    private static Random SharedRandom => Random.Shared;
    private readonly Dictionary<int, BossFight> _activeBossFights = [];

    public ClassType? DetectClass(TSPlayer player, NPC npc)
    {
        if (WasKilledByMinionOrSentry(npc))
        {
            return ClassType.Summoner;
        }

        var weapon = player.SelectedItem;

        if (weapon.damage <= 0)
        {
            return null;
        }

        if (weapon.melee)
            return ClassType.Melee;
        if (weapon.ranged)
            return ClassType.Ranged;
        if (weapon.magic)
            return ClassType.Magic;
        if (weapon.summon)
            return ClassType.Summoner;

        return null;
    }
    private static bool WasKilledByMinionOrSentry(NPC npc)
    {
        int playerIndex = npc.lastInteraction;

        if (playerIndex < 0 || playerIndex >= Main.maxPlayers)
        {
            return false;
        }

        for (int i = 0; i < Main.maxProjectiles; i++)
        {
            var proj = Main.projectile[i];

            if (!proj.active)
                continue;

            if (proj.owner != playerIndex)
                continue;

            if (proj.minion || proj.sentry)
            {
                if (proj.Hitbox.Intersects(npc.Hitbox))
                {
                    return true;
                }
            }
        }

        return false;
    }

    public void ProcessKill(TSPlayer player, NPC npc, double diminishingMultiplier)
    {
        ClassType? detectedClass = DetectClass(player, npc);
        if (detectedClass is null)
            return;

        var uuid = player.Account?.Name;
        if (uuid == null)
            return;

        var data = playerManager.GetPlayer(uuid);
        if (data is null)
            return;

        int baseExp = DetermineBaseExp(npc);

        double bonus = CalculateBonus(data);

        int bonusExp = (int)Math.Floor(baseExp * (1.0 + bonus));

        int drExp = (int)Math.Floor(bonusExp * diminishingMultiplier);

        double eventMultiplier = antiAbuseManager.GetEventMultiplier();
        int finalExp = (int)Math.Floor(drExp * eventMultiplier);

        if (finalExp > 0)
        {
            playerManager.ApplyExp(data, detectedClass.Value, finalExp);
            data.PrestigeExp += finalExp;

            prestigeManager?.EvaluatePrestige(data, player);

            if (config.EnableExpNotifications && data.ExpNotificationsEnabled)
            {
                player.SendMessage(
                    $"+{finalExp} {detectedClass.Value} EXP",
                    UiHelper.GetClassColor(detectedClass.Value));
            }
        }
    }

    public int DetermineBaseExp(NPC npc)
    {
        if (npc.boss)
        {
            return SharedRandom.Next(config.BossMinExp, config.BossMaxExp + 1);
        }

        if (config.RareMobIds is not null && Array.IndexOf(config.RareMobIds, npc.type) >= 0)
        {
            return SharedRandom.Next(config.RareMobMinExp, config.RareMobMaxExp + 1);
        }

        return config.CommonMobExp;
    }
    public double CalculateBonus(PlayerData data)
    {
        double prestigeBonus = data.PrestigeRank * 0.02;
        double rebirthBonus = data.RebirthCount * 0.05;
        double maxBonus = config.MaxEXPBonusPercent / 100.0;

        return Math.Min(prestigeBonus + rebirthBonus, maxBonus);
    }

    public void TrackBossDamage(int npcIndex, int npcType, string playerUuid, int damage)
    {
        if (damage <= 0)
            return;

        if (!_activeBossFights.TryGetValue(npcIndex, out var bossFight))
        {
            bossFight = new BossFight
            {
                NpcIndex = npcIndex,
                NpcType = npcType
            };
            _activeBossFights[npcIndex] = bossFight;
        }

        if (bossFight.DamageByPlayer.TryGetValue(playerUuid, out int existing))
        {
            bossFight.DamageByPlayer[playerUuid] = existing + damage;
        }
        else
        {
            bossFight.DamageByPlayer[playerUuid] = damage;
        }

        bossFight.TotalDamage += damage;
    }
    public void ProcessBossKill(NPC npc)
    {
        if (!_activeBossFights.TryGetValue(npc.whoAmI, out var bossFight))
        {
            return;
        }

        if (bossFight.TotalDamage <= 0)
        {
            _activeBossFights.Remove(npc.whoAmI);
            return;
        }

        int baseExp = SharedRandom.Next(config.BossMinExp, config.BossMaxExp + 1);

        foreach (var (playerUuid, playerDamage) in bossFight.DamageByPlayer)
        {
            double participationPercent = (double)playerDamage / bossFight.TotalDamage * 100.0;
            if (participationPercent < config.BossParticipationPercent)
            {
                continue;
            }

            TSPlayer? tsPlayer = FindConnectedPlayer(playerUuid);
            if (tsPlayer is null)
            {
                continue;
            }

            var data = playerManager.GetPlayer(playerUuid);
            if (data is null)
            {
                continue;
            }

            ClassType? detectedClass = DetectClassForPlayer(tsPlayer);
            if (detectedClass is null)
            {
                continue;
            }

            double bonus = CalculateBonus(data);
            int bonusExp = (int)Math.Floor(baseExp * (1.0 + bonus));

            if (bonusExp > 0)
            {
                playerManager.ApplyExp(data, detectedClass.Value, bonusExp);
                data.PrestigeExp += bonusExp;

                prestigeManager?.EvaluatePrestige(data, tsPlayer);

                if (config.EnableExpNotifications && data.ExpNotificationsEnabled)
                {
                    tsPlayer.SendMessage(
                        $"+{bonusExp} {detectedClass.Value} EXP (Boss: {participationPercent:F1}%)",
                        UiHelper.GetClassColor(detectedClass.Value));
                }
            }
        }

        _activeBossFights.Remove(npc.whoAmI);
    }
    public BossFight? GetBossFight(int npcIndex)
    {
        _activeBossFights.TryGetValue(npcIndex, out var bossFight);
        return bossFight;
    }

    private static ClassType? DetectClassForPlayer(TSPlayer player)
    {
        var weapon = player.SelectedItem;

        if (weapon.damage <= 0)
        {
            return null;
        }

        if (weapon.melee)
            return ClassType.Melee;
        if (weapon.ranged)
            return ClassType.Ranged;
        if (weapon.magic)
            return ClassType.Magic;
        if (weapon.summon)
            return ClassType.Summoner;

        return null;
    }
    private static TSPlayer? FindConnectedPlayer(string uuid)
    {
        for (int i = 0; i < TShock.Players.Length; i++)
        {
            var tsPlayer = TShock.Players[i];
            if (tsPlayer is null || !tsPlayer.Active || tsPlayer.Account == null)
                continue;

            if (tsPlayer.Account.Name == uuid)
                return tsPlayer;
        }

        return null;
    }
}
