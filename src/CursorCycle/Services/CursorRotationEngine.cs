using CursorCycle.Domain;

namespace CursorCycle.Services;

public sealed class CursorRotationEngine : IDisposable
{
    private readonly System.Windows.Forms.Timer _timer = new();
    private List<CursorPreset> _presets = [];
    private CursorSelectionMode _selectionMode;
    private Guid? _currentPresetId;
    private bool _enabled;
    private bool _disposed;

    public CursorRotationEngine()
    {
        _timer.Tick += HandleTimerTick;
    }

    public event Action<CursorPreset>? PresetDue;

    public event Action? ScheduleChanged;

    public bool IsRunning => _timer.Enabled;

    public DateTime? NextSwitchAt { get; private set; }

    public void Configure(
        bool enabled,
        int intervalSeconds,
        CursorSelectionMode selectionMode,
        IReadOnlyList<CursorPreset> presets,
        Guid? currentPresetId,
        bool switchImmediately)
    {
        ThrowIfDisposed();

        _timer.Stop();
        NextSwitchAt = null;
        _enabled = enabled;
        _selectionMode = selectionMode;
        _presets = presets.Select(preset => preset.DeepClone()).ToList();
        _currentPresetId = currentPresetId;

        var safeSeconds = Math.Clamp(
            intervalSeconds,
            AppSettings.MinimumIntervalSeconds,
            AppSettings.MaximumIntervalSeconds);

        _timer.Interval = checked(safeSeconds * 1000);

        if (!_enabled || _presets.Count == 0)
        {
            ScheduleChanged?.Invoke();
            return;
        }

        if (switchImmediately)
        {
            RequestNextPreset();
        }

        StartTimer();
    }

    public void RestartAfterManualSelection(Guid presetId)
    {
        ThrowIfDisposed();
        _currentPresetId = presetId;

        if (!_enabled || _presets.Count == 0)
        {
            return;
        }

        _timer.Stop();
        StartTimer();
    }

    public void Stop()
    {
        if (_disposed)
        {
            return;
        }

        _enabled = false;
        _timer.Stop();
        NextSwitchAt = null;
        ScheduleChanged?.Invoke();
    }

    private void HandleTimerTick(object? sender, EventArgs eventArgs)
    {
        _timer.Stop();
        NextSwitchAt = null;

        if (!_enabled || _presets.Count == 0)
        {
            ScheduleChanged?.Invoke();
            return;
        }

        RequestNextPreset();
        StartTimer();
    }

    private void RequestNextPreset()
    {
        var next = SelectNextPreset();
        _currentPresetId = next.Id;
        PresetDue?.Invoke(next);
    }

    private CursorPreset SelectNextPreset()
    {
        if (_presets.Count == 1)
        {
            return _presets[0];
        }

        if (_selectionMode == CursorSelectionMode.Sequential)
        {
            var currentIndex = _presets.FindIndex(preset => preset.Id == _currentPresetId);
            var nextIndex = currentIndex < 0 ? 0 : (currentIndex + 1) % _presets.Count;
            return _presets[nextIndex];
        }

        var current = _presets.FindIndex(preset => preset.Id == _currentPresetId);
        if (current < 0)
        {
            return _presets[Random.Shared.Next(_presets.Count)];
        }

        var randomIndex = Random.Shared.Next(_presets.Count - 1);
        if (randomIndex >= current)
        {
            randomIndex++;
        }

        return _presets[randomIndex];
    }

    private void StartTimer()
    {
        _timer.Start();
        NextSwitchAt = DateTime.Now.AddMilliseconds(_timer.Interval);
        ScheduleChanged?.Invoke();
    }

    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _timer.Stop();
        _timer.Tick -= HandleTimerTick;
        _timer.Dispose();
    }
}
