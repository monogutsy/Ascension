using System.Text.Json.Serialization;

namespace ClassPrestige.Models;
public sealed class PlayerData
{
    public required string UUID { get; set; }
    public required string PlayerName { get; set; }

    public int MeleeLevel { get; set; }
    public int RangedLevel { get; set; }
    public int MagicLevel { get; set; }
    public int SummonerLevel { get; set; }

    public int MeleeExp { get; set; }
    public int RangedExp { get; set; }
    public int MagicExp { get; set; }
    public int SummonerExp { get; set; }

    public int PrestigeRank { get; set; }
    public int PrestigeExp { get; set; }
    public int PrestigeCycles { get; set; }

    public int RebirthCount { get; set; }

    public List<string> UnlockedTitles { get; set; } = [];
    public List<string> UnlockedRewards { get; set; } = [];

    public bool ExpNotificationsEnabled { get; set; } = true;
    public bool TutorialShown { get; set; }

    public DateTime LastLogin { get; set; }
    public DateTime LastSave { get; set; }

    [JsonIgnore]
    public bool IsDirty { get; set; }
    [JsonIgnore]
    public DateTime LastActivity { get; set; }
    [JsonIgnore]
    public float LastX { get; set; }
    [JsonIgnore]
    public float LastY { get; set; }
    [JsonIgnore]
    public bool AFKNotified { get; set; }
}
