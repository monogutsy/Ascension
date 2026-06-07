using ClassPrestige.Models;

namespace ClassPrestige.Interfaces;
public interface IDatabase
{
    Task InitializeAsync(CancellationToken ct = default);
    Task<PlayerData?> LoadAsync(string uuid, CancellationToken ct = default);
    Task SaveAsync(PlayerData data, CancellationToken ct = default);
    Task SaveBatchAsync(IEnumerable<PlayerData> data, CancellationToken ct = default);
    Task DeleteAsync(string uuid, CancellationToken ct = default);
    Task<IReadOnlyList<LeaderboardEntry>> QueryLeaderboardAsync(LeaderboardCategory category, int count, CancellationToken ct = default);
}
