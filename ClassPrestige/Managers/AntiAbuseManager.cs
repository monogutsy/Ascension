using System.Collections.Concurrent;
using ClassPrestige.Config;
using ClassPrestige.Interfaces;
using ClassPrestige.Models;
using Terraria;
using TShockAPI;

namespace ClassPrestige.Managers;
public sealed class AntiAbuseManager(PluginConfig config) : IKillValidator
{
    private readonly PluginConfig _config = config ?? throw new ArgumentNullException(nameof(config));

    private readonly ConcurrentDictionary<string, DateTime> _lastActivity = new();
    private readonly ConcurrentDictionary<string, (float X, float Y)> _lastPosition = new();
    private readonly ConcurrentDictionary<string, bool> _afkNotified = new();

    private readonly ConcurrentDictionary<string, ConcurrentDictionary<int, (int Count, DateTime WindowStart)>> _killWindows = new();
    private const float MinMovementTiles = 2.0f;

    public KillValidationResult Validate(TSPlayer player, NPC npc)
    {
        if (player.Account == null)
            return new KillValidationResult(false, 0.0, "Player not authenticated");

        var uuid = player.Account.Name;

        if (IsPlayerAFK(uuid))
        {
            return new KillValidationResult(false, 0.0, "Player is AFK");
        }

        if (_config.EnableStatueProtection && npc.SpawnedFromStatue)
        {
            return new KillValidationResult(false, 0.0, "NPC was spawned from a statue");
        }

        double multiplier = GetDiminishingMultiplier(uuid, npc.type);

        return new KillValidationResult(true, multiplier, null);
    }

    public void RecordActivity(string uuid)
    {
        bool wasNotified = _afkNotified.TryGetValue(uuid, out var notified) && notified;
        _lastActivity[uuid] = DateTime.UtcNow;
        _afkNotified[uuid] = false;

        if (wasNotified)
        {
            TShock.Log.ConsoleDebug($"[ClassPrestige] Player {uuid} is no longer AFK.");
        }
    }
    public bool UpdatePosition(string uuid, float x, float y)
    {
        if (_lastPosition.TryGetValue(uuid, out var lastPos))
        {
            float dx = x - lastPos.X;
            float dy = y - lastPos.Y;
            float distanceSquared = dx * dx + dy * dy;

            float thresholdPixels = MinMovementTiles * 16f;
            if (distanceSquared >= thresholdPixels * thresholdPixels)
            {
                _lastPosition[uuid] = (x, y);
                RecordActivity(uuid);
                return true;
            }
        }
        else
        {
            _lastPosition[uuid] = (x, y);
            RecordActivity(uuid);
            return true;
        }

        return false;
    }
    public bool IsPlayerAFK(string uuid)
    {
        if (_config.AFKTimeoutMinutes <= 0)
            return false;

        if (!_lastActivity.TryGetValue(uuid, out var lastAction))
        {
            return false;
        }

        return (DateTime.UtcNow - lastAction).TotalMinutes >= _config.AFKTimeoutMinutes;
    }
    public void OnPlayerJoin(string uuid, float x, float y)
    {
        RecordActivity(uuid);
        _lastPosition[uuid] = (x, y);
        _afkNotified[uuid] = false;
    }
    public void OnPlayerLeave(string uuid)
    {
        _lastActivity.TryRemove(uuid, out _);
        _lastPosition.TryRemove(uuid, out _);
        _afkNotified.TryRemove(uuid, out _);
        _killWindows.TryRemove(uuid, out _);
    }
    public void UpdateActivity(TSPlayer player)
    {
        if (player == null || !player.Active || player.Account == null)
            return;

        var uuid = player.Account.Name;
        float currentX = player.TPlayer.position.X;
        float currentY = player.TPlayer.position.Y;

        bool moved = UpdatePosition(uuid, currentX, currentY);

        if (!moved)
        {
            CheckAFKTransition(player, uuid);
        }
    }
    public void RecordCombatAction(TSPlayer player)
    {
        if (player == null || !player.Active || player.Account == null)
            return;

        var uuid = player.Account.Name;
        RecordActivity(uuid);

        float currentX = player.TPlayer.position.X;
        float currentY = player.TPlayer.position.Y;
        _lastPosition[uuid] = (currentX, currentY);
    }
    private void CheckAFKTransition(TSPlayer player, string uuid)
    {
        if (_config.AFKTimeoutMinutes <= 0)
            return;

        if (_afkNotified.TryGetValue(uuid, out var notified) && notified)
            return;

        if (!_lastActivity.TryGetValue(uuid, out var lastAction))
            return;

        if ((DateTime.UtcNow - lastAction).TotalMinutes >= _config.AFKTimeoutMinutes)
        {
            _afkNotified[uuid] = true;
            player.SendInfoMessage("[ClassPrestige] You are now AFK. EXP gains are paused until you move or engage in combat.");
            TShock.Log.ConsoleInfo($"[ClassPrestige] Player {player.Name} is now AFK (inactive for {_config.AFKTimeoutMinutes} min).");
        }
    }

    public double GetDiminishingMultiplier(string uuid, int npcTypeId)
    {
        if (!_config.EnableSpawnFarmProtection)
            return 1.0;

        var now = DateTime.UtcNow;

        var playerWindows = _killWindows.GetOrAdd(uuid, _ => new ConcurrentDictionary<int, (int Count, DateTime WindowStart)>());

        var updated = playerWindows.AddOrUpdate(
            npcTypeId,
            _ => (Count: 1, WindowStart: now),
            (_, existing) =>
            {
                if (existing.WindowStart.AddSeconds(60) < now)
                {
                    return (Count: 1, WindowStart: now);
                }

                return (Count: existing.Count + 1, WindowStart: existing.WindowStart);
            });

        return updated.Count switch
        {
            1 => 1.0,
            2 => 0.75,
            _ => 0.50
        };
    }
    public int GetCurrentKillCount(string uuid, int npcTypeId)
    {
        if (!_killWindows.TryGetValue(uuid, out var playerWindows))
            return 0;

        if (!playerWindows.TryGetValue(npcTypeId, out var window))
            return 0;

        if (window.WindowStart.AddSeconds(60) < DateTime.UtcNow)
            return 0;

        return window.Count;
    }

    public double GetEventMultiplier()
    {
        if (!_config.EnableEventFarmingReduction)
            return 1.0;

        if (IsWaveEventActive())
            return _config.EventExpMultiplier;

        return 1.0;
    }
    private static bool IsWaveEventActive()
    {
        if (Main.invasionType is 3 or 4)
            return true;

        if (Main.pumpkinMoon)
            return true;

        if (Main.snowMoon)
            return true;

        return false;
    }
}
