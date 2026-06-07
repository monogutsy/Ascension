using ClassPrestige.Managers;

using Terraria;

using TerrariaApi.Server;

using TShockAPI;

namespace ClassPrestige.Hooks;
public sealed class KillHooks(
    ExpManager expManager,
    AntiAbuseManager antiAbuseManager,
    PlayerManager playerManager)
{
    public void OnNpcKilled(NpcKilledEventArgs args)
    {
        try
        {
            var npc = args.npc;
            if (npc == null || !npc.active)
                return;

            if (npc.boss)
            {
                expManager.ProcessBossKill(npc);
                return;
            }

            var player = FindKillingPlayer(npc);
            if (player == null)
                return;

            var result = antiAbuseManager.Validate(player, npc);
            if (!result.IsValid)
                return;

            expManager.ProcessKill(player, npc, result.Multiplier);
        }
        catch (Exception ex)
        {
            TShock.Log.ConsoleError($"[ClassPrestige] Error in NpcKilled handler: {ex.Message}");
        }
    }
    public void OnNpcStrike(NpcStrikeEventArgs args)
    {
        try
        {
            var player = TShock.Players[args.Player.whoAmI];
            if (player?.Account == null)
                return;

            antiAbuseManager.RecordCombatAction(player);

            var npc = Main.npc[args.Npc.whoAmI];
            if (npc == null || !npc.boss)
                return;

            var uuid = player.Account.Name;
            expManager.TrackBossDamage(npc.whoAmI, npc.type, uuid, args.Damage);
        }
        catch (Exception ex)
        {
            TShock.Log.ConsoleError($"[ClassPrestige] Error in NpcStrike handler: {ex.Message}");
        }
    }
    private static TSPlayer? FindKillingPlayer(NPC npc)
    {
        if (npc.lastInteraction >= 0 && npc.lastInteraction < Main.maxPlayers)
        {
            var player = TShock.Players[npc.lastInteraction];
            if (player is { Active: true, IsLoggedIn: true })
                return player;
        }

        return null;
    }
}
