using System.Collections.Concurrent;

using ClassPrestige.Config;
using ClassPrestige.Interfaces;
using ClassPrestige.Models;

using TShockAPI;

using PlayerData = ClassPrestige.Models.PlayerData;

namespace ClassPrestige.Managers;
public sealed class PlayerManager(IDatabase database, PluginConfig config) : IDisposable
{
    private readonly ConcurrentDictionary<string, PlayerData> _cache = new();
    private Timer? _autoSaveTimer;
    private bool _disposed;
    public PlayerData? GetPlayer(string uuid)
    {
        _cache.TryGetValue(uuid, out var data);
        return data;
    }
    public async Task<PlayerData> LoadPlayerAsync(string uuid, string playerName, CancellationToken ct = default)
    {
        try
        {
            var data = await database.LoadAsync(uuid, ct).ConfigureAwait(false);

            if (data == null)
            {
                data = new PlayerData
                {
                    UUID = uuid,
                    PlayerName = playerName,
                    LastLogin = DateTime.UtcNow,
                    ExpNotificationsEnabled = config.ExpNotificationDefaultState
                };
                TShock.Log.ConsoleInfo($"[ClassPrestige] Created new PlayerData for {playerName} ({uuid}).");
            }
            else
            {
                data.LastLogin = DateTime.UtcNow;
                data.PlayerName = playerName;
                TShock.Log.ConsoleInfo($"[ClassPrestige] Loaded PlayerData for {playerName} ({uuid}).");
            }

            int derivedRank = DerivePrestigeRankFromConfig(data.PrestigeExp);
            if (derivedRank != data.PrestigeRank)
            {
                TShock.Log.ConsoleInfo($"[ClassPrestige] Repairing prestige rank for {playerName}: {data.PrestigeRank} -> {derivedRank}");
                data.PrestigeRank = derivedRank;
                data.IsDirty = true;
            }

            _cache[uuid] = data;
            return data;
        }
        catch (Exception ex)
        {
            TShock.Log.ConsoleError($"[ClassPrestige] Failed to load PlayerData for {playerName} ({uuid}): {ex.Message}");

            var fallback = new PlayerData
            {
                UUID = uuid,
                PlayerName = playerName,
                LastLogin = DateTime.UtcNow,
                ExpNotificationsEnabled = config.ExpNotificationDefaultState
            };
            _cache[uuid] = fallback;
            return fallback;
        }
    }
    public async Task SavePlayerAsync(string uuid, CancellationToken ct = default)
    {
        if (!_cache.TryGetValue(uuid, out var data))
        {
            TShock.Log.ConsoleError($"[ClassPrestige] SavePlayerAsync called for unknown UUID: {uuid}");
            return;
        }

        try
        {
            data.LastSave = DateTime.UtcNow;
            await database.SaveAsync(data, ct).ConfigureAwait(false);
            data.IsDirty = false;
            TShock.Log.ConsoleDebug($"[ClassPrestige] Saved PlayerData for {data.PlayerName} ({uuid}).");
        }
        catch (Exception ex)
        {
            data.IsDirty = true;
            TShock.Log.ConsoleError($"[ClassPrestige] Failed to save PlayerData for {data.PlayerName} ({uuid}): {ex.Message}");
        }
    }
    public async Task EvictPlayer(string uuid, CancellationToken ct = default)
    {
        if (!_cache.TryGetValue(uuid, out var data))
        {
            return;
        }

        try
        {
            data.LastSave = DateTime.UtcNow;
            await database.SaveAsync(data, ct).ConfigureAwait(false);
            data.IsDirty = false;
            TShock.Log.ConsoleInfo($"[ClassPrestige] Saved and evicted PlayerData for {data.PlayerName} ({uuid}).");
        }
        catch (Exception ex)
        {
            TShock.Log.ConsoleError($"[ClassPrestige] Failed to save on eviction for {data.PlayerName} ({uuid}): {ex.Message}");
        }

        _cache.TryRemove(uuid, out _);
    }
    public void MarkDirty(string uuid)
    {
        if (_cache.TryGetValue(uuid, out var data))
        {
            data.IsDirty = true;
        }
    }
    public IEnumerable<PlayerData> GetAllCached()
    {
        return _cache.Values;
    }
    public IEnumerable<PlayerData> GetDirtyRecords()
    {
        return _cache.Values.Where(p => p.IsDirty);
    }

