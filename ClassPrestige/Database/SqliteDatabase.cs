using System.Text.Json;
using ClassPrestige.Interfaces;
using ClassPrestige.Models;
using Microsoft.Data.Sqlite;
using TShockAPI;

using PlayerData = ClassPrestige.Models.PlayerData;

namespace ClassPrestige.Database;
public sealed class SqliteDatabase(string dbPath) : IDatabase
{
    private readonly string _connectionString = $"Data Source={dbPath}";
    public async Task InitializeAsync(CancellationToken ct = default)
    {
        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(ct).ConfigureAwait(false);

        await using var command = connection.CreateCommand();
        command.CommandText = """
            CREATE TABLE IF NOT EXISTS PlayerProgress (
                UUID            TEXT PRIMARY KEY,
                PlayerName      TEXT NOT NULL,
                MeleeLevel      INTEGER DEFAULT 0,
                RangedLevel     INTEGER DEFAULT 0,
                MagicLevel      INTEGER DEFAULT 0,
                SummonerLevel   INTEGER DEFAULT 0,
                MeleeExp        INTEGER DEFAULT 0,
                RangedExp       INTEGER DEFAULT 0,
                MagicExp        INTEGER DEFAULT 0,
                SummonerExp     INTEGER DEFAULT 0,
                PrestigeRank    INTEGER DEFAULT 0,
                PrestigeExp     INTEGER DEFAULT 0,
                PrestigeCycles  INTEGER DEFAULT 0,
                RebirthCount    INTEGER DEFAULT 0,
                UnlockedTitles  TEXT DEFAULT '[]',
                UnlockedRewards TEXT DEFAULT '[]',
                LastLogin       TEXT,
                LastSave        TEXT
            );
            """;

        await command.ExecuteNonQueryAsync(ct).ConfigureAwait(false);

        try
        {
            await using var alterCmd = connection.CreateCommand();
            alterCmd.CommandText = "ALTER TABLE PlayerProgress ADD COLUMN ExpNotificationsEnabled INTEGER DEFAULT 1;";
            await alterCmd.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
        }
        catch (Microsoft.Data.Sqlite.SqliteException)
        {
        }

        try
        {
            await using var tutorialCmd = connection.CreateCommand();
            tutorialCmd.CommandText = "ALTER TABLE PlayerProgress ADD COLUMN TutorialShown INTEGER DEFAULT 0;";
            await tutorialCmd.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
        }
        catch (Microsoft.Data.Sqlite.SqliteException)
        {
        }
    }
    public async Task<PlayerData?> LoadAsync(string uuid, CancellationToken ct = default)
    {
        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(ct).ConfigureAwait(false);

        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT * FROM PlayerProgress WHERE UUID = @uuid;";
        command.Parameters.AddWithValue("@uuid", uuid);

        await using var reader = await command.ExecuteReaderAsync(ct).ConfigureAwait(false);
        if (!await reader.ReadAsync(ct).ConfigureAwait(false))
            return null;

        return MapReaderToPlayerData(reader);
    }
    public async Task SaveAsync(PlayerData data, CancellationToken ct = default)
    {
        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(ct).ConfigureAwait(false);

        await UpsertPlayerData(connection, data, transaction: null, ct).ConfigureAwait(false);
    }
    public async Task SaveBatchAsync(IEnumerable<PlayerData> data, CancellationToken ct = default)
    {
        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(ct).ConfigureAwait(false);

        await using var transaction = (SqliteTransaction)await connection.BeginTransactionAsync(ct).ConfigureAwait(false);

        try
        {
            foreach (var playerData in data)
            {
                await UpsertPlayerData(connection, playerData, transaction, ct).ConfigureAwait(false);
            }

            await transaction.CommitAsync(ct).ConfigureAwait(false);
        }
        catch
        {
            await transaction.RollbackAsync(ct).ConfigureAwait(false);
            throw;
        }
    }
    public async Task DeleteAsync(string uuid, CancellationToken ct = default)
    {
        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(ct).ConfigureAwait(false);

        await using var command = connection.CreateCommand();
        command.CommandText = "DELETE FROM PlayerProgress WHERE UUID = @uuid;";
        command.Parameters.AddWithValue("@uuid", uuid);

        await command.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
    }
    public async Task<IReadOnlyList<LeaderboardEntry>> QueryLeaderboardAsync(
        LeaderboardCategory category,
        int count,
        CancellationToken ct = default)
    {
        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(ct).ConfigureAwait(false);

        await using var command = connection.CreateCommand();
        command.CommandText = category switch
        {
            LeaderboardCategory.TopLevels =>
                """
                SELECT PlayerName, (MeleeLevel + RangedLevel + MagicLevel + SummonerLevel) AS Score
                FROM PlayerProgress
                ORDER BY Score DESC
                LIMIT @count;
                """,

            LeaderboardCategory.TopPrestige =>
                """
                SELECT PlayerName, PrestigeRank AS Score
                FROM PlayerProgress
                ORDER BY PrestigeRank DESC, PrestigeExp DESC
                LIMIT @count;
                """,

            LeaderboardCategory.TopRebirth =>
                """
                SELECT PlayerName, RebirthCount AS Score
                FROM PlayerProgress
                ORDER BY RebirthCount DESC
                LIMIT @count;
                """,

            _ => throw new ArgumentOutOfRangeException(nameof(category), category, "Unsupported leaderboard category.")
        };

        command.Parameters.AddWithValue("@count", count);

        List<LeaderboardEntry> results = [];
        await using var reader = await command.ExecuteReaderAsync(ct).ConfigureAwait(false);

        var position = 1;
        while (await reader.ReadAsync(ct).ConfigureAwait(false))
        {
            var playerName = reader.GetString(reader.GetOrdinal("PlayerName"));
            var score = reader.GetInt32(reader.GetOrdinal("Score"));

            results.Add(new LeaderboardEntry(playerName, score, position));
            position++;
        }

        return results;
    }
    private static async Task UpsertPlayerData(
        SqliteConnection connection,
        PlayerData data,
        SqliteTransaction? transaction,
        CancellationToken ct)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;

