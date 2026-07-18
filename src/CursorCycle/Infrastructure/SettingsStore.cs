using CursorCycle.Domain;

namespace CursorCycle.Infrastructure;

public sealed class SettingsStore
{
    public AppSettings Load()
    {
        try
        {
            var settings = JsonFile.Load<AppSettings>(AppPaths.SettingsFile)
                ?? DefaultSettingsFactory.Create();

            settings.Normalize();
            return settings;
        }
        catch
        {
            JsonFile.PreserveBrokenFile(AppPaths.SettingsFile);
            return DefaultSettingsFactory.Create();
        }
    }

    public void Save(AppSettings settings)
    {
        var normalized = settings.DeepClone();
        normalized.Normalize();
        JsonFile.Save(AppPaths.SettingsFile, normalized);
    }
}
