using ClassPrestige.Models;
using TShockAPI;
using Terraria;

namespace ClassPrestige.Interfaces;
public interface IKillValidator
{
    KillValidationResult Validate(TSPlayer player, NPC npc);
}
