using ClassPrestige.Commands;
using ClassPrestige.Config;
using ClassPrestige.Database;
using ClassPrestige.Hooks;
using ClassPrestige.Interfaces;
using ClassPrestige.Managers;

using Terraria;

using TerrariaApi.Server;

using TShockAPI;
using TShockAPI.Hooks;

namespace ClassPrestige;
[ApiVersion(2, 1)]
public sealed class ClassPrestigePlugin(Main game) : TerrariaPlugin(game)
{
    public override string Name => "ClassPrestige";
    public override string Author => "ClassPrestige Team";
    public override string Description => "Per-class EXP, prestige ranks, rebirth, anti-abuse, leaderboards, and milestone rewards.";
    public override Version Version => new(1, 0, 0);

    private readonly CancellationTokenSource _cancellationTokenSource = new();

    private ConfigManager? _configManager;
    private IDatabase? _database;
    private PlayerManager? _playerManager;
    private AntiAbuseManager? _antiAbuseManager;
    private ExpManager? _expManager;
    private PrestigeManager? _prestigeManager;
    private RebirthManager? _rebirthManager;
    private RewardManager? _rewardManager;
    private LeaderboardManager? _leaderboardManager;

    private KillHooks? _killHooks;
    private SaveHooks? _saveHooks;
    private ChatHooks? _chatHooks;

