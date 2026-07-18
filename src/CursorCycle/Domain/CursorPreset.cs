namespace CursorCycle.Domain;

public sealed class CursorPreset
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public string Name { get; set; } = "新しいカーソル";

    public string FolderPath { get; set; } = string.Empty;

    public CursorPreset DeepClone()
    {
        return new CursorPreset
        {
            Id = Id,
            Name = Name,
            FolderPath = FolderPath
        };
    }

    public override string ToString() => Name;
}
