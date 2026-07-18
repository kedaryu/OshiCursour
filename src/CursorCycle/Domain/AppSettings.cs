namespace CursorCycle.Domain;

public sealed class AppSettings
{
    public const int CurrentDataVersion = 1;
    public const int MinimumIntervalSeconds = 5;
    public const int MaximumIntervalSeconds = 7 * 24 * 60 * 60;

    public int DataVersion { get; set; } = CurrentDataVersion;

    public bool IsRotationEnabled { get; set; }

    public int IntervalSeconds { get; set; } = 60 * 60;

    public CursorSelectionMode SelectionMode { get; set; } = CursorSelectionMode.Random;

    public Guid? SelectedGroupId { get; set; }

    public Guid? LastActivePresetId { get; set; }

    public bool StartWithWindows { get; set; }

    public bool UseDarkMode { get; set; }

    public List<CursorGroup> Groups { get; set; } = [];

    public AppSettings DeepClone()
    {
        return new AppSettings
        {
            DataVersion = DataVersion,
            IsRotationEnabled = IsRotationEnabled,
            IntervalSeconds = IntervalSeconds,
            SelectionMode = SelectionMode,
            SelectedGroupId = SelectedGroupId,
            LastActivePresetId = LastActivePresetId,
            StartWithWindows = StartWithWindows,
            UseDarkMode = UseDarkMode,
            Groups = Groups.Select(group => group.DeepClone()).ToList()
        };
    }

    public void Normalize()
    {
        DataVersion = CurrentDataVersion;
        IntervalSeconds = Math.Clamp(
            IntervalSeconds,
            MinimumIntervalSeconds,
            MaximumIntervalSeconds);

        Groups ??= [];

        var usedGroupIds = new HashSet<Guid>();
        var usedPresetIds = new HashSet<Guid>();

        for (var groupIndex = Groups.Count - 1; groupIndex >= 0; groupIndex--)
        {
            var group = Groups[groupIndex];
            if (group is null)
            {
                Groups.RemoveAt(groupIndex);
                continue;
            }

            if (group.Id == Guid.Empty || !usedGroupIds.Add(group.Id))
            {
                group.Id = Guid.NewGuid();
                usedGroupIds.Add(group.Id);
            }

            group.Name = CleanName(group.Name, "カーソルグループ");
            group.Presets ??= [];

            for (var presetIndex = group.Presets.Count - 1; presetIndex >= 0; presetIndex--)
            {
                var preset = group.Presets[presetIndex];
                if (preset is null)
                {
                    group.Presets.RemoveAt(presetIndex);
                    continue;
                }

                if (preset.Id == Guid.Empty || !usedPresetIds.Add(preset.Id))
                {
                    preset.Id = Guid.NewGuid();
                    usedPresetIds.Add(preset.Id);
                }

                preset.Name = CleanName(preset.Name, "カーソル");
                preset.FolderPath = (preset.FolderPath ?? string.Empty).Trim();
            }
        }

        if (SelectedGroupId is null || Groups.All(group => group.Id != SelectedGroupId))
        {
            SelectedGroupId = Groups.FirstOrDefault()?.Id;
        }

        if (LastActivePresetId is not null &&
            Groups.SelectMany(group => group.Presets)
                .All(preset => preset.Id != LastActivePresetId))
        {
            LastActivePresetId = null;
        }
    }

    public CursorGroup? GetSelectedGroup()
    {
        return SelectedGroupId is null
            ? null
            : Groups.FirstOrDefault(group => group.Id == SelectedGroupId);
    }

    private static string CleanName(string? value, string fallback)
    {
        var cleaned = (value ?? string.Empty).Trim();
        return string.IsNullOrWhiteSpace(cleaned) ? fallback : cleaned;
    }
}
