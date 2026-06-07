using ClassPrestige.Managers;
using ClassPrestige.UI;

using Terraria;

using TerrariaApi.Server;

using TShockAPI;
using TShockAPI.Hooks;

namespace ClassPrestige.Hooks;
public sealed class SaveHooks(
    PlayerManager playerManager,
    AntiAbuseManager antiAbuseManager,
    RewardManager rewardManager)
{
    private DateTime _lastGameUpdateCheck = DateTime.MinValue;
    private static readonly TimeSpan GameUpdateInterval = TimeSpan.FromMilliseconds(500);
    public void OnNetGreetPlayer(GreetPlayerEventArgs args)
    {
        try
        {
            var player = TShock.Players[args.Who];
            if (player == null)
                return;

            if (player.Account == null)
            {
                TShock.Log.ConsoleDebug($"[ClassPrestige] OnNetGreetPlayer: Player {player.Name} not yet authenticated, deferring to PostLogin.");
                return;
            }

            var uuid = player.Account.Name;
            var playerName = player.Name;

            TShock.Log.ConsoleInfo($"[ClassPrestige] OnNetGreetPlayer: Player {playerName} already authenticated, loading data.");
            _ = LoadAndInitializeAsync(player, uuid, playerName);
        }
        catch (Exception ex)
        {
            TShock.Log.ConsoleError($"[ClassPrestige] Error in OnNetGreetPlayer: {ex.Message}");
        }
    }
    public void OnPlayerPostLogin(PlayerPostLoginEventArgs args)
    {
        try
        {
            var player = args.Player;
            if (player?.Account == null)
            {
                TShock.Log.ConsoleError("[ClassPrestige] OnPlayerPostLogin: args.Player or Account is null (unexpected).");
                return;
            }

            var uuid = player.Account.Name;
            var playerName = player.Name;

            var existing = playerManager.GetPlayer(uuid);
            if (existing != null)
            {
                TShock.Log.ConsoleDebug($"[ClassPrestige] OnPlayerPostLogin: Player {playerName} data already cached, skipping reload.");
                return;
            }

            TShock.Log.ConsoleInfo($"[ClassPrestige] OnPlayerPostLogin: Loading data for {playerName} ({uuid}).");
            _ = LoadAndInitializeAsync(player, uuid, playerName);
        }
        catch (Exception ex)
        {
            TShock.Log.ConsoleError($"[ClassPrestige] Error in OnPlayerPostLogin: {ex.Message}");
        }
    }
    private async Task LoadAndInitializeAsync(TSPlayer player, string uuid, string playerName)
    {
        try
        {
            await playerManager.LoadPlayerAsync(uuid, playerName).ConfigureAwait(false);

            float x = player.TPlayer.position.X;
            float y = player.TPlayer.position.Y;
            antiAbuseManager.OnPlayerJoin(uuid, x, y);

            rewardManager.RetryPendingRewards(player);

            var data = playerManager.GetPlayer(uuid);
            if (data != null && !data.TutorialShown)
            {
                data.TutorialShown = true;
                data.IsDirty = true;
                UiHelper.SendHeader(player, "CLASSPRESTIGE");
                player.SendMessage(" Welcome!", UiHelper.AccentColor);
                player.SendMessage(" Earn EXP by killing enemies.", UiHelper.DimColor);
                player.SendMessage(" Commands: /level /prestige /rebirth /progression", UiHelper.DimColor);
                player.SendMessage(" Type /progression for more info.", UiHelper.DimColor);
            }
        }
        catch (Exception ex)
        {
            TShock.Log.ConsoleError($"[ClassPrestige] Error in LoadAndInitializeAsync for {playerName}: {ex.Message}");
        }
    }
    public void OnServerLeave(LeaveEventArgs args)
    {
        try
        {
            var player = TShock.Players[args.Who];
            if (player?.Account == null)
                return;

            var uuid = player.Account.Name;

            TShock.Log.ConsoleDebug($"[ClassPrestige] OnServerLeave: Saving and evicting data for {player.Name} ({uuid}).");

            _ = playerManager.EvictPlayer(uuid);

            antiAbuseManager.OnPlayerLeave(uuid);
        }
        catch (Exception ex)
        {
            TShock.Log.ConsoleError($"[ClassPrestige] Error in OnServerLeave: {ex.Message}");
        }
    }
    public void OnWorldSave(WorldSaveEventArgs args)
    {
        try
        {
            _ = playerManager.SaveAllDirtyAsync();
        }
        catch (Exception ex)
        {
            TShock.Log.ConsoleError($"[ClassPrestige] Error in OnWorldSave: {ex.Message}");
        }
    }
    public void OnGameUpdate(EventArgs args)
    {
        try
        {
            var now = DateTime.UtcNow;
            if (now - _lastGameUpdateCheck < GameUpdateInterval)
                return;

            _lastGameUpdateCheck = now;

            for (int i = 0; i < TShock.Players.Length; i++)
            {
                var player = TShock.Players[i];
                if (player?.Active != true || player.Account == null)
                    continue;

                antiAbuseManager.UpdateActivity(player);
            }
        }
        catch (Exception ex)
        {
            TShock.Log.ConsoleError($"[ClassPrestige] Error in OnGameUpdate: {ex.Message}");
        }
    }
}
