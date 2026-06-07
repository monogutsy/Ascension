using ClassPrestige.Config;
using ClassPrestige.Interfaces;
using ClassPrestige.Models;

namespace ClassPrestige.Managers;
public sealed class LeaderboardManager(IDatabase database, PluginConfig config) : IDisposable
{
    private readonly IDatabase _database = database ?? throw new ArgumentNullException(nameof(database));
    private readonly PluginConfig _config = config ?? throw new ArgumentNullException(nameof(config));
    private Timer? _refreshTimer;
    private bool _disposed;

    private volatile IReadOnlyList<LeaderboardEntry>? _topLevels;
    private volatile IReadOnlyList<LeaderboardEntry>? _topPrestige;
    private volatile IReadOnlyList<LeaderboardEntry>? _topRebirth;
    public void Start()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        var intervalMs = _config.LeaderboardRefreshMinutes * 60 * 1000;
        _refreshTimer = new Timer(OnRefreshTimerElapsed, null, 0, intervalMs);
        TShockAPI.TShock.Log.ConsoleInfo($"[ClassPrestige] Leaderboard refresh started with interval of {_config.LeaderboardRefreshMinutes} minute(s).");
    }
    public void Stop()
    {
        if (_refreshTimer != null)
        {
            _refreshTimer.Change(Timeout.Infinite, Timeout.Infinite);
            _refreshTimer.Dispose();
            _refreshTimer = null;
            TShockAPI.TShock.Log.ConsoleInfo("[ClassPrestige] Leaderboard refresh stopped.");
        }
    }
    public (IReadOnlyList<LeaderboardEntry>? Entries, string? ErrorMessage) GetLeaderboard(LeaderboardCategory category)
    {
        if (!_config.EnableLeaderboards)
        {
            return (null, "Leaderboards are not available on this server.");
        }

        var cached = category switch
        {
            LeaderboardCategory.TopLevels => _topLevels,
            LeaderboardCategory.TopPrestige => _topPrestige,
            LeaderboardCategory.TopRebirth => _topRebirth,
            _ => null
        };

        if (cached is null)
        {
            return (null, "Leaderboard data is not yet available.");
        }

        var count = Math.Min(cached.Count, _config.LeaderboardTopCount);
        if (count == cached.Count)
        {
            return (cached, null);
        }

        return (cached.Take(count).ToList().AsReadOnly(), null);
    }
    private void OnRefreshTimerElapsed(object? state)
    {
        _ = RefreshAsync(CancellationToken.None);
    }
    internal async Task RefreshAsync(CancellationToken ct)
    {
        try
        {
            var topCount = _config.LeaderboardTopCount;

            var topLevelsTask = _database.QueryLeaderboardAsync(LeaderboardCategory.TopLevels, topCount, ct);
            var topPrestigeTask = _database.QueryLeaderboardAsync(LeaderboardCategory.TopPrestige, topCount, ct);
            var topRebirthTask = _database.QueryLeaderboardAsync(LeaderboardCategory.TopRebirth, topCount, ct);

            await Task.WhenAll(topLevelsTask, topPrestigeTask, topRebirthTask).ConfigureAwait(false);

            _topLevels = await topLevelsTask.ConfigureAwait(false);
            _topPrestige = await topPrestigeTask.ConfigureAwait(false);
            _topRebirth = await topRebirthTask.ConfigureAwait(false);

            TShockAPI.TShock.Log.ConsoleDebug("[ClassPrestige] Leaderboard cache refreshed successfully.");
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
        }
        catch (Exception ex)
        {
            TShockAPI.TShock.Log.ConsoleError($"[ClassPrestige] Leaderboard refresh failed: {ex.Message}");
        }
    }
    public void Dispose()
    {
        if (_disposed)
            return;

        Stop();
        _disposed = true;
    }
}
