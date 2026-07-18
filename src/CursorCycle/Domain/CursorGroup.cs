namespace CursorCycle.Domain;

public sealed class CursorGroup
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public string Name { get; set; } = "新しいグループ";

    public List<CursorPreset> Presets { get; set; } = [];

    public CursorGroup DeepClone()
    {
        return new CursorGroup
        {
            Id = Id,
            Name = Name,
            Presets = Presets.Select(preset => preset.DeepClone()).ToList()
        };
    }

    public override string ToString() => Name;
}
