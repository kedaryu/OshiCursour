using System.Text;

namespace CursorCycle.Infrastructure;

public static class DiagnosticLogger
{
    private static readonly object SyncRoot = new();

    public static string LogFilePath => Path.Combine(
        AppPaths.DataDirectory,
        "logs",
        "OshiCursour.log");

    public static void Write(string message)
    {
        try
        {
            lock (SyncRoot)
            {
                var directory = Path.GetDirectoryName(LogFilePath);
                if (!string.IsNullOrWhiteSpace(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                TrimIfNeeded();

                var line =
                    $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] " +
                    $"[PID:{Environment.ProcessId}] " +
                    $"[TID:{Environment.CurrentManagedThreadId}] " +
                    message + Environment.NewLine;

                File.AppendAllText(LogFilePath, line, new UTF8Encoding(false));
            }
        }
        catch
        {
            // 診断ログ自体の失敗でアプリを停止させない。
        }
    }

    public static void WriteException(string stage, Exception exception)
    {
        Write($"EXCEPTION at {stage}{Environment.NewLine}{exception}");
    }

    private static void TrimIfNeeded()
    {
        const long maximumBytes = 1024 * 1024;
        const int retainedCharacters = 256 * 1024;

        var file = new FileInfo(LogFilePath);
        if (!file.Exists || file.Length <= maximumBytes)
        {
            return;
        }

        var text = File.ReadAllText(LogFilePath);
        if (text.Length > retainedCharacters)
        {
            text = text[^retainedCharacters..];
        }

        File.WriteAllText(LogFilePath, text, new UTF8Encoding(false));
    }
}
