using ClassPrestige.Config;
using ClassPrestige.Models;
using ClassPrestige.UI;

using TShockAPI;

using PlayerData = ClassPrestige.Models.PlayerData;

namespace ClassPrestige.Managers;
public sealed class PrestigeManager(PlayerManager playerManager, PluginConfig config)
{
    private static readonly string[] RomanNumerals = ["I", "II", "III", "IV"];
    public void EvaluatePrestige(PlayerData data, TSPlayer player)
    {
        int previousRank = data.PrestigeRank;

        int newRank = DerivePrestigeRank(data.PrestigeExp);

        if (newRank > previousRank)
        {
            data.PrestigeRank = newRank;
            data.IsDirty = true;

            string romanRank = ToRomanNumeral(newRank);
            double bonus = (newRank * 0.02 + data.RebirthCount * 0.05);
            double maxBonus = config.MaxEXPBonusPercent / 100.0;
            double totalBonus = Math.Min(bonus, maxBonus) * 100.0;

            UiHelper.SendHeader(player, "PRESTIGE UP");
            player.SendMessage(
                $" Prestige {romanRank} Achieved",
                UiHelper.GetPrestigeColor(newRank));
            player.SendMessage(
                $" Bonus: +{totalBonus:F0}% EXP",
                UiHelper.AccentColor);

            string baseName = StripPrestigePrefix(data.PlayerName);
            TShock.Utils.Broadcast(
                $"{baseName} has reached Prestige {romanRank}",
                UiHelper.HeaderColor);

            TShock.Log.ConsoleInfo($"[ClassPrestige] Prestige promotion: {baseName} -> Prestige {romanRank}");

            ApplyTitlePrefix(data, newRank);
        }

        if (data.PrestigeRank == 4)
        {
            data.PrestigeCycles = CalculatePrestigeCycles(data.PrestigeExp);
            data.IsDirty = true;
        }
    }
    public int DerivePrestigeRank(int prestigeExp)
    {
        var thresholds = config.PrestigeThresholds;

        return prestigeExp switch
        {
            _ when thresholds.Length >= 4 && prestigeExp >= thresholds[3] => 4,
            _ when thresholds.Length >= 3 && prestigeExp >= thresholds[2] => 3,
            _ when thresholds.Length >= 2 && prestigeExp >= thresholds[1] => 2,
            _ when thresholds.Length >= 1 && prestigeExp >= thresholds[0] => 1,
            _ => 0
        };
    }
    public int DeriveNextThreshold(int currentRank)
    {
        var thresholds = config.PrestigeThresholds;

        if (currentRank < 0 || currentRank >= 4 || currentRank >= thresholds.Length)
            return 0;

        return thresholds[currentRank];
    }
    public int CalculatePrestigeCycles(int prestigeExp)
    {
        var thresholds = config.PrestigeThresholds;

        if (thresholds.Length < 4)
            return 0;

        int prestigeIVThreshold = thresholds[3];

        if (prestigeExp < prestigeIVThreshold)
            return 0;

        int cycleExp = config.PrestigeCycleExp > 0 ? config.PrestigeCycleExp : 2_500_000;
        return (prestigeExp - prestigeIVThreshold) / cycleExp;
    }
    public static string ToRomanNumeral(int rank)
    {
        if (rank < 1 || rank > 4)
            return string.Empty;

        return RomanNumerals[rank - 1];
    }
    internal static void ApplyTitlePrefix(PlayerData data, int rank)
    {
        string romanRank = ToRomanNumeral(rank);
        string prefix = $"Prestige {romanRank}";

        string baseName = StripPrestigePrefix(data.PlayerName);
        data.PlayerName = $"{prefix} {baseName}";
    }
    internal static string StripPrestigePrefix(string name)
    {
        foreach (var numeral in RomanNumerals)
        {
            string prefix = $"Prestige {numeral} ";
            if (name.StartsWith(prefix, StringComparison.Ordinal))
            {
                return name[prefix.Length..];
            }

            string legacyPrefix = $"[Prestige {numeral}] ";
            if (name.StartsWith(legacyPrefix, StringComparison.Ordinal))
            {
                return name[legacyPrefix.Length..];
            }
        }

        return name;
    }
}
