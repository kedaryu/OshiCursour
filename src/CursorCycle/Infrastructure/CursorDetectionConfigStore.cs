using CursorCycle.Domain;

namespace CursorCycle.Infrastructure;

public sealed class CursorDetectionConfigStore
{
    public CursorDetectionConfig Load()
    {
        try
        {
            var config = JsonFile.Load<CursorDetectionConfig>(AppPaths.CursorDetectionConfigFile);
            if (config is null)
            {
                config = CursorDetectionConfig.CreateDefault();
                Save(config);
            }

            config.Normalize();
            return config;
        }
        catch (Exception exception)
        {
            DiagnosticLogger.WriteException("CursorDetectionConfigStore.Load", exception);
            JsonFile.PreserveBrokenFile(AppPaths.CursorDetectionConfigFile);
            var fallback = CursorDetectionConfig.CreateDefault();
            try
            {
                Save(fallback);
            }
            catch (Exception saveException)
            {
                DiagnosticLogger.WriteException(
                    "CursorDetectionConfigStore.SaveFallback",
                    saveException);
            }

            return fallback;
        }
    }

    public void Save(CursorDetectionConfig config)
    {
        config.Normalize();
        JsonFile.Save(AppPaths.CursorDetectionConfigFile, config);
    }
}