    public void StartAutoSave()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        var intervalMs = config.AutoSaveIntervalMinutes * 60 * 1000;
        _autoSaveTimer = new Timer(OnAutoSaveTimerElapsed, null, intervalMs, intervalMs);
        TShock.Log.ConsoleInfo($"[ClassPrestige] Auto-save started with interval of {config.AutoSaveIntervalMinutes} minute(s).");
    }
    public void StopAutoSave()
    {
        if (_autoSaveTimer != null)
        {
            _autoSaveTimer.Change(Timeout.Infinite, Timeout.Infinite);
            _autoSaveTimer.Dispose();
            _autoSaveTimer = null;
            TShock.Log.ConsoleInfo("[ClassPrestige] Auto-save stopped.");
        }
    }
    private void OnAutoSaveTimerElapsed(object? state)
    {
        _ = SaveAllDirtyAsync();
    }
    public async Task SaveAllDirtyAsync(CancellationToken ct = default)
    {
        var dirtyRecords = GetDirtyRecords().ToList();

        if (dirtyRecords.Count == 0)
            return;

        try
        {
            var now = DateTime.UtcNow;
            foreach (var record in dirtyRecords)
            {
                record.LastSave = now;
            }

            await database.SaveBatchAsync(dirtyRecords, ct).ConfigureAwait(false);

            foreach (var record in dirtyRecords)
            {
                record.IsDirty = false;
            }

            TShock.Log.ConsoleDebug($"[ClassPrestige] Auto-save completed: {dirtyRecords.Count} record(s) persisted.");
        }
        catch (Exception ex)
        {
            TShock.Log.ConsoleError($"[ClassPrestige] Batch auto-save failed ({dirtyRecords.Count} records): {ex.Message}");
        }
    }
    public void SaveAllOnShutdown()
    {
        var allRecords = GetAllCached().ToList();

        if (allRecords.Count == 0)
        {
            TShock.Log.ConsoleInfo("[ClassPrestige] Shutdown save: no records to persist.");
            return;
        }

        TShock.Log.ConsoleInfo($"[ClassPrestige] Shutdown save: persisting {allRecords.Count} record(s)...");

        try
        {
            var saveTask = Task.Run(async () =>
            {
                var now = DateTime.UtcNow;
                foreach (var record in allRecords)
                {
                    record.LastSave = now;
                }

                await database.SaveBatchAsync(allRecords).ConfigureAwait(false);

                foreach (var record in allRecords)
                {
                    record.IsDirty = false;
                }
            });

            if (!saveTask.Wait(TimeSpan.FromSeconds(30)))
            {
                TShock.Log.ConsoleError("[ClassPrestige] CRITICAL: Shutdown save timed out after 30 seconds. Some data may not have been persisted.");
            }
            else if (saveTask.IsFaulted)
            {
                var ex = saveTask.Exception?.InnerException ?? saveTask.Exception;
                TShock.Log.ConsoleError($"[ClassPrestige] Shutdown save failed: {ex?.Message}");
                foreach (var record in allRecords)
                {
                    record.IsDirty = true;
                }
            }
            else
            {
                TShock.Log.ConsoleInfo($"[ClassPrestige] Shutdown save completed: {allRecords.Count} record(s) persisted.");
            }
        }
        catch (AggregateException ae)
        {
            var inner = ae.InnerException ?? ae;
            TShock.Log.ConsoleError($"[ClassPrestige] Shutdown save failed: {inner.Message}");
            foreach (var record in allRecords)
            {
                record.IsDirty = true;
            }
        }
        catch (Exception ex)
        {
            TShock.Log.ConsoleError($"[ClassPrestige] Shutdown save failed: {ex.Message}");
            foreach (var record in allRecords)
            {
                record.IsDirty = true;
            }
        }
    }

    private int DerivePrestigeRankFromConfig(int prestigeExp)
    {
        var thresholds = config.PrestigeThresholds;
        int rank = 0;
        for (int i = 0; i < thresholds.Length && i < 4; i++)
        {
            if (prestigeExp >= thresholds[i])
                rank = i + 1;
            else
                break;
        }
        return rank;
    }

    public static int ExpRequired(int level)
    {
        return Math.Max(level * level * 100, 100);
    }
    public int ApplyExp(PlayerData data, ClassType classType, int amount)
    {
        int currentLevel = GetLevel(data, classType);
        int currentExp = GetExp(data, classType);
        int startingLevel = currentLevel;

        currentExp += amount;

        while (currentLevel < config.MaxLevel && currentExp >= ExpRequired(currentLevel))
        {
            currentExp -= ExpRequired(currentLevel);
            currentLevel++;
        }

        SetLevel(data, classType, currentLevel);
        SetExp(data, classType, currentExp);

        data.IsDirty = true;

        return currentLevel - startingLevel;
    }

    public static int GetLevel(PlayerData data, ClassType classType)
    {
        return classType switch
        {
            ClassType.Melee => data.MeleeLevel,
            ClassType.Ranged => data.RangedLevel,
            ClassType.Magic => data.MagicLevel,
            ClassType.Summoner => data.SummonerLevel,
            _ => 0
        };
    }
    public static void SetLevel(PlayerData data, ClassType classType, int level)
    {
        switch (classType)
        {
            case ClassType.Melee:
                data.MeleeLevel = level;
                break;
            case ClassType.Ranged:
                data.RangedLevel = level;
                break;
            case ClassType.Magic:
                data.MagicLevel = level;
                break;
            case ClassType.Summoner:
                data.SummonerLevel = level;
                break;
        }
    }
    public static int GetExp(PlayerData data, ClassType classType)
    {
        return classType switch
        {
            ClassType.Melee => data.MeleeExp,
            ClassType.Ranged => data.RangedExp,
            ClassType.Magic => data.MagicExp,
            ClassType.Summoner => data.SummonerExp,
            _ => 0
        };
    }
    public static void SetExp(PlayerData data, ClassType classType, int exp)
    {
        switch (classType)
        {
            case ClassType.Melee:
                data.MeleeExp = exp;
                break;
            case ClassType.Ranged:
                data.RangedExp = exp;
                break;
            case ClassType.Magic:
                data.MagicExp = exp;
                break;
            case ClassType.Summoner:
                data.SummonerExp = exp;
                break;
        }
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        StopAutoSave();
        _disposed = true;
    }
}
