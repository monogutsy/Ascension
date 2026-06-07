namespace ClassPrestige.Models;
public sealed class MilestoneReward
{
    public string Type { get; set; } = string.Empty;
    public int ItemId { get; set; }
    public int Quantity { get; set; }
    public string Title { get; set; } = string.Empty;
}
