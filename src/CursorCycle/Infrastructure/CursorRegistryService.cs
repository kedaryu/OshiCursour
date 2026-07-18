using System.ComponentModel;
using System.Runtime.InteropServices;
using CursorCycle.Domain;
using Microsoft.Win32;

namespace CursorCycle.Infrastructure;

public sealed class CursorRegistryService
{
    private const string CursorRegistryPath = @"Control Panel\Cursors";
    private const uint SpiSetCursors = 0x0057;
    private const uint SpifSendChange = 0x0002;

    public CursorRegistrySnapshot Capture()
    {
        using var key = Registry.CurrentUser.OpenSubKey(CursorRegistryPath, false)
            ?? throw new InvalidOperationException("Windows のカーソル設定を読み込めません。");

        var snapshot = new CursorRegistrySnapshot
        {
            CapturedAtUtc = DateTime.UtcNow,
            DefaultValue = CaptureValue(key, null)
        };

        foreach (var role in CursorRoles.All)
        {
            snapshot.Values[role.RegistryName] = CaptureValue(key, role.RegistryName);
        }

        return snapshot;
    }

    public void Apply(CursorSetScanResult cursorSet)
    {
        if (!cursorSet.IsValid)
        {
            throw new InvalidOperationException(
                "通常カーソルを検出できないため、このカーソル一式は適用できません。");
        }

        var rollback = Capture();

        try
        {
            var windowsFolder = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
            var defaults = CreateWindowsDefaultMap(Path.Combine(windowsFolder, "Cursors"));
            using var key = Registry.CurrentUser.CreateSubKey(CursorRegistryPath, true)
                ?? throw new InvalidOperationException("Windows のカーソル設定を開けません。");

            foreach (var role in CursorRoles.All)
            {
                if (cursorSet.FilesByRegistryName.TryGetValue(role.RegistryName, out var path))
                {
                    key.SetValue(role.RegistryName, path, RegistryValueKind.String);
                    continue;
                }

                // 不足役割を「切り替え開始前」のカーソルで補うと、別セットの
                // キャラクターが混在する。常に Windows 標準で補完する。
                var defaultPath = defaults.GetValueOrDefault(role.RegistryName, string.Empty);
                if (!string.IsNullOrEmpty(defaultPath) && !File.Exists(defaultPath))
                {
                    defaultPath = string.Empty;
                }

                key.SetValue(role.RegistryName, defaultPath, RegistryValueKind.String);
            }

            ReloadSystemCursors();
        }
        catch
        {
            TryRollback(rollback);
            throw;
        }
    }

    public void Restore(CursorRegistrySnapshot snapshot)
    {
        var rollback = Capture();

        try
        {
            RestoreCore(snapshot);
            ReloadSystemCursors();
        }
        catch
        {
            TryRollback(rollback);
            throw;
        }
    }

    public void RestoreWindowsDefault()
    {
        var rollback = Capture();

        try
        {
            var windowsFolder = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
            var cursorsFolder = Path.Combine(windowsFolder, "Cursors");
            var defaults = CreateWindowsDefaultMap(cursorsFolder);

            using var key = Registry.CurrentUser.CreateSubKey(CursorRegistryPath, true)
                ?? throw new InvalidOperationException("Windows のカーソル設定を開けません。");

            foreach (var role in CursorRoles.All)
            {
                var value = defaults.GetValueOrDefault(role.RegistryName, string.Empty);
                if (!string.IsNullOrEmpty(value) && !File.Exists(value))
                {
                    value = string.Empty;
                }

                key.SetValue(role.RegistryName, value, RegistryValueKind.String);
            }

            ReloadSystemCursors();
        }
        catch
        {
            TryRollback(rollback);
            throw;
        }
    }

