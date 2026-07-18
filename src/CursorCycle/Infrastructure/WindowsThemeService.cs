using System.Runtime.InteropServices;

namespace CursorCycle.Infrastructure;

public static class WindowsThemeService
{
    public static void ApplyToWindow(Form form, bool useDarkMode)
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        try
        {
            var enabled = useDarkMode ? 1 : 0;
            var result = DwmSetWindowAttribute(
                form.Handle,
                20,
                ref enabled,
                sizeof(int));

            // Windows 10 の一部ビルドでは旧属性番号を使用する。
            if (result != 0)
            {
                DwmSetWindowAttribute(form.Handle, 19, ref enabled, sizeof(int));
            }
        }
        catch
        {
            // タイトルバーのテーマ適用に失敗してもアプリ動作は継続する。
        }
    }

    public static void ApplyToControl(Control control, bool useDarkMode)
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        try
        {
            SetWindowTheme(
                control.Handle,
                useDarkMode ? "DarkMode_Explorer" : "Explorer",
                null);
        }
        catch
        {
            // OSがテーマ名に対応していない場合は通常描画を使用する。
        }
    }

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(
        IntPtr windowHandle,
        int attribute,
        ref int attributeValue,
        int attributeSize);

    [DllImport("uxtheme.dll", CharSet = CharSet.Unicode)]
    private static extern int SetWindowTheme(
        IntPtr windowHandle,
        string? subApplicationName,
        string? subIdList);
}
