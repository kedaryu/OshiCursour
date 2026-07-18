namespace CursorCycle.Domain;

public sealed class CursorRegistrySnapshot
{
    public DateTime CapturedAtUtc { get; set; } = DateTime.UtcNow;

    public RegistryValueSnapshot DefaultValue { get; set; } = new();

    public Dictionary<string, RegistryValueSnapshot> Values { get; set; } =
        new(StringComparer.OrdinalIgnoreCase);
}

public sealed class RegistryValueSnapshot
{
    public bool Exists { get; set; }

    public string? Value { get; set; }

    public int ValueKind { get; set; }
}
