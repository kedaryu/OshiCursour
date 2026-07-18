using System.Diagnostics;
using System.Runtime.InteropServices;

namespace CursorCycle.Infrastructure;

public static class UpdateInstaller
{
    public static bool TryRunUpdaterMode(string[] args)
    {
        if (!args.Contains("--apply-update", StringComparer.OrdinalIgnoreCase))
        {
            return false;
        }

        try
        {
            ApplyUpdate(args);
        }
        catch (Exception exception)
        {
            DiagnosticLogger.WriteException("UpdateInstaller.ApplyUpdate", exception);
            TryShowError("OshiCursourを更新できませんでした。\n\n" + exception.Message);
        }

        return true;
    }

    public static void MarkApplicationHealthy(string[] args)
    {
        var marker = GetArgument(args, "--update-health-marker");
        if (string.IsNullOrWhiteSpace(marker))
        {
            return;
        }

        try
        {
            var directory = Path.GetDirectoryName(marker);
            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }

            File.WriteAllText(marker, DateTime.UtcNow.ToString("O"));
            DiagnosticLogger.Write("Update health marker written.");
        }
        catch (Exception exception)
        {
            DiagnosticLogger.WriteException("UpdateInstaller.MarkApplicationHealthy", exception);
        }
    }

    private static void ApplyUpdate(string[] args)
    {
        var waitPid = int.Parse(RequireArgument(args, "--wait-pid"));
        var source = RequireArgument(args, "--source");
        var target = RequireArgument(args, "--target");
        var backup = RequireArgument(args, "--backup");
        var marker = RequireArgument(args, "--marker");
        var updater = RequireArgument(args, "--updater");

        WaitForProcessExit(waitPid, TimeSpan.FromSeconds(45));
        if (!File.Exists(source))
        {
            throw new FileNotFoundException("更新後の実行ファイルがありません。", source);
        }

        var backupDirectory = Path.GetDirectoryName(backup);
        if (!string.IsNullOrWhiteSpace(backupDirectory))
        {
            Directory.CreateDirectory(backupDirectory);
        }

        File.Copy(target, backup, overwrite: true);

        Process? updatedProcess = null;
        try
        {
            File.Copy(source, target, overwrite: true);
            updatedProcess = StartApplication(target, marker);
            if (WaitForMarker(marker, updatedProcess, TimeSpan.FromSeconds(40)))
            {
                TryDelete(backup);
                TryDeleteDirectory(Path.GetDirectoryName(marker));
                ScheduleDeleteAfterRestart(updater);
                return;
            }

            throw new InvalidOperationException(
                "更新後のアプリが正常に起動したことを確認できませんでした。");
        }
        catch
        {
            TryStop(updatedProcess);
            File.Copy(backup, target, overwrite: true);
            _ = StartApplication(target, markerPath: null, rollback: true);
            throw;
        }
    }

    private static Process StartApplication(
        string executable,
        string? markerPath,
        bool rollback = false)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = executable,
            UseShellExecute = true,
            WorkingDirectory = Path.GetDirectoryName(executable)
                ?? Environment.CurrentDirectory
        };
        if (!string.IsNullOrWhiteSpace(markerPath))
        {
            startInfo.ArgumentList.Add("--update-health-marker");
            startInfo.ArgumentList.Add(markerPath);
        }

        if (rollback)
        {
            startInfo.ArgumentList.Add("--update-rollback");
        }

        return Process.Start(startInfo)
            ?? throw new InvalidOperationException("OshiCursourを再起動できません。");
    }

    private static bool WaitForMarker(
        string markerPath,
        Process process,
        TimeSpan timeout)
    {
        var stopwatch = Stopwatch.StartNew();
        while (stopwatch.Elapsed < timeout)
        {
            if (File.Exists(markerPath))
            {
                return true;
            }

            if (process.HasExited)
            {
                return false;
            }

            Thread.Sleep(250);
        }

        return false;
    }

    private static void WaitForProcessExit(int processId, TimeSpan timeout)
    {
        try
        {
            using var process = Process.GetProcessById(processId);
            if (!process.WaitForExit((int)timeout.TotalMilliseconds))
            {
                throw new TimeoutException("更新前のOshiCursourが終了しませんでした。");
            }
        }
        catch (ArgumentException)
        {
            // 既に終了している。
        }
    }

    private static string RequireArgument(string[] args, string name) =>
        GetArgument(args, name)
        ?? throw new ArgumentException($"更新引数 {name} がありません。");

    private static string? GetArgument(string[] args, string name)
    {
        for (var index = 0; index < args.Length - 1; index++)
        {
            if (string.Equals(args[index], name, StringComparison.OrdinalIgnoreCase))
            {
                return args[index + 1];
            }
        }

        return null;
    }

    private static void TryStop(Process? process)
    {
        try
        {
            if (process is not null && !process.HasExited)
            {
                process.Kill(entireProcessTree: true);
                process.WaitForExit(5000);
            }
        }
        catch
        {
            // ロールバックを優先する。
        }
    }

    private static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch
        {
            // 次回更新時に上書きされる。
        }
    }

    private static void TryDeleteDirectory(string? path)
    {
        try
        {
            if (!string.IsNullOrWhiteSpace(path) && Directory.Exists(path))
            {
                Directory.Delete(path, recursive: true);
            }
        }
        catch
        {
            // 使用中のファイルがあれば次回の更新時に再利用・上書きする。
        }
    }

    private static void ScheduleDeleteAfterRestart(string path)
    {
        try
        {
            MoveFileEx(path, null, MoveFileDelayUntilReboot);
        }
        catch
        {
            // 一時ファイルはOSのクリーンアップ対象になる。
        }
    }

    private static void TryShowError(string message)
    {
        try
        {
            MessageBox.Show(
                message,
                "OshiCursour 更新エラー",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }
        catch
        {
            // ログのみ残す。
        }
    }

    private const int MoveFileDelayUntilReboot = 0x4;

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool MoveFileEx(
        string existingFileName,
        string? newFileName,
        int flags);
}
