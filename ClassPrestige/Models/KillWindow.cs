namespace ClassPrestige.Models;
public sealed class KillWindow
{
    public string PlayerUUID { get; set; } = string.Empty;
    public int NpcType { get; set; }
    public List<DateTime> KillTimes { get; set; } = [];
    public int GetCountWithinWindow(DateTime now, TimeSpan? windowDuration = null)
    {
        var duration = windowDuration ?? TimeSpan.FromSeconds(60);
        var cutoff = now - duration;

        KillTimes.RemoveAll(t => t < cutoff);

        return KillTimes.Count;
    }
}