    private PlayerCommands? _playerCommands;
    private AdminCommands? _adminCommands;
    public override void Initialize()
    {
        _ = InitializeAsync();
    }
    private async Task InitializeAsync()
    {
        try
        {
            var ct = _cancellationTokenSource.Token;

            var configPath = Path.Combine(TShock.SavePath, "ClassPrestige", "config.json");
            _configManager = new ConfigManager(configPath);
            await _configManager.LoadAsync(ct).ConfigureAwait(false);

            var config = _configManager.Current;

            _database = CreateDatabase(config);
            await _database.InitializeAsync(ct).ConfigureAwait(false);

            _playerManager = new PlayerManager(_database, config);

            _antiAbuseManager = new AntiAbuseManager(config);

            _prestigeManager = new PrestigeManager(_playerManager, config);

            _expManager = new ExpManager(_playerManager, _antiAbuseManager, config, _prestigeManager);

            _rebirthManager = new RebirthManager(_playerManager, config);

            _rewardManager = new RewardManager(_playerManager, config);

            _leaderboardManager = new LeaderboardManager(_database, config);

            _killHooks = new KillHooks(_expManager, _antiAbuseManager, _playerManager);
            _saveHooks = new SaveHooks(_playerManager, _antiAbuseManager, _rewardManager);
            _chatHooks = new ChatHooks(_playerManager, config);

            _playerCommands = new PlayerCommands(_playerManager, _prestigeManager, _rebirthManager, _leaderboardManager, config);
            _adminCommands = new AdminCommands(_playerManager, _configManager, _database, _prestigeManager);

            ServerApi.Hooks.NpcKilled.Register(this, _killHooks.OnNpcKilled);
            ServerApi.Hooks.NpcStrike.Register(this, _killHooks.OnNpcStrike);
            ServerApi.Hooks.NetGreetPlayer.Register(this, _saveHooks.OnNetGreetPlayer);
            ServerApi.Hooks.ServerLeave.Register(this, _saveHooks.OnServerLeave);
            ServerApi.Hooks.GameUpdate.Register(this, _saveHooks.OnGameUpdate);
            ServerApi.Hooks.ServerChat.Register(this, _chatHooks.OnServerChat);

            ServerApi.Hooks.WorldSave.Register(this, _saveHooks.OnWorldSave);

            TShockAPI.Hooks.PlayerHooks.PlayerPostLogin += _saveHooks.OnPlayerPostLogin;

            _playerCommands.Register();
            _adminCommands.Register();
            TShock.Log.ConsoleInfo("[ClassPrestige] Registered 11 player commands and 5 admin commands.");

            EnsurePlayerPermissions(config);

            _playerManager.StartAutoSave();
            _leaderboardManager.Start();

            TShock.Log.ConsoleInfo("[ClassPrestige] Plugin initialized successfully.");
        }
        catch (Exception ex)
        {
            TShock.Log.ConsoleError($"[ClassPrestige] Failed to initialize plugin: {ex.Message}");
            TShock.Log.ConsoleError($"[ClassPrestige] Stack trace: {ex.StackTrace}");
        }
    }
    private static IDatabase CreateDatabase(PluginConfig config)
    {
        return config.DatabaseType.ToLowerInvariant() switch
        {
            "mysql" => new MysqlDatabase(
                config.MySQLHost,
                config.MySQLDatabase,
                config.MySQLUser,
                config.MySQLPassword),
            _ => new SqliteDatabase(
                Path.Combine(TShock.SavePath, "ClassPrestige", "classprestige.sqlite"))
        };
    }
    private static void EnsurePlayerPermissions(PluginConfig config)
    {
        try
        {
            var defaultGroup = TShock.Groups.GetGroupByName(TShock.Config.Settings.DefaultRegistrationGroupName);
            if (defaultGroup == null)
            {
                TShock.Log.ConsoleWarn("[ClassPrestige] Could not find the default registration group. Player permissions not verified.");
                return;
            }

            if (defaultGroup.HasPermission(Permissions.Player))
            {
                TShock.Log.ConsoleDebug($"[ClassPrestige] Default group '{defaultGroup.Name}' already has '{Permissions.Player}' permission.");
                return;
            }

            if (config.AutoGrantPlayerPermission)
            {
                TShock.Groups.AddPermissions(defaultGroup.Name, new List<string> { Permissions.Player });
                TShock.Log.ConsoleInfo($"[ClassPrestige] Automatically granted '{Permissions.Player}' permission to the '{defaultGroup.Name}' group.");
            }
            else
            {
                TShock.Log.ConsoleWarn($"[ClassPrestige] WARNING: The '{defaultGroup.Name}' group does not have the '{Permissions.Player}' permission.");
                TShock.Log.ConsoleWarn($"[ClassPrestige] Player commands will not be available to normal players.");
                TShock.Log.ConsoleWarn($"[ClassPrestige] Run: /group addperm {defaultGroup.Name} {Permissions.Player}");
            }
        }
        catch (Exception ex)
        {
            TShock.Log.ConsoleError($"[ClassPrestige] Error checking player permissions: {ex.Message}");
        }
    }
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            if (_killHooks != null)
            {
                ServerApi.Hooks.NpcKilled.Deregister(this, _killHooks.OnNpcKilled);
                ServerApi.Hooks.NpcStrike.Deregister(this, _killHooks.OnNpcStrike);
            }

            if (_saveHooks != null)
            {
                ServerApi.Hooks.NetGreetPlayer.Deregister(this, _saveHooks.OnNetGreetPlayer);
                ServerApi.Hooks.ServerLeave.Deregister(this, _saveHooks.OnServerLeave);
                ServerApi.Hooks.GameUpdate.Deregister(this, _saveHooks.OnGameUpdate);
                ServerApi.Hooks.WorldSave.Deregister(this, _saveHooks.OnWorldSave);
                TShockAPI.Hooks.PlayerHooks.PlayerPostLogin -= _saveHooks.OnPlayerPostLogin;
            }

            if (_chatHooks != null)
            {
                ServerApi.Hooks.ServerChat.Deregister(this, _chatHooks.OnServerChat);
            }

            _playerManager?.StopAutoSave();
            _leaderboardManager?.Stop();

            _playerManager?.SaveAllOnShutdown();

            _playerManager?.Dispose();
            _leaderboardManager?.Dispose();

            _cancellationTokenSource.Cancel();
            _cancellationTokenSource.Dispose();

            TShock.Log.ConsoleInfo("[ClassPrestige] Plugin disposed successfully.");
        }

        base.Dispose(disposing);
    }
}
