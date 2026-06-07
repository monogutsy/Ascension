using System.Text.Json.Serialization;

using ClassPrestige.Models;

namespace ClassPrestige.Config;
public sealed class PluginConfig
{
    [JsonPropertyName("maxLevel")]
    public int MaxLevel { get; set; } = 100;
    [JsonPropertyName("commonMobExp")]
    public int CommonMobExp { get; set; } = 100;
    [JsonPropertyName("rareMobMinExp")]
    public int RareMobMinExp { get; set; } = 2500;
    [JsonPropertyName("rareMobMaxExp")]
    public int RareMobMaxExp { get; set; } = 5000;
    [JsonPropertyName("bossMinExp")]
    public int BossMinExp { get; set; } = 10000;
    [JsonPropertyName("bossMaxExp")]
    public int BossMaxExp { get; set; } = 25000;
    [JsonPropertyName("bossParticipationPercent")]
    public int BossParticipationPercent { get; set; } = 5;

    [JsonPropertyName("prestigeThresholds")]
    public int[] PrestigeThresholds { get; set; } = [500_000, 1_500_000, 4_000_000, 8_000_000];
    [JsonPropertyName("rebirthCyclesRequired")]
    public int RebirthCyclesRequired { get; set; } = 3;
    [JsonPropertyName("prestigeCycleExp")]
    public int PrestigeCycleExp { get; set; } = 2_500_000;
    [JsonPropertyName("maxEXPBonusPercent")]
    public int MaxEXPBonusPercent { get; set; } = 25;
    [JsonPropertyName("maxRebirthCount")]
    public int MaxRebirthCount { get; set; } = 4;

    [JsonPropertyName("afkTimeoutMinutes")]
    public int AFKTimeoutMinutes { get; set; } = 10;
    [JsonPropertyName("enableStatueProtection")]
    public bool EnableStatueProtection { get; set; } = true;
    [JsonPropertyName("enableSpawnFarmProtection")]
    public bool EnableSpawnFarmProtection { get; set; } = true;
    [JsonPropertyName("enableEventFarmingReduction")]
    public bool EnableEventFarmingReduction { get; set; } = true;
    [JsonPropertyName("eventExpMultiplier")]
    public double EventExpMultiplier { get; set; } = 0.5;
    [JsonPropertyName("rareMobIds")]
    public int[] RareMobIds { get; set; } = [195, 471, 473, 474, 475, 85];

    [JsonPropertyName("enableLeaderboards")]
    public bool EnableLeaderboards { get; set; } = true;
    [JsonPropertyName("leaderboardTopCount")]
    public int LeaderboardTopCount { get; set; } = 10;
    [JsonPropertyName("leaderboardRefreshMinutes")]
    public int LeaderboardRefreshMinutes { get; set; } = 5;

    [JsonPropertyName("databaseType")]
    public string DatabaseType { get; set; } = "sqlite";
    [JsonPropertyName("mySQLHost")]
    public string MySQLHost { get; set; } = "localhost";
    [JsonPropertyName("mySQLDatabase")]
    public string MySQLDatabase { get; set; } = "classprestige";
    [JsonPropertyName("mySQLUser")]
    public string MySQLUser { get; set; } = "root";
    [JsonPropertyName("mySQLPassword")]
    public string MySQLPassword { get; set; } = "";

    [JsonPropertyName("autoSaveIntervalMinutes")]
    public int AutoSaveIntervalMinutes { get; set; } = 5;

    [JsonPropertyName("milestones")]
    public Dictionary<int, MilestoneReward[]> Milestones { get; set; } = [];

    [JsonPropertyName("autoGrantPlayerPermission")]
    public bool AutoGrantPlayerPermission { get; set; } = true;

    [JsonPropertyName("enableExpNotifications")]
    public bool EnableExpNotifications { get; set; } = true;
    [JsonPropertyName("expNotificationDefaultState")]
    public bool ExpNotificationDefaultState { get; set; } = true;

    [JsonPropertyName("enableFancyUI")]
    public bool EnableFancyUI { get; set; } = true;
    [JsonPropertyName("enableItemIcons")]
    public bool EnableItemIcons { get; set; } = true;
}