    private static RegistryValueSnapshot CaptureValue(RegistryKey key, string? valueName)
    {
        var normalizedName = valueName ?? string.Empty;
        var exists = key.GetValueNames().Any(name =>
            string.Equals(name, normalizedName, StringComparison.OrdinalIgnoreCase));

        if (!exists)
        {
            return new RegistryValueSnapshot { Exists = false };
        }

        var rawValue = key.GetValue(
            normalizedName,
            null,
            RegistryValueOptions.DoNotExpandEnvironmentNames);

        return new RegistryValueSnapshot
        {
            Exists = true,
            Value = rawValue?.ToString() ?? string.Empty,
            ValueKind = (int)key.GetValueKind(normalizedName)
        };
    }

    private static void RestoreCore(CursorRegistrySnapshot snapshot)
    {
        using var key = Registry.CurrentUser.CreateSubKey(CursorRegistryPath, true)
            ?? throw new InvalidOperationException("Windows のカーソル設定を開けません。");

        RestoreValue(key, null, snapshot.DefaultValue);

        foreach (var role in CursorRoles.All)
        {
            if (snapshot.Values.TryGetValue(role.RegistryName, out var value))
            {
                RestoreValue(key, role.RegistryName, value);
            }
            else
            {
                key.DeleteValue(role.RegistryName, false);
            }
        }
    }

    private static void RestoreValue(
        RegistryKey key,
        string? valueName,
        RegistryValueSnapshot snapshot)
    {
        var normalizedName = valueName ?? string.Empty;
        if (!snapshot.Exists)
        {
            key.DeleteValue(normalizedName, false);
            return;
        }

        var kind = Enum.IsDefined(typeof(RegistryValueKind), snapshot.ValueKind)
            ? (RegistryValueKind)snapshot.ValueKind
            : RegistryValueKind.String;

        if (kind is not RegistryValueKind.String and not RegistryValueKind.ExpandString)
        {
            kind = RegistryValueKind.String;
        }

        key.SetValue(normalizedName, snapshot.Value ?? string.Empty, kind);
    }

    private static Dictionary<string, string> CreateWindowsDefaultMap(string folder)
    {
        return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["Arrow"] = Path.Combine(folder, "aero_arrow.cur"),
            ["Help"] = Path.Combine(folder, "aero_helpsel.cur"),
            ["AppStarting"] = Path.Combine(folder, "aero_working.ani"),
            ["Wait"] = Path.Combine(folder, "aero_busy.ani"),
            ["Crosshair"] = string.Empty,
            ["IBeam"] = string.Empty,
            ["NWPen"] = Path.Combine(folder, "aero_pen.cur"),
            ["No"] = Path.Combine(folder, "aero_unavail.cur"),
            ["SizeNS"] = Path.Combine(folder, "aero_ns.cur"),
            ["SizeWE"] = Path.Combine(folder, "aero_ew.cur"),
            ["SizeNWSE"] = Path.Combine(folder, "aero_nwse.cur"),
            ["SizeNESW"] = Path.Combine(folder, "aero_nesw.cur"),
            ["SizeAll"] = Path.Combine(folder, "aero_move.cur"),
            ["UpArrow"] = Path.Combine(folder, "aero_up.cur"),
            ["Hand"] = Path.Combine(folder, "aero_link.cur"),
            ["Pin"] = Path.Combine(folder, "aero_pin.cur"),
            ["Person"] = Path.Combine(folder, "aero_person.cur")
        };
    }

    private static void TryRollback(CursorRegistrySnapshot rollback)
    {
        try
        {
            RestoreCore(rollback);
            ReloadSystemCursors();
        }
        catch
        {
            // 元の例外を優先する。次回起動時には保存済みベースラインから復元できる。
        }
    }

    private static void ReloadSystemCursors()
    {
        if (!SystemParametersInfo(
                SpiSetCursors,
                0,
                IntPtr.Zero,
                SpifSendChange))
        {
            throw new Win32Exception(
                Marshal.GetLastWin32Error(),
                "Windows にカーソルを再読み込みさせられませんでした。");
        }
    }

    [DllImport("user32.dll", EntryPoint = "SystemParametersInfoW", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SystemParametersInfo(
        uint uiAction,
        uint uiParam,
        IntPtr pvParam,
        uint fWinIni);
}
