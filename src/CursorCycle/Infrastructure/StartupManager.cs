using Microsoft.Win32;

namespace CursorCycle.Infrastructure;

public sealed class StartupManager
{
    private const string RunRegistryPath =
        @"Software\Microsoft\Windows\CurrentVersion\Run";

    private const string ValueName = "OshiCursour";

    public void SetEnabled(bool enabled)
    {
        using var key = Registry.CurrentUser.CreateSubKey(RunRegistryPath, true)
            ?? throw new InvalidOperationException("Windows の自動起動設定を開けません。");

        if (!enabled)
        {
            key.DeleteValue(ValueName, false);
            return;
        }

        var executablePath = Environment.ProcessPath;
        if (string.IsNullOrWhiteSpace(executablePath) || !File.Exists(executablePath))
        {
            throw new InvalidOperationException("実行ファイルの場所を取得できません。");
        }

        key.SetValue(
            ValueName,
            $"\"{executablePath}\" --startup",
            RegistryValueKind.String);
    }
}
