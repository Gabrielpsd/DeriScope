using System.IO;

public static class Logger
{
    private static string _filePath = "debug.log";
    private static Boolean Logging = true;

    public static void configureLogging(bool logSteps, AppConfig config)
    {
        Logger.Log($"Logging configured. LogSteps: {logSteps}");

        Logging = logSteps;
        _filePath = config.LogFileName + ".log";
    }
    public static void Log(string message)
    {
        if (Logging is false)
            return;

        var logLine = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} | {message}";

        File.AppendAllText(_filePath, logLine + Environment.NewLine);
    }

}