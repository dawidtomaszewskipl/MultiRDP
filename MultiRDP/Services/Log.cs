using System.IO;

namespace MultiRDP.Services;

/// <summary>
/// Prosty logger plikowy: %LocalAppData%\MultiRDP\logs\multirdp-RRRRMMDD.log.
/// Błędy logowania są ignorowane — log nie może zatrzymać aplikacji.
///
/// Simple file logger. Logging failures are ignored — the log must never stop the app.
/// </summary>
public static class Log
{
    private static readonly object Sync = new();

    public static string Directory { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "MultiRDP", "logs");

    public static void Info(string message)  => Write("INFO ", message);
    public static void Warn(string message)  => Write("WARN ", message);
    public static void Error(string message, Exception? ex = null)
        => Write("ERROR", ex is null ? message : $"{message}: {ex}");

    private static void Write(string level, string message)
    {
        try
        {
            lock (Sync)
            {
                System.IO.Directory.CreateDirectory(Directory);
                string file = Path.Combine(Directory, $"multirdp-{DateTime.Now:yyyyMMdd}.log");
                File.AppendAllText(file, $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} {level} {message}{Environment.NewLine}");
            }
        }
        catch { }
    }
}