        command.CommandText = """
            INSERT INTO PlayerProgress (
                UUID, PlayerName, MeleeLevel, RangedLevel, MagicLevel, SummonerLevel,
                MeleeExp, RangedExp, MagicExp, SummonerExp,
                PrestigeRank, PrestigeExp, PrestigeCycles, RebirthCount,
                UnlockedTitles, UnlockedRewards, LastLogin, LastSave,
                ExpNotificationsEnabled, TutorialShown
            ) VALUES (
                @uuid, @playerName, @meleeLevel, @rangedLevel, @magicLevel, @summonerLevel,
                @meleeExp, @rangedExp, @magicExp, @summonerExp,
                @prestigeRank, @prestigeExp, @prestigeCycles, @rebirthCount,
                @unlockedTitles, @unlockedRewards, @lastLogin, @lastSave,
                @expNotificationsEnabled, @tutorialShown
            )
            ON CONFLICT(UUID) DO UPDATE SET
                PlayerName = excluded.PlayerName,
                MeleeLevel = excluded.MeleeLevel,
                RangedLevel = excluded.RangedLevel,
                MagicLevel = excluded.MagicLevel,
                SummonerLevel = excluded.SummonerLevel,
                MeleeExp = excluded.MeleeExp,
                RangedExp = excluded.RangedExp,
                MagicExp = excluded.MagicExp,
                SummonerExp = excluded.SummonerExp,
                PrestigeRank = excluded.PrestigeRank,
                PrestigeExp = excluded.PrestigeExp,
                PrestigeCycles = excluded.PrestigeCycles,
                RebirthCount = excluded.RebirthCount,
                UnlockedTitles = excluded.UnlockedTitles,
                UnlockedRewards = excluded.UnlockedRewards,
                LastLogin = excluded.LastLogin,
                LastSave = excluded.LastSave,
                ExpNotificationsEnabled = excluded.ExpNotificationsEnabled,
                TutorialShown = excluded.TutorialShown;
            """;

