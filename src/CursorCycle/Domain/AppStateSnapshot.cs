namespace CursorCycle.Domain;

public sealed record AppStateSnapshot(
    AppSettings Settings,
    Guid? ActivePresetId,
    string ActivePresetName,
    string StatusMessage,
    bool IsTimerRunning,
    DateTime? NextSwitchAt);
