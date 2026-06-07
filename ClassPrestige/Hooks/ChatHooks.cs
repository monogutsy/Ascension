using ClassPrestige.Config;
using ClassPrestige.Managers;
using ClassPrestige.UI;

using Microsoft.Xna.Framework;

using TerrariaApi.Server;

using TShockAPI;

namespace ClassPrestige.Hooks;

public sealed class ChatHooks(PlayerManager playerManager, PluginConfig config)
{
    public void OnServerChat(ServerChatEventArgs args)
    {
        try
        {
            if (args.Handled)
                return;

            var text = args.Text;
            if (string.IsNullOrEmpty(text))
                return;

            string cmdSpec = TShock.Config.Settings.CommandSpecifier ?? "/";
            string silentSpec = TShock.Config.Settings.CommandSilentSpecifier ?? ".";
            if (text.StartsWith(cmdSpec, StringComparison.Ordinal) ||
                text.StartsWith(silentSpec, StringComparison.Ordinal) ||
                text.StartsWith('/') || text.StartsWith('.'))
                return;

            var player = TShock.Players[args.Who];
            if (player?.Account == null)
                return;

            var data = playerManager.GetPlayer(player.Account.Name);
            if (data == null)
                return;

            string title = UiHelper.GetChatTitle(data.RebirthCount, data.PrestigeRank);
            if (string.IsNullOrEmpty(title))
                return;

            args.Handled = true;

            Color titleColor = UiHelper.GetTitleColor(data.RebirthCount, data.PrestigeRank);
            string formatted = $"{title} {player.Name}: {args.Text}";

            TSPlayer.All.SendMessage(formatted, titleColor);
            TShock.Log.ConsoleInfo($"<{title} {player.Name}> {args.Text}");
        }
        catch (Exception ex)
        {
            TShock.Log.ConsoleError($"[ClassPrestige] Error in OnServerChat: {ex.Message}");
        }
    }
}
