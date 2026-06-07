using Microsoft.Xna.Framework;

using ClassPrestige.Models;

using TShockAPI;

namespace ClassPrestige.UI;

public static class UiHelper
{
    public const string MeleeIcon = "[i:3507]";
    public const string RangedIcon = "[i:3019]";
    public const string MagicIcon = "[i:3541]";
    public const string SummonerIcon = "[i:3474]";
    public const string PrestigeIcon = "[i:3467]";
    public const string RebirthIcon = "[i:3457]";

    public static readonly Color MeleeColor = new(255, 80, 80);
    public static readonly Color RangedColor = new(80, 255, 80);
    public static readonly Color MagicColor = new(80, 200, 255);
    public static readonly Color SummonerColor = new(180, 80, 255);

    public static readonly Color PrestigeIColor = new(220, 200, 150);
    public static readonly Color PrestigeIIColor = new(255, 215, 0);
    public static readonly Color PrestigeIIIColor = new(255, 140, 0);
    public static readonly Color PrestigeIVColor = new(0, 255, 255);

    public static readonly Color RebirthIColor = new(80, 255, 80);
    public static readonly Color RebirthIIColor = new(80, 160, 255);
    public static readonly Color RebirthIIIColor = new(180, 80, 255);
    public static readonly Color AscendedColor = new(255, 215, 0);

    public static readonly Color HeaderColor = new(255, 215, 0);
    public static readonly Color TextColor = new(220, 220, 220);
    public static readonly Color AccentColor = new(255, 255, 100);
    public static readonly Color DimColor = new(160, 160, 160);

    public static readonly Color GoldColor = new(255, 215, 0);
    public static readonly Color SilverColor = new(192, 192, 192);
    public static readonly Color BronzeColor = new(205, 127, 50);

    public static void SendHeader(TSPlayer player, string title)
    {
        player.SendMessage("======================", HeaderColor);
        player.SendMessage($"      {title}", HeaderColor);
        player.SendMessage("======================", HeaderColor);
    }

    public static string GetClassIcon(ClassType ct) => ct switch
    {
        ClassType.Melee => MeleeIcon,
        ClassType.Ranged => RangedIcon,
        ClassType.Magic => MagicIcon,
        ClassType.Summoner => SummonerIcon,
        _ => ""
    };

    public static Color GetClassColor(ClassType ct) => ct switch
    {
        ClassType.Melee => MeleeColor,
        ClassType.Ranged => RangedColor,
        ClassType.Magic => MagicColor,
        ClassType.Summoner => SummonerColor,
        _ => Color.White
    };

    public static Color GetPrestigeColor(int rank) => rank switch
    {
        1 => PrestigeIColor,
        2 => PrestigeIIColor,
        3 => PrestigeIIIColor,
        4 => PrestigeIVColor,
        _ => TextColor
    };

    public static Color GetRebirthColor(int count) => count switch
    {
        1 => RebirthIColor,
        2 => RebirthIIColor,
        3 => RebirthIIIColor,
        >= 4 => AscendedColor,
        _ => TextColor
    };

    public static Color GetRankColor(int position) => position switch
    {
        1 => GoldColor,
        2 => SilverColor,
        3 => BronzeColor,
        _ => DimColor
    };

    public static string GetChatTitle(int rebirthCount, int prestigeRank)
    {
        if (rebirthCount >= 4) return "(Ascended)";
        if (rebirthCount > 0) return $"(Reborn {ToRoman(rebirthCount)})";
        if (prestigeRank > 0) return $"(Prestige {ToRoman(prestigeRank)})";
        return "";
    }

    public static Color GetTitleColor(int rebirthCount, int prestigeRank)
    {
        if (rebirthCount >= 4) return AscendedColor;
        if (rebirthCount > 0) return GetRebirthColor(rebirthCount);
        if (prestigeRank > 0) return GetPrestigeColor(prestigeRank);
        return TextColor;
    }

    public static string Fmt(int n) => n.ToString("N0");

    private static string ToRoman(int n) => n switch
    {
        1 => "I", 2 => "II", 3 => "III", 4 => "IV", _ => n.ToString()
    };
}
