using System.Runtime.InteropServices;

namespace CursorCycle.Infrastructure;

public static class CursorThumbnailLoader
{
    public static Bitmap? Load(string? filePath)
    {
        if (!OperatingSystem.IsWindows() ||
            string.IsNullOrWhiteSpace(filePath) ||
            !File.Exists(filePath))
        {
            return null;
        }

        var cursorHandle = LoadCursorFromFile(filePath);
        if (cursorHandle == IntPtr.Zero)
        {
            return null;
        }

        try
        {
            using var icon = Icon.FromHandle(cursorHandle);
            return icon.ToBitmap();
        }
        catch
        {
            return null;
        }
        finally
        {
            DestroyCursor(cursorHandle);
        }
    }

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern IntPtr LoadCursorFromFile(string fileName);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DestroyCursor(IntPtr cursorHandle);
}
