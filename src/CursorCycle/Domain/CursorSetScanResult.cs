namespace CursorCycle.Domain;

public sealed class CursorSetScanResult
{
    public required string FolderPath { get; init; }

    public Dictionary<string, string> FilesByRegistryName { get; init; } =
        new(StringComparer.OrdinalIgnoreCase);

    public List<string> Warnings { get; init; } = [];

    public bool IsValid => FilesByRegistryName.ContainsKey(CursorRoles.Arrow.RegistryName);

    public int MatchedRoleCount => FilesByRegistryName.Count;
}
