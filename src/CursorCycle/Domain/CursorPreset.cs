namespace CursorCycle.Domain;

public sealed class CursorPreset
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public string Name { get; set; } = "新しいカーソル";

    public string FolderPath { get; set; } = string.Empty;

    public Dictionary<string, string> ManualFilesByRegistryName { get; set; } =
        new(StringComparer.OrdinalIgnoreCase);

    public CursorPreset DeepClone()
    {
        return new CursorPreset
        {
            Id = Id,
            Name = Name,
            FolderPath = FolderPath,
            ManualFilesByRegistryName = new Dictionary<string, string>(
                ManualFilesByRegistryName ?? new Dictionary<string, string>(),
                StringComparer.OrdinalIgnoreCase)
        };
    }

    public override string ToString() => Name;
}
