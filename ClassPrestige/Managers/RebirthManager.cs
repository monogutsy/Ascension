using ClassPrestige.Config;
using ClassPrestige.Models;
using ClassPrestige.UI;

using TShockAPI;

using PlayerData = ClassPrestige.Models.PlayerData;

namespace ClassPrestige.Managers;
public sealed class RebirthManager(PlayerManager playerManager, PluginConfig config)
{
    private static readonly string[] RebirthTitles = ["Reborn I", "Reborn II", "Reborn III", "Ascended"];
    public (bool success, string message) TryRebirth(PlayerData data, TSPlayer player)
    {
        if (data.PrestigeCycles < config.RebirthCyclesRequired)
        {
            int remaining = config.RebirthCyclesRequired - data.PrestigeCycles;
            return (false, $"Cycles: {data.PrestigeCycles}/{config.RebirthCyclesRequired} - Need {remaining} more to rebirth.");
        }

        if (data.RebirthCount >= config.MaxRebirthCount)
        {
            return (false, "Maximum rebirths reached.");
        }

        ExecuteRebirth(data, player);

        return (true, $"Rebirth successful! You are now Rebirth {data.RebirthCount}.");
    }
    public static string GetRebirthTitle(int rebirthCount)
    {
        if (rebirthCount < 1 || rebirthCount > RebirthTitles.Length)
            return string.Empty;

        return RebirthTitles[rebirthCount - 1];
    }
    internal static string StripProgressionPrefix(string name)
    {
        foreach (var title in RebirthTitles)
        {
            string prefix = $"{title} ";
            if (name.StartsWith(prefix, StringComparison.Ordinal))
            {
                return name[prefix.Length..];
            }
        }

        string[] legacyRebirthTitles = ["[Reborn I]", "[Reborn II]", "[Reborn III]", "[Ascended]"];
        foreach (var title in legacyRebirthTitles)
        {
            string prefix = $"{title} ";
            if (name.StartsWith(prefix, StringComparison.Ordinal))
            {
                return name[prefix.Length..];
            }
        }

        string[] romanNumerals = ["I", "II", "III", "IV"];
        foreach (var numeral in romanNumerals)
        {
            string prefix = $"Prestige {numeral} ";
            if (name.StartsWith(prefix, StringComparison.Ordinal))
            {
                return name[prefix.Length..];
            }
        }

        foreach (var numeral in romanNumerals)
        {
            string prefix = $"[Prestige {numeral}] ";
            if (name.StartsWith(prefix, StringComparison.Ordinal))
            {
                return name[prefix.Length..];
            }
        }

        return name;
    }
    private void ExecuteRebirth(PlayerData data, TSPlayer player)
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

        data.RebirthCount++;

        string title = GetRebirthTitle(data.RebirthCount);

        if (!data.UnlockedTitles.Contains(title))
        {
            data.UnlockedTitles.Add(title);
        }

        string baseName = StripProgressionPrefix(data.PlayerName);
        data.PlayerName = $"{title} {baseName}";

        data.IsDirty = true;

        string roman = data.RebirthCount >= 4 ? "Ascended" : $"Reborn {ToRoman(data.RebirthCount)}";
        double rebirthBonus = data.RebirthCount * 0.05;
        double maxBonus = config.MaxEXPBonusPercent / 100.0;
        double totalBonus = Math.Min(rebirthBonus, maxBonus) * 100.0;

        UiHelper.SendHeader(player, "REBIRTH");
        player.SendMessage(
            $" {roman} Achieved",
            UiHelper.GetRebirthColor(data.RebirthCount));
        player.SendMessage(
            $" Permanent Bonus: +{totalBonus:F0}%",
            UiHelper.AccentColor);

        TShock.Utils.Broadcast(
            $"{baseName} has achieved {roman}",
            UiHelper.GetRebirthColor(data.RebirthCount));

        _ = playerManager.SavePlayerAsync(data.UUID);
    }

    private static string ToRoman(int n) => n switch
    {
        1 => "I", 2 => "II", 3 => "III", 4 => "IV", _ => n.ToString()
    };
}
