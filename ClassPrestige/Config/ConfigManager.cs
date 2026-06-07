using System.Text.Json;

using TShockAPI;

namespace ClassPrestige.Config;
public sealed class ConfigManager(string configPath)
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true
    };
    public PluginConfig Current { get; private set; } = new();
    public async Task LoadAsync(CancellationToken ct = default)
    {
        if (!File.Exists(configPath))
        {
            TShock.Log.ConsoleInfo("[ClassPrestige] Configuration file not found. Generating defaults.");
            Current = new PluginConfig();
            Validate(Current);
            await SaveAsync(ct).ConfigureAwait(false);
            return;
        }

        try
        {
            await using var stream = File.OpenRead(configPath);
            var config = await JsonSerializer.DeserializeAsync<PluginConfig>(stream, cancellationToken: ct)
                .ConfigureAwait(false);

            if (config is null)
            {
                TShock.Log.ConsoleError("[ClassPrestige] Configuration file deserialized to null. Using defaults.");
                Current = new PluginConfig();
                Validate(Current);
                return;
            }

            Validate(config);
            Current = config;
            TShock.Log.ConsoleInfo("[ClassPrestige] Configuration loaded successfully.");
        }
        catch (JsonException ex)
        {
            TShock.Log.ConsoleError($"[ClassPrestige] Failed to parse configuration file: {ex.Message}. Using defaults.");
            Current = new PluginConfig();
            Validate(Current);
        }
        catch (IOException ex)
        {
            TShock.Log.ConsoleError($"[ClassPrestige] Failed to read configuration file: {ex.Message}. Using defaults.");
            Current = new PluginConfig();
            Validate(Current);
        }
    }
    public async Task SaveAsync(CancellationToken ct = default)
    {
        try
        {
            var directory = Path.GetDirectoryName(configPath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            await using var stream = File.Create(configPath);
            await JsonSerializer.SerializeAsync(stream, Current, SerializerOptions, ct)
                .ConfigureAwait(false);

            TShock.Log.ConsoleInfo($"[ClassPrestige] Configuration saved to {configPath}.");
        }
        catch (IOException ex)
        {
            TShock.Log.ConsoleError($"[ClassPrestige] Failed to write configuration: {ex.Message}");
        }
    }
    public async Task<string?> ReloadAsync(CancellationToken ct = default)
    {
        if (!File.Exists(configPath))
        {
            return "Configuration file not found. Current configuration retained.";
        }

        try
        {
            await using var stream = File.OpenRead(configPath);
            var newConfig = await JsonSerializer.DeserializeAsync<PluginConfig>(stream, cancellationToken: ct)
                .ConfigureAwait(false);

            if (newConfig is null)
            {
                return "Configuration file deserialized to null. Current configuration retained.";
            }

            Validate(newConfig);

            newConfig.DatabaseType = Current.DatabaseType;
            newConfig.MySQLHost = Current.MySQLHost;
            newConfig.MySQLDatabase = Current.MySQLDatabase;
            newConfig.MySQLUser = Current.MySQLUser;
            newConfig.MySQLPassword = Current.MySQLPassword;

            Current = newConfig;
            TShock.Log.ConsoleInfo("[ClassPrestige] Configuration reloaded successfully.");
            return null;
        }
        catch (JsonException ex)
        {
            var msg = $"Failed to parse configuration file: {ex.Message}. Current configuration retained.";
            TShock.Log.ConsoleError($"[ClassPrestige] {msg}");
            return msg;
        }
        catch (IOException ex)
        {
            var msg = $"Failed to read configuration file: {ex.Message}. Current configuration retained.";
            TShock.Log.ConsoleError($"[ClassPrestige] {msg}");
            return msg;
        }
    }
    private static void Validate(PluginConfig config)
    {
        if (config.EventExpMultiplier < 0.1)
        {
            TShock.Log.ConsoleWarn(
                $"[ClassPrestige] EventExpMultiplier ({config.EventExpMultiplier}) is below minimum 0.1. Clamping to 0.1.");
            config.EventExpMultiplier = 0.1;
        }
        else if (config.EventExpMultiplier > 0.99)
        {
            TShock.Log.ConsoleWarn(
                $"[ClassPrestige] EventExpMultiplier ({config.EventExpMultiplier}) exceeds maximum 0.99. Clamping to 0.99.");
            config.EventExpMultiplier = 0.99;
        }
    }
}