        command.Parameters.AddWithValue("@uuid", data.UUID);
        command.Parameters.AddWithValue("@playerName", data.PlayerName);
        command.Parameters.AddWithValue("@meleeLevel", data.MeleeLevel);
        command.Parameters.AddWithValue("@rangedLevel", data.RangedLevel);
        command.Parameters.AddWithValue("@magicLevel", data.MagicLevel);
        command.Parameters.AddWithValue("@summonerLevel", data.SummonerLevel);
        command.Parameters.AddWithValue("@meleeExp", data.MeleeExp);
        command.Parameters.AddWithValue("@rangedExp", data.RangedExp);
        command.Parameters.AddWithValue("@magicExp", data.MagicExp);
        command.Parameters.AddWithValue("@summonerExp", data.SummonerExp);
        command.Parameters.AddWithValue("@prestigeRank", data.PrestigeRank);
        command.Parameters.AddWithValue("@prestigeExp", data.PrestigeExp);
        command.Parameters.AddWithValue("@prestigeCycles", data.PrestigeCycles);
        command.Parameters.AddWithValue("@rebirthCount", data.RebirthCount);
        command.Parameters.AddWithValue("@unlockedTitles", SerializeList(data.UnlockedTitles));
        command.Parameters.AddWithValue("@unlockedRewards", SerializeList(data.UnlockedRewards));
        command.Parameters.AddWithValue("@lastLogin", data.LastLogin.ToString("o"));
        command.Parameters.AddWithValue("@lastSave", DateTime.UtcNow.ToString("o"));
        command.Parameters.AddWithValue("@expNotificationsEnabled", data.ExpNotificationsEnabled ? 1 : 0);
        command.Parameters.AddWithValue("@tutorialShown", data.TutorialShown ? 1 : 0);

        await command.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
    }
    private static PlayerData MapReaderToPlayerData(SqliteDataReader reader)
    {
        return new PlayerData
        {
            UUID = reader.GetString(reader.GetOrdinal("UUID")),
            PlayerName = reader.GetString(reader.GetOrdinal("PlayerName")),
            MeleeLevel = reader.GetInt32(reader.GetOrdinal("MeleeLevel")),
            RangedLevel = reader.GetInt32(reader.GetOrdinal("RangedLevel")),
            MagicLevel = reader.GetInt32(reader.GetOrdinal("MagicLevel")),
            SummonerLevel = reader.GetInt32(reader.GetOrdinal("SummonerLevel")),
            MeleeExp = reader.GetInt32(reader.GetOrdinal("MeleeExp")),
            RangedExp = reader.GetInt32(reader.GetOrdinal("RangedExp")),
            MagicExp = reader.GetInt32(reader.GetOrdinal("MagicExp")),
            SummonerExp = reader.GetInt32(reader.GetOrdinal("SummonerExp")),
            PrestigeRank = reader.GetInt32(reader.GetOrdinal("PrestigeRank")),
            PrestigeExp = reader.GetInt32(reader.GetOrdinal("PrestigeExp")),
            PrestigeCycles = reader.GetInt32(reader.GetOrdinal("PrestigeCycles")),
            RebirthCount = reader.GetInt32(reader.GetOrdinal("RebirthCount")),
            UnlockedTitles = DeserializeList(reader.GetString(reader.GetOrdinal("UnlockedTitles"))),
            UnlockedRewards = DeserializeList(reader.GetString(reader.GetOrdinal("UnlockedRewards"))),
            ExpNotificationsEnabled = ReadOptionalBool(reader, "ExpNotificationsEnabled", true),
            TutorialShown = ReadOptionalBool(reader, "TutorialShown", false),
            LastLogin = ParseDateTime(reader.GetString(reader.GetOrdinal("LastLogin"))),
            LastSave = ParseDateTime(reader.GetString(reader.GetOrdinal("LastSave")))
        };
    }
    private static bool ReadOptionalBool(SqliteDataReader reader, string columnName, bool defaultValue)
    {
        try
        {
            var ordinal = reader.GetOrdinal(columnName);
            return reader.IsDBNull(ordinal) ? defaultValue : reader.GetInt32(ordinal) != 0;
        }
        catch (ArgumentOutOfRangeException)
        {
            return defaultValue;
        }
    }
    private static string SerializeList(List<string> list)
    {
        return JsonSerializer.Serialize(list);
    }
    private static List<string> DeserializeList(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return [];

        try
        {
            return JsonSerializer.Deserialize<List<string>>(json) ?? [];
        }
        catch (JsonException ex)
        {
            TShock.Log.Warn($"[ClassPrestige] Invalid JSON in database field, treating as empty list: {ex.Message}");
            return [];
        }
    }
    private static DateTime ParseDateTime(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return DateTime.MinValue;

        if (DateTime.TryParse(value, null, System.Globalization.DateTimeStyles.RoundtripKind, out var result))
            return result;

        return DateTime.MinValue;
    }
}
