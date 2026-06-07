using ClassPrestige.Config;
using ClassPrestige.Interfaces;
using ClassPrestige.Managers;
using ClassPrestige.Models;

using TShockAPI;

using PlayerData = ClassPrestige.Models.PlayerData;

namespace ClassPrestige.Commands;
public sealed class AdminCommands(PlayerManager playerManager, ConfigManager configManager, IDatabase database, PrestigeManager prestigeManager)
{
    public void Register()
    {
        TShockAPI.Commands.ChatCommands.Add(new Command(Permissions.Admin, AddExpCommand, "addexp")
            { HelpText = "Add EXP to a player's class. Usage: /addexp <player> <class> <amount>" });
        TShockAPI.Commands.ChatCommands.Add(new Command(Permissions.Admin, SetLevelCommand, "setlevel")
            { HelpText = "Set a player's class level. Usage: /setlevel <player> <class> <level>" });
        TShockAPI.Commands.ChatCommands.Add(new Command(Permissions.Admin, SetPrestigeCommand, "setprestige")
            { HelpText = "Set a player's prestige rank. Usage: /setprestige <player> <rank>" });
        TShockAPI.Commands.ChatCommands.Add(new Command(Permissions.Admin, ResetPlayerCommand, "resetplayer")
            { HelpText = "Reset all progression for a player. Usage: /resetplayer <player>" });
        TShockAPI.Commands.ChatCommands.Add(new Command(Permissions.Admin, ReloadLevelsCommand, "reloadlevels")
            { HelpText = "Reload the ClassPrestige configuration from disk." });
    }
    private void AddExpCommand(CommandArgs args)
    {
        var admin = args.Player;

        if (args.Parameters.Count < 3)
        {
            admin.SendErrorMessage("[ClassPrestige] Usage: /addexp <player> <class> <amount>");
            admin.SendErrorMessage("  Classes: Melee, Ranged, Magic, Summoner");
            return;
        }

        string targetName = args.Parameters[0];
        string className = args.Parameters[1];
        string amountStr = args.Parameters[2];

        if (!Enum.TryParse<ClassType>(className, ignoreCase: true, out var classType))
        {
            admin.SendErrorMessage($"[ClassPrestige] Invalid class '{className}'. Valid: Melee, Ranged, Magic, Summoner.");
            return;
        }

        if (!int.TryParse(amountStr, out int amount) || amount <= 0)
        {
            admin.SendErrorMessage($"[ClassPrestige] Invalid amount '{amountStr}'. Must be a positive integer.");
            return;
        }

        _ = ExecuteOnTargetAsync(admin, targetName, async (data, tsPlayer) =>
        {
            int levelsGained = playerManager.ApplyExp(data, classType, amount);

            data.PrestigeExp += amount;
            data.IsDirty = true;

            if (tsPlayer != null)
            {
                prestigeManager.EvaluatePrestige(data, tsPlayer);
            }

            if (tsPlayer != null)
            {
                await playerManager.SavePlayerAsync(data.UUID).ConfigureAwait(false);
                tsPlayer.SendInfoMessage($"[ClassPrestige] An admin added {amount} {classType} EXP to your account. Levels gained: {levelsGained}.");
            }
            else
            {
                data.LastSave = DateTime.UtcNow;
                await database.SaveAsync(data).ConfigureAwait(false);
            }

            admin.SendSuccessMessage($"[ClassPrestige] Added {amount} {classType} EXP to {data.PlayerName}. Levels gained: {levelsGained}.");

            string adminName = admin.Account?.Name ?? admin.Name;
            TShock.Log.ConsoleInfo($"[ClassPrestige] Admin {adminName} executed /addexp on {data.PlayerName}: class={classType}, amount={amount}, levelsGained={levelsGained}");
        });
    }
    private void SetLevelCommand(CommandArgs args)
    {
        var admin = args.Player;

        if (args.Parameters.Count < 3)
        {
            admin.SendErrorMessage("[ClassPrestige] Usage: /setlevel <player> <class> <level>");
            admin.SendErrorMessage("  Classes: Melee, Ranged, Magic, Summoner");
            return;
        }

        string targetName = args.Parameters[0];
        string className = args.Parameters[1];
        string levelStr = args.Parameters[2];

        if (!Enum.TryParse<ClassType>(className, ignoreCase: true, out var classType))
        {
            admin.SendErrorMessage($"[ClassPrestige] Invalid class '{className}'. Valid: Melee, Ranged, Magic, Summoner.");
            return;
        }

        int maxLevel = configManager.Current.MaxLevel;

        if (!int.TryParse(levelStr, out int level) || level < 0 || level > maxLevel)
        {
            admin.SendErrorMessage($"[ClassPrestige] Invalid level '{levelStr}'. Must be between 0 and {maxLevel}.");
            return;
        }

        _ = ExecuteOnTargetAsync(admin, targetName, async (data, tsPlayer) =>
        {
            PlayerManager.SetLevel(data, classType, level);
            PlayerManager.SetExp(data, classType, 0);
            data.IsDirty = true;

            if (tsPlayer != null)
            {
                await playerManager.SavePlayerAsync(data.UUID).ConfigureAwait(false);
                tsPlayer.SendInfoMessage($"[ClassPrestige] Your {classType} level has been set to {level} by an admin.");
            }
            else
            {
                data.LastSave = DateTime.UtcNow;
                await database.SaveAsync(data).ConfigureAwait(false);
            }

            admin.SendSuccessMessage($"[ClassPrestige] Set {data.PlayerName}'s {classType} level to {level}.");

            string adminName = admin.Account?.Name ?? admin.Name;
            TShock.Log.ConsoleInfo($"[ClassPrestige] Admin {adminName} executed /setlevel on {data.PlayerName}: class={classType}, level={level}");
        });
    }
    private void SetPrestigeCommand(CommandArgs args)
    {
        var admin = args.Player;

        if (args.Parameters.Count < 2)
        {
            admin.SendErrorMessage("[ClassPrestige] Usage: /setprestige <player> <rank>");
            admin.SendErrorMessage("  Rank: 0-4");
            return;
        }

        string targetName = args.Parameters[0];
        string rankStr = args.Parameters[1];

        if (!int.TryParse(rankStr, out int rank) || rank < 0 || rank > 4)
        {
            admin.SendErrorMessage($"[ClassPrestige] Invalid rank '{rankStr}'. Must be between 0 and 4.");
            return;
        }

        _ = ExecuteOnTargetAsync(admin, targetName, async (data, tsPlayer) =>
        {
            data.PrestigeRank = rank;

            if (rank == 0)
            {
                data.PrestigeExp = 0;
            }
            else
            {
                data.PrestigeExp = configManager.Current.PrestigeThresholds[rank - 1];
            }

            data.IsDirty = true;

            if (tsPlayer != null)
            {
                await playerManager.SavePlayerAsync(data.UUID).ConfigureAwait(false);
                tsPlayer.SendInfoMessage($"[ClassPrestige] Your prestige rank has been set to {rank} by an admin.");
            }
            else
            {
                data.LastSave = DateTime.UtcNow;
                await database.SaveAsync(data).ConfigureAwait(false);
            }

            admin.SendSuccessMessage($"[ClassPrestige] Set {data.PlayerName}'s prestige rank to {rank}.");

            string adminName = admin.Account?.Name ?? admin.Name;
            TShock.Log.ConsoleInfo($"[ClassPrestige] Admin {adminName} executed /setprestige on {data.PlayerName}: rank={rank}");
        });
    }
    private void ResetPlayerCommand(CommandArgs args)
    {
        var admin = args.Player;

        if (args.Parameters.Count < 1)
        {
            admin.SendErrorMessage("[ClassPrestige] Usage: /resetplayer <player>");
            return;
        }

        string targetName = args.Parameters[0];

        _ = ExecuteOnTargetAsync(admin, targetName, async (data, tsPlayer) =>
        {
            data.MeleeLevel = 0;
            data.RangedLevel = 0;
            data.MagicLevel = 0;
            data.SummonerLevel = 0;

            data.MeleeExp = 0;
            data.RangedExp = 0;
            data.MagicExp = 0;
            data.SummonerExp = 0;

            data.PrestigeRank = 0;
            data.PrestigeExp = 0;
            data.PrestigeCycles = 0;

            data.RebirthCount = 0;

            data.UnlockedTitles.Clear();
            data.UnlockedRewards.Clear();

            data.IsDirty = true;

            if (tsPlayer != null)
            {
                await playerManager.SavePlayerAsync(data.UUID).ConfigureAwait(false);
                tsPlayer.SendInfoMessage("[ClassPrestige] Your progression has been fully reset by an admin.");
            }
            else
            {
                data.LastSave = DateTime.UtcNow;
                await database.SaveAsync(data).ConfigureAwait(false);
            }

            admin.SendSuccessMessage($"[ClassPrestige] Reset all progression for {data.PlayerName}.");

            string adminName = admin.Account?.Name ?? admin.Name;
            TShock.Log.ConsoleInfo($"[ClassPrestige] Admin {adminName} executed /resetplayer on {data.PlayerName}: all progression reset");
        });
    }
    private void ReloadLevelsCommand(CommandArgs args)
    {
        var admin = args.Player;

        _ = ReloadLevelsAsync(admin);
    }
    private async Task ReloadLevelsAsync(TSPlayer admin)
    {
        try
        {
            string? error = await configManager.ReloadAsync().ConfigureAwait(false);

            if (error is null)
            {
                admin.SendSuccessMessage("[ClassPrestige] Configuration reloaded successfully.");
            }
            else
            {
                admin.SendErrorMessage($"[ClassPrestige] Reload failed: {error}");
            }

            string adminName = admin.Account?.Name ?? admin.Name;
            TShock.Log.ConsoleInfo($"[ClassPrestige] Admin {adminName} executed /reloadlevels: {(error is null ? "success" : $"failed - {error}")}");
        }
        catch (Exception ex)
        {
            admin.SendErrorMessage($"[ClassPrestige] Reload error: {ex.Message}");
            TShock.Log.ConsoleError($"[ClassPrestige] Error during /reloadlevels: {ex.Message}");
        }
    }

