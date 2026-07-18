namespace CursorCycle.Infrastructure;

public static class AppPaths
{
    public static string DataDirectory => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "OshiCursour");

    public static string SettingsFile => Path.Combine(DataDirectory, "settings.json");

    public static string BaselineFile => Path.Combine(DataDirectory, "cursor-baseline.json");
}
