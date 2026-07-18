using CursorCycle.Domain;

namespace CursorCycle.Infrastructure;

public sealed class BaselineStore
{
    public CursorRegistrySnapshot? Load()
    {
        try
        {
            var snapshot = JsonFile.Load<CursorRegistrySnapshot>(AppPaths.BaselineFile);
            if (snapshot is null)
            {
                return null;
            }

            snapshot.DefaultValue ??= new RegistryValueSnapshot();
            snapshot.Values ??=
                new Dictionary<string, RegistryValueSnapshot>(StringComparer.OrdinalIgnoreCase);

            return snapshot;
        }
        catch
        {
            JsonFile.PreserveBrokenFile(AppPaths.BaselineFile);
            return null;
        }
    }

    public void Save(CursorRegistrySnapshot snapshot)
    {
        JsonFile.Save(AppPaths.BaselineFile, snapshot);
    }
}
