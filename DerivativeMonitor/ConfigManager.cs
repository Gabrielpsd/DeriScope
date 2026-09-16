using System.IO;
using System.Text.Json;

public static class ConfigManager
{
    private const string FilePath = "config.json";

    public static AppConfig Load()
    {
        if (!File.Exists(FilePath))
        {
            var defaultConfig = new AppConfig();

            var json = JsonSerializer.Serialize(defaultConfig, new JsonSerializerOptions
            {
                WriteIndented = true
            });

            File.WriteAllText(FilePath, json);

            return defaultConfig;
        }

        var fileContent = File.ReadAllText(FilePath);

        var config = JsonSerializer.Deserialize<AppConfig>(fileContent) ?? new AppConfig();

        return config;
    }

    public static void Validate(AppConfig config)
    {
        if (string.IsNullOrWhiteSpace(config.Ticker))
        {
            Logger.Log("Ticker is missing in config.json");
            throw new Exception("Ticker is missing in config.json");
        }

        if (string.IsNullOrWhiteSpace(config.PutOptionTableOnWeb))
        {
            Logger.Log("PutOptionTableOnWeb is missing in config.json");
            throw new Exception("PutOptionTableOnWeb is missing in config.json");
        }

        if (string.IsNullOrWhiteSpace(config.CallOptionTableOnWeb))
        {
            Logger.Log("CallOptionTableOnWeb is missing in config.json");
            throw new Exception("CallOptionTableOnWeb is missing in config.json");
        }

        if (string.IsNullOrWhiteSpace(config.BaseUrl))
        {
            Logger.Log("BaseUrl is missing in config.json");
            throw new Exception("BaseUrl is missing in config.json");
        }

        if (string.IsNullOrWhiteSpace(config.DatabasePath))
        {
            Logger.Log("DatabasePath is missing in config.json");
            throw new Exception("DatabasePath is missing in config.json");
        }

        if (string.IsNullOrWhiteSpace(config.TicketSuffix))
        {
            Logger.Log("TicketSuffix is missing in config.json");
            throw new Exception("TicketSuffix is missing in config.json");
        }

        if (config.RefreshIntervalMilliseconds <= 0)
        {
            Logger.Log("RefreshIntervalMilliseconds must be greater than 0 in config.json");
            throw new Exception("RefreshIntervalMilliseconds must be greater than 0 in config.json");
        }

        if (config.ScraperIntervalMinutes <= 0)
        {
            Logger.Log("ScraperIntervalMinutes must be greater than 0 in config.json");
            throw new Exception("ScraperIntervalMinutes must be greater than 0 in config.json");
        }

        if (config.CallParametersToMonitor == null || config.CallParametersToMonitor.Count == 0)
        {
            Logger.Log("CallParametersToMonitor must contain at least one entry in config.json");
            throw new Exception("CallParametersToMonitor must contain at least one entry in config.json");
        }

        if (config.PutParametersToMonitor == null || config.PutParametersToMonitor.Count == 0)
        {
            Logger.Log("PutParametersToMonitor must contain at least one entry in config.json");
            throw new Exception("PutParametersToMonitor must contain at least one entry in config.json");
        }
    }

    public static void Save(AppConfig config)
    {
        var json = JsonSerializer.Serialize(config, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(FilePath, json);
    }

    // Deep copy via JSON round-trip: the dialog edits this copy, so the live
    // _appConfig stays untouched until the user clicks SAVE.
    public static AppConfig Clone(AppConfig source)
    {
        var json = JsonSerializer.Serialize(source);
        return JsonSerializer.Deserialize<AppConfig>(json) ?? new AppConfig();
    }
}
