namespace CursorCycle.Domain;

public static class DefaultSettingsFactory
{
    public static AppSettings Create()
    {
        return new AppSettings
        {
            IsRotationEnabled = false,
            IntervalSeconds = 60 * 60,
            SelectionMode = CursorSelectionMode.Random,
            SelectedGroupId = null,
            StartWithWindows = false,
            UseDarkMode = false,
            Groups = []
        };
    }
}
