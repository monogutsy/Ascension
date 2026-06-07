namespace ClassPrestige.Models;
public sealed class BossFight
{
    public int NpcIndex { get; set; }
    public int NpcType { get; set; }
    public Dictionary<string, int> DamageByPlayer { get; set; } = [];
    public int TotalDamage { get; set; }
}
