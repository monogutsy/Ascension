namespace ClassPrestige.Models;
public sealed class PendingReward
{
    public string PlayerUUID { get; set; } = string.Empty;
    public string RewardKey { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public int ItemId { get; set; }
    public int Quantity { get; set; }
    public DateTime QueuedAt { get; set; }
}