    private async Task ExecuteOnTargetAsync(TSPlayer admin, string targetName, Func<PlayerData, TSPlayer?, Task> action)
    {
        try
        {
            var onlinePlayers = TShock.Players
                .Where(p => p != null && p.Active && p.Name.Equals(targetName, StringComparison.OrdinalIgnoreCase))
                .ToList();

            if (onlinePlayers.Count > 1)
            {
                admin.SendErrorMessage($"[ClassPrestige] Multiple online players match '{targetName}'. Please be more specific.");
                return;
            }

            if (onlinePlayers.Count == 1)
            {
                var tsPlayer = onlinePlayers[0];

                if (tsPlayer.Account == null)
                {
                    admin.SendErrorMessage($"[ClassPrestige] Player '{targetName}' is online but not logged in yet.");
                    return;
                }

                var data = playerManager.GetPlayer(tsPlayer.Account.Name);

                if (data is null)
                {
                    admin.SendErrorMessage($"[ClassPrestige] Player '{targetName}' is online but data is still loading.");
                    return;
                }

                await action(data, tsPlayer).ConfigureAwait(false);
                return;
            }

            var userAccount = TShock.UserAccounts.GetUserAccountByName(targetName);
            if (userAccount is null)
            {
                admin.SendErrorMessage($"[ClassPrestige] Player '{targetName}' not found (not online and no account found).");
                return;
            }

            var offlineData = await database.LoadAsync(userAccount.Name).ConfigureAwait(false);
            if (offlineData is null)
            {
                admin.SendErrorMessage($"[ClassPrestige] Player '{targetName}' has no progression data in the database.");
                return;
            }

            await action(offlineData, null).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            admin.SendErrorMessage($"[ClassPrestige] Error executing command: {ex.Message}");
            TShock.Log.ConsoleError($"[ClassPrestige] Admin command error for target '{targetName}': {ex.Message}");
        }
    }
}
