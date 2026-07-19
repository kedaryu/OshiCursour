namespace CursorCycle.Domain;

public sealed class CursorDetectionConfig
{
    public const int CurrentDataVersion = 1;

    public int DataVersion { get; set; } = CurrentDataVersion;

    public bool SearchSubfolders { get; set; }

    public bool PreferAnimatedCursors { get; set; } = true;

    public Dictionary<string, List<string>> PatternsByRegistryName { get; set; } =
        new(StringComparer.OrdinalIgnoreCase);

    public static CursorDetectionConfig CreateDefault()
    {
        var config = new CursorDetectionConfig();
        foreach (var role in CursorRoles.All)
        {
            var patterns = new List<string>();
            foreach (var alias in role.FileAliases)
            {
                AddUnique(patterns, alias);

                if (ContainsNonAscii(alias) && alias.Length >= 2)
                {
                    // 「かぐや_通常」だけでなく「カーソル通常」「通常カーソル」にも対応する。
                    AddUnique(patterns, $"*{alias}");
                    AddUnique(patterns, $"{alias}*");
                }
                else
                {
                    AddUnique(patterns, $"*_{alias}");
                    AddUnique(patterns, $"*-{alias}");
                    AddUnique(patterns, $"* {alias}");
                }
            }

            config.PatternsByRegistryName[role.RegistryName] = patterns;
        }

        return config;
    }

    public void Normalize()
    {
        DataVersion = CurrentDataVersion;
        PatternsByRegistryName ??=
            new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);

        var defaults = CreateDefault();
        foreach (var role in CursorRoles.All)
        {
            if (!PatternsByRegistryName.TryGetValue(role.RegistryName, out var patterns) ||
                patterns is null)
            {
                PatternsByRegistryName[role.RegistryName] =
                    defaults.PatternsByRegistryName[role.RegistryName];
                continue;
            }

            PatternsByRegistryName[role.RegistryName] = patterns
                .Select(pattern => (pattern ?? string.Empty).Trim())
                .Where(pattern => !string.IsNullOrWhiteSpace(pattern))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
        }
    }

    private static bool ContainsNonAscii(string value) => value.Any(character => character > 127);

    private static void AddUnique(List<string> values, string value)
    {
        if (!values.Contains(value, StringComparer.OrdinalIgnoreCase))
        {
            values.Add(value);
        }
    }
}
