using System.Text.Json;
using ClassPrestige.Interfaces;
using ClassPrestige.Models;
using MySqlConnector;

namespace ClassPrestige.Database;
public sealed class MysqlDatabase(string host, string database, string user, string password) : IDatabase
{
    private readonly string _connectionString = new MySqlConnectionStringBuilder
    {
        Server = host,
        Database = database,
        UserID = user,
        Password = password,
        SslMode = MySqlSslMode.Preferred
    }.ConnectionString;
    public async Task InitializeAsync(CancellationToken ct = default)
    {
        await using var connection = new MySqlConnection(_connectionString);
        await connection.OpenAsync(ct).ConfigureAwait(false);

        await using var cmd = connection.CreateCommand();
        cmd.CommandText = """
            CREATE TABLE IF NOT EXISTS PlayerProgress (
                UUID            VARCHAR(36) PRIMARY KEY,
                PlayerName      VARCHAR(255) NOT NULL,
                MeleeLevel      INT DEFAULT 0,
                RangedLevel     INT DEFAULT 0,
                MagicLevel      INT DEFAULT 0,
                SummonerLevel   INT DEFAULT 0,
                MeleeExp        INT DEFAULT 0,
                RangedExp       INT DEFAULT 0,
                MagicExp        INT DEFAULT 0,
                SummonerExp     INT DEFAULT 0,
                PrestigeRank    INT DEFAULT 0,
                PrestigeExp     INT DEFAULT 0,
                PrestigeCycles  INT DEFAULT 0,
                RebirthCount    INT DEFAULT 0,
                UnlockedTitles  TEXT DEFAULT ('[]'),
                UnlockedRewards TEXT DEFAULT ('[]'),
                LastLogin       TEXT,
                LastSave        TEXT
            );
            """;
        await cmd.ExecuteNonQueryAsync(ct).ConfigureAwait(false);

        try
        {
            await using var alterCmd = connection.CreateCommand();
            alterCmd.CommandText = "ALTER TABLE PlayerProgress ADD COLUMN ExpNotificationsEnabled INT DEFAULT 1;";
            await alterCmd.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
        }
        catch (MySqlConnector.MySqlException)
        {
        }

        try
        {
            await using var tutorialCmd = connection.CreateCommand();
            tutorialCmd.CommandText = "ALTER TABLE PlayerProgress ADD COLUMN TutorialShown INT DEFAULT 0;";
            await tutorialCmd.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
        }
        catch (MySqlConnector.MySqlException)
        {
        }
    }
    public async Task<PlayerData?> LoadAsync(string uuid, CancellationToken ct = default)
    {
        await using var connection = new MySqlConnection(_connectionString);
        await connection.OpenAsync(ct).ConfigureAwait(false);

        await using var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT * FROM PlayerProgress WHERE UUID = @uuid;";
        cmd.Parameters.AddWithValue("@uuid", uuid);

        await using var reader = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
        if (!await reader.ReadAsync(ct).ConfigureAwait(false))
            return null;

        return ReadPlayerData(reader);
    }
    public async Task SaveAsync(PlayerData data, CancellationToken ct = default)
    {
        await using var connection = new MySqlConnection(_connectionString);
        await connection.OpenAsync(ct).ConfigureAwait(false);

        await UpsertPlayerAsync(connection, data, transaction: null, ct).ConfigureAwait(false);
    }
    public async Task SaveBatchAsync(IEnumerable<PlayerData> data, CancellationToken ct = default)
    {
        await using var connection = new MySqlConnection(_connectionString);
        await connection.OpenAsync(ct).ConfigureAwait(false);

        await using var transaction = await connection.BeginTransactionAsync(ct).ConfigureAwait(false);
        try
        {
            foreach (var player in data)
            {
                await UpsertPlayerAsync(connection, player, transaction, ct).ConfigureAwait(false);
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
        await using var connection = new MySqlConnection(_connectionString);
        await connection.OpenAsync(ct).ConfigureAwait(false);

        await using var cmd = connection.CreateCommand();
        cmd.CommandText = "DELETE FROM PlayerProgress WHERE UUID = @uuid;";
        cmd.Parameters.AddWithValue("@uuid", uuid);

        await cmd.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
    }
    public async Task<IReadOnlyList<LeaderboardEntry>> QueryLeaderboardAsync(
        LeaderboardCategory category,
        int count,
        CancellationToken ct = default)
    {
        await using var connection = new MySqlConnection(_connectionString);
        await connection.OpenAsync(ct).ConfigureAwait(false);

        var orderBy = category switch
        {
            LeaderboardCategory.TopLevels => "(MeleeLevel + RangedLevel + MagicLevel + SummonerLevel) DESC",
            LeaderboardCategory.TopPrestige => "PrestigeRank DESC, PrestigeExp DESC",
            LeaderboardCategory.TopRebirth => "RebirthCount DESC",
            _ => throw new ArgumentOutOfRangeException(nameof(category))
        };

        var valueExpression = category switch
        {
            LeaderboardCategory.TopLevels => "(MeleeLevel + RangedLevel + MagicLevel + SummonerLevel)",
            LeaderboardCategory.TopPrestige => "PrestigeRank",
            LeaderboardCategory.TopRebirth => "RebirthCount",
            _ => throw new ArgumentOutOfRangeException(nameof(category))
        };

        await using var cmd = connection.CreateCommand();
        cmd.CommandText = $"""
            SELECT PlayerName, {valueExpression} AS RankValue
            FROM PlayerProgress
            ORDER BY {orderBy}
            LIMIT @count;
            """;
        cmd.Parameters.AddWithValue("@count", count);

        List<LeaderboardEntry> results = [];
        await using var reader = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
        var position = 1;
        while (await reader.ReadAsync(ct).ConfigureAwait(false))
        {
            results.Add(new LeaderboardEntry(
                PlayerName: reader.GetString("PlayerName"),
                Score: reader.GetInt32("RankValue"),
                Rank: position++));
        }

        return results;
    }
    private static async Task UpsertPlayerAsync(
        MySqlConnection connection,
        PlayerData data,
        MySqlTransaction? transaction,
        CancellationToken ct)
    {
        await using var cmd = connection.CreateCommand();
        if (transaction is not null)
            cmd.Transaction = transaction;

        cmd.CommandText = """
            INSERT INTO PlayerProgress (
                UUID, PlayerName,
                MeleeLevel, RangedLevel, MagicLevel, SummonerLevel,
                MeleeExp, RangedExp, MagicExp, SummonerExp,
                PrestigeRank, PrestigeExp, PrestigeCycles,
                RebirthCount, UnlockedTitles, UnlockedRewards,
                LastLogin, LastSave, ExpNotificationsEnabled, TutorialShown
            ) VALUES (
                @uuid, @playerName,
                @meleeLevel, @rangedLevel, @magicLevel, @summonerLevel,
                @meleeExp, @rangedExp, @magicExp, @summonerExp,
                @prestigeRank, @prestigeExp, @prestigeCycles,
                @rebirthCount, @unlockedTitles, @unlockedRewards,
                @lastLogin, @lastSave, @expNotificationsEnabled, @tutorialShown
            )
            ON DUPLICATE KEY UPDATE
                PlayerName = VALUES(PlayerName),
                MeleeLevel = VALUES(MeleeLevel),
                RangedLevel = VALUES(RangedLevel),
                MagicLevel = VALUES(MagicLevel),
                SummonerLevel = VALUES(SummonerLevel),
                MeleeExp = VALUES(MeleeExp),
                RangedExp = VALUES(RangedExp),
                MagicExp = VALUES(MagicExp),
                SummonerExp = VALUES(SummonerExp),
                PrestigeRank = VALUES(PrestigeRank),
                PrestigeExp = VALUES(PrestigeExp),
                PrestigeCycles = VALUES(PrestigeCycles),
                RebirthCount = VALUES(RebirthCount),
                UnlockedTitles = VALUES(UnlockedTitles),
                UnlockedRewards = VALUES(UnlockedRewards),
                LastLogin = VALUES(LastLogin),
                LastSave = VALUES(LastSave),
                ExpNotificationsEnabled = VALUES(ExpNotificationsEnabled),
                TutorialShown = VALUES(TutorialShown);
            """;

        cmd.Parameters.AddWithValue("@uuid", data.UUID);
        cmd.Parameters.AddWithValue("@playerName", data.PlayerName);
        cmd.Parameters.AddWithValue("@meleeLevel", data.MeleeLevel);
        cmd.Parameters.AddWithValue("@rangedLevel", data.RangedLevel);
        cmd.Parameters.AddWithValue("@magicLevel", data.MagicLevel);
        cmd.Parameters.AddWithValue("@summonerLevel", data.SummonerLevel);
        cmd.Parameters.AddWithValue("@meleeExp", data.MeleeExp);
        cmd.Parameters.AddWithValue("@rangedExp", data.RangedExp);
        cmd.Parameters.AddWithValue("@magicExp", data.MagicExp);
        cmd.Parameters.AddWithValue("@summonerExp", data.SummonerExp);
        cmd.Parameters.AddWithValue("@prestigeRank", data.PrestigeRank);
        cmd.Parameters.AddWithValue("@prestigeExp", data.PrestigeExp);
        cmd.Parameters.AddWithValue("@prestigeCycles", data.PrestigeCycles);
        cmd.Parameters.AddWithValue("@rebirthCount", data.RebirthCount);
        cmd.Parameters.AddWithValue("@unlockedTitles", SerializeList(data.UnlockedTitles));
        cmd.Parameters.AddWithValue("@unlockedRewards", SerializeList(data.UnlockedRewards));
        cmd.Parameters.AddWithValue("@lastLogin", data.LastLogin.ToString("o"));
        cmd.Parameters.AddWithValue("@lastSave", data.LastSave.ToString("o"));
        cmd.Parameters.AddWithValue("@expNotificationsEnabled", data.ExpNotificationsEnabled ? 1 : 0);
        cmd.Parameters.AddWithValue("@tutorialShown", data.TutorialShown ? 1 : 0);

        await cmd.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
    }
    private static PlayerData ReadPlayerData(MySqlDataReader reader)
    {
        return new PlayerData
        {
            UUID = reader.GetString("UUID"),
            PlayerName = reader.GetString("PlayerName"),
            MeleeLevel = reader.GetInt32("MeleeLevel"),
            RangedLevel = reader.GetInt32("RangedLevel"),
            MagicLevel = reader.GetInt32("MagicLevel"),
            SummonerLevel = reader.GetInt32("SummonerLevel"),
            MeleeExp = reader.GetInt32("MeleeExp"),
            RangedExp = reader.GetInt32("RangedExp"),
            MagicExp = reader.GetInt32("MagicExp"),
            SummonerExp = reader.GetInt32("SummonerExp"),
            PrestigeRank = reader.GetInt32("PrestigeRank"),
            PrestigeExp = reader.GetInt32("PrestigeExp"),
            PrestigeCycles = reader.GetInt32("PrestigeCycles"),
            RebirthCount = reader.GetInt32("RebirthCount"),
            UnlockedTitles = DeserializeList(reader.GetString("UnlockedTitles")),
            UnlockedRewards = DeserializeList(reader.GetString("UnlockedRewards")),
            ExpNotificationsEnabled = ReadOptionalBool(reader, "ExpNotificationsEnabled", true),
            TutorialShown = ReadOptionalBool(reader, "TutorialShown", false),
            LastLogin = DateTime.TryParse(reader.GetString("LastLogin"), out var login) ? login : DateTime.MinValue,
            LastSave = DateTime.TryParse(reader.GetString("LastSave"), out var save) ? save : DateTime.MinValue
        };
    }
    private static bool ReadOptionalBool(MySqlDataReader reader, string columnName, bool defaultValue)
    {
        try
        {
            var ordinal = reader.GetOrdinal(columnName);
            return reader.IsDBNull(ordinal) ? defaultValue : reader.GetInt32(ordinal) != 0;
        }
        catch (IndexOutOfRangeException)
        {
            return defaultValue;
        }
    }
    private static string SerializeList(List<string> list)
    {
        return JsonSerializer.Serialize(list);
    }
    private static List<string> DeserializeList(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return [];

        try
        {
            return JsonSerializer.Deserialize<List<string>>(json) ?? [];
        }
        catch (JsonException)
        {
            return [];
        }
    }
}
