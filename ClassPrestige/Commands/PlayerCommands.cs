using ClassPrestige.Config;
using ClassPrestige.Managers;
using ClassPrestige.Models;
using ClassPrestige.UI;

using Microsoft.Xna.Framework;

using TShockAPI;

using PlayerData = ClassPrestige.Models.PlayerData;

namespace ClassPrestige.Commands;

public sealed class PlayerCommands(
    PlayerManager playerManager,
    PrestigeManager prestigeManager,
    RebirthManager rebirthManager,
    LeaderboardManager leaderboardManager,
    PluginConfig config)
{
    public void Register()
    {
        TShockAPI.Commands.ChatCommands.Add(new Command(Permissions.Player, LevelCommand, "level")
            { HelpText = "View your class levels." });
        TShockAPI.Commands.ChatCommands.Add(new Command(Permissions.Player, ClassStatsCommand, "classstats")
            { HelpText = "View detailed class EXP stats." });
        TShockAPI.Commands.ChatCommands.Add(new Command(Permissions.Player, PrestigeCommand, "prestige")
            { HelpText = "View your prestige rank and progress." });
        TShockAPI.Commands.ChatCommands.Add(new Command(Permissions.Player, RebirthCommand, "rebirth")
            { HelpText = "Attempt a rebirth or view progress." });
        TShockAPI.Commands.ChatCommands.Add(new Command(Permissions.Player, TopLevelsCommand, "toplevels")
            { HelpText = "View the top levels leaderboard." });
        TShockAPI.Commands.ChatCommands.Add(new Command(Permissions.Player, TopPrestigeCommand, "topprestige")
            { HelpText = "View the top prestige leaderboard." });
        TShockAPI.Commands.ChatCommands.Add(new Command(Permissions.Player, TopRebirthCommand, "toprebirth")
            { HelpText = "View the top rebirth leaderboard." });
        TShockAPI.Commands.ChatCommands.Add(new Command(Permissions.Player, ExpToggleCommand, "exptoggle")
            { HelpText = "Toggle EXP gain notifications." });
        TShockAPI.Commands.ChatCommands.Add(new Command(Permissions.Player, ExpBonusCommand, "expbonus")
            { HelpText = "View your current EXP bonus breakdown." });
        TShockAPI.Commands.ChatCommands.Add(new Command(Permissions.Player, RebirthInfoCommand, "rebirthinfo")
            { HelpText = "View detailed rebirth status and requirements." });
        TShockAPI.Commands.ChatCommands.Add(new Command(Permissions.Player, ProgressionCommand, "progression")
            { HelpText = "Learn about the ClassPrestige progression systems." });
    }

    private void LevelCommand(CommandArgs args)
    {
        var player = args.Player;
        var data = GetPlayerData(player);
        if (data is null) return;

        UiHelper.SendHeader(player, "LEVELS");

        foreach (ClassType ct in Enum.GetValues<ClassType>())
        {
            int level = PlayerManager.GetLevel(data, ct);
            string icon = config.EnableItemIcons ? UiHelper.GetClassIcon(ct) : "";
            string line = $" {icon} {ct,-10} Lv. {level}";
            player.SendMessage(line, UiHelper.GetClassColor(ct));
        }
    }

    private void ClassStatsCommand(CommandArgs args)
    {
        var player = args.Player;
        var data = GetPlayerData(player);
        if (data is null) return;

        UiHelper.SendHeader(player, "CLASS STATS");

        foreach (ClassType ct in Enum.GetValues<ClassType>())
        {
            int level = PlayerManager.GetLevel(data, ct);
            int exp = PlayerManager.GetExp(data, ct);
            int required = PlayerManager.ExpRequired(level);
            string icon = config.EnableItemIcons ? UiHelper.GetClassIcon(ct) : "";

            player.SendMessage(
                $" {icon} {ct}  Lv. {level}",
                UiHelper.GetClassColor(ct));
            player.SendMessage(
                $"   EXP: {UiHelper.Fmt(exp)} / {UiHelper.Fmt(required)}",
                UiHelper.DimColor);
        }
    }

    private void PrestigeCommand(CommandArgs args)
    {
        var player = args.Player;
        var data = GetPlayerData(player);
        if (data is null) return;

        UiHelper.SendHeader(player, "PRESTIGE");

        int derivedRank = prestigeManager.DerivePrestigeRank(data.PrestigeExp);
        if (derivedRank > data.PrestigeRank)
        {
            data.PrestigeRank = derivedRank;
            data.IsDirty = true;
        }

        if (data.PrestigeRank > 0)
        {
            string rankDisplay = $"Prestige {PrestigeManager.ToRomanNumeral(data.PrestigeRank)}";
            player.SendMessage($" Rank: {rankDisplay}", UiHelper.GetPrestigeColor(data.PrestigeRank));
        }
        else
        {
            player.SendMessage(" Rank: None", UiHelper.DimColor);
        }

        player.SendMessage($" EXP: {UiHelper.Fmt(data.PrestigeExp)}", UiHelper.DimColor);

        double bonus = (data.PrestigeRank * 0.02 + data.RebirthCount * 0.05);
        double maxBonus = config.MaxEXPBonusPercent / 100.0;
        double totalBonus = Math.Min(bonus, maxBonus) * 100.0;
        player.SendMessage($" Bonus: +{totalBonus:F0}%", UiHelper.AccentColor);

        int avgLevel = (data.MeleeLevel + data.RangedLevel + data.MagicLevel + data.SummonerLevel) / 4;
        player.SendMessage($" Avg Class Level: {avgLevel} / {config.MaxLevel}", UiHelper.DimColor);

        if (data.PrestigeRank >= 4)
        {
            player.SendMessage($" Cycles: {data.PrestigeCycles}", UiHelper.DimColor);
            player.SendMessage($" Rebirth Progress: {data.PrestigeCycles} / {config.RebirthCyclesRequired} Cycles", UiHelper.AccentColor);
        }
        else
        {
            int nextThreshold = prestigeManager.DeriveNextThreshold(data.PrestigeRank);
            if (nextThreshold > 0)
            {
                int remaining = Math.Max(0, nextThreshold - data.PrestigeExp);
                player.SendMessage($" Next Rank: {UiHelper.Fmt(remaining)} EXP needed", UiHelper.DimColor);
            }
            else
            {
                player.SendMessage(" Next Rank: MAX", UiHelper.AccentColor);
            }
        }
    }

    private void RebirthCommand(CommandArgs args)
    {
        var player = args.Player;
        var data = GetPlayerData(player);
        if (data is null) return;

        var (success, message) = rebirthManager.TryRebirth(data, player);

        if (!success)
        {
            player.SendMessage($" {message}", UiHelper.DimColor);
        }
    }

    private void TopLevelsCommand(CommandArgs args)
    {
        DisplayLeaderboard(args.Player, LeaderboardCategory.TopLevels, "TOP LEVELS");
    }

    private void TopPrestigeCommand(CommandArgs args)
    {
        DisplayLeaderboard(args.Player, LeaderboardCategory.TopPrestige, "TOP PRESTIGE");
    }

    private void TopRebirthCommand(CommandArgs args)
    {
        DisplayLeaderboard(args.Player, LeaderboardCategory.TopRebirth, "TOP REBIRTH");
    }

    private void ExpToggleCommand(CommandArgs args)
    {
        var player = args.Player;
        var data = GetPlayerData(player);
        if (data is null) return;

        data.ExpNotificationsEnabled = !data.ExpNotificationsEnabled;
        data.IsDirty = true;

        if (data.ExpNotificationsEnabled)
            player.SendMessage(" EXP notifications: ON", UiHelper.AccentColor);
        else
            player.SendMessage(" EXP notifications: OFF", UiHelper.DimColor);
    }

    private void ExpBonusCommand(CommandArgs args)
    {
        var player = args.Player;
        var data = GetPlayerData(player);
        if (data is null) return;

        UiHelper.SendHeader(player, "EXP BONUS");

        double prestigeBonus = data.PrestigeRank * 0.02 * 100;
        double rebirthBonus = data.RebirthCount * 0.05 * 100;
        double maxBonus = config.MaxEXPBonusPercent;
        double totalBonus = Math.Min(prestigeBonus + rebirthBonus, maxBonus);

        player.SendMessage($" Prestige Bonus: +{prestigeBonus:F0}%", UiHelper.GetPrestigeColor(data.PrestigeRank));
        player.SendMessage($" Rebirth Bonus: +{rebirthBonus:F0}%", UiHelper.GetRebirthColor(data.RebirthCount));
        player.SendMessage($" Total Bonus: +{totalBonus:F0}%", UiHelper.AccentColor);
        player.SendMessage($" Maximum Bonus: +{maxBonus}%", UiHelper.DimColor);
    }

    private void RebirthInfoCommand(CommandArgs args)
    {
        var player = args.Player;
        var data = GetPlayerData(player);
        if (data is null) return;

        UiHelper.SendHeader(player, "REBIRTH INFO");

        if (data.RebirthCount > 0)
        {
            string title = data.RebirthCount >= 4 ? "Ascended" : $"Reborn {RomanFromScore(data.RebirthCount)}";
            player.SendMessage($" Current: {title}", UiHelper.GetRebirthColor(data.RebirthCount));
        }
        else
        {
            player.SendMessage(" Current: None", UiHelper.DimColor);
        }

        double rebirthBonus = data.RebirthCount * 0.05 * 100;
        player.SendMessage($" Permanent Bonus: +{rebirthBonus:F0}%", UiHelper.AccentColor);

        if (data.RebirthCount < config.MaxRebirthCount)
        {
            string nextTitle = (data.RebirthCount + 1) >= 4 ? "Ascended" : $"Reborn {RomanFromScore(data.RebirthCount + 1)}";
            player.SendMessage($" Next: {nextTitle}", UiHelper.DimColor);
            player.SendMessage($" Cycles Required: {config.RebirthCyclesRequired}", UiHelper.DimColor);
            player.SendMessage($" Progress: {data.PrestigeCycles} / {config.RebirthCyclesRequired} Cycles", UiHelper.AccentColor);
        }
        else
        {
            player.SendMessage(" Status: MAX REBIRTH", UiHelper.AccentColor);
        }
    }

    private void ProgressionCommand(CommandArgs args)
    {
        var player = args.Player;

        UiHelper.SendHeader(player, "PROGRESSION");
        player.SendMessage(" Class Levels", UiHelper.AccentColor);
        player.SendMessage("   Earn EXP from kills. Unlock rewards at milestones.", UiHelper.DimColor);
        player.SendMessage(" Prestige", UiHelper.GetPrestigeColor(2));
        player.SendMessage("   Cumulative EXP earns prestige ranks and bonuses.", UiHelper.DimColor);
        player.SendMessage(" Rebirth", UiHelper.GetRebirthColor(2));
        player.SendMessage("   Reset progress for permanent EXP bonuses.", UiHelper.DimColor);
        player.SendMessage(" Commands", UiHelper.AccentColor);
        player.SendMessage("   /level /classstats /prestige /rebirth /expbonus", UiHelper.DimColor);
    }

    private PlayerData? GetPlayerData(TSPlayer player)
    {
        if (player.Account == null)
        {
            player.SendErrorMessage("You must be logged in to use this command.");
            return null;
        }

        var data = playerManager.GetPlayer(player.Account.Name);
        if (data is null)
        {
            player.SendErrorMessage("Your progression data could not be found. Try re-logging.");
        }

        return data;
    }

    private void DisplayLeaderboard(TSPlayer player, LeaderboardCategory category, string title)
    {
        var (entries, error) = leaderboardManager.GetLeaderboard(category);

        if (error is not null)
        {
            player.SendErrorMessage(error);
            return;
        }

        if (entries is null || entries.Count == 0)
        {
            player.SendMessage($" {title}: No entries yet.", UiHelper.DimColor);
            return;
        }

        UiHelper.SendHeader(player, title);

        foreach (var entry in entries)
        {
            Color rankColor = UiHelper.GetRankColor(entry.Rank);
            string scoreDisplay = category switch
            {
                LeaderboardCategory.TopPrestige => $"Prestige {RomanFromScore(entry.Score)}",
                LeaderboardCategory.TopRebirth => entry.Score >= 4 ? "Ascended" : $"Reborn {RomanFromScore(entry.Score)}",
                _ => UiHelper.Fmt(entry.Score)
            };
            player.SendMessage($" #{entry.Rank,-3} {entry.PlayerName,-12} {scoreDisplay}", rankColor);
        }
    }

    private static string RomanFromScore(int score) => score switch
    {
        1 => "I", 2 => "II", 3 => "III", 4 => "IV", _ => score.ToString()
    };
}
