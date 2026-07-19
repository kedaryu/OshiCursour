using CursorCycle.Domain;
using CursorCycle.Infrastructure;
using CursorCycle.Services;

namespace CursorCycle.Application;

public sealed class AppController : IDisposable
{
    private readonly SettingsStore _settingsStore = new();
    private readonly BaselineStore _baselineStore = new();
    private readonly CursorFolderScanner _folderScanner = new();
    private readonly CursorRegistryService _cursorRegistry = new();
    private readonly StartupManager _startupManager = new();
    private readonly CursorRotationEngine _rotationEngine = new();

    private AppSettings _settings;
    private CursorRegistrySnapshot? _baseline;
    private Guid? _activePresetId;
    private string _activePresetName = "未選択";
    private string _statusMessage = "待機中";
    private bool _initialized;
    private bool _disposed;

    public AppController()
    {
        _settings = _settingsStore.Load();
        _activePresetId = _settings.LastActivePresetId;
        _activePresetName = FindPreset(_activePresetId)?.Name ?? "未選択";

        _rotationEngine.PresetDue += HandlePresetDue;
        _rotationEngine.ScheduleChanged += RaiseStateChanged;
    }

    public event Action<AppStateSnapshot>? StateChanged;

    public event Action<string>? ErrorOccurred;

    public void Initialize()
    {
        if (_initialized)
        {
            return;
        }

        _initialized = true;

        EnsureBaseline();

        try
        {
            _startupManager.SetEnabled(_settings.StartWithWindows);
        }
        catch (Exception exception)
        {
            ReportError($"自動起動設定を反映できませんでした: {exception.Message}");
        }

        if (_settings.IsRotationEnabled)
        {
            ConfigureRotation(switchImmediately: true);
        }
        else
        {
            _rotationEngine.Stop();
            _statusMessage = "自動切り替えは OFF です";
        }

        RaiseStateChanged();
    }

    public AppStateSnapshot GetState()
    {
        return new AppStateSnapshot(
            _settings.DeepClone(),
            _activePresetId,
            _activePresetName,
            _statusMessage,
            _rotationEngine.IsRunning,
            _rotationEngine.NextSwitchAt);
    }

    public CursorSetScanResult ScanPreset(CursorPreset preset)
    {
        return _folderScanner.Scan(preset);
    }

    public OperationResult UpdateSettings(AppSettings updatedSettings)
    {
        ThrowIfDisposed();

        var normalized = updatedSettings.DeepClone();
        normalized.Normalize();
        var previous = _settings.DeepClone();

        try
        {
            if (previous.StartWithWindows != normalized.StartWithWindows)
            {
                _startupManager.SetEnabled(normalized.StartWithWindows);
            }

            _settingsStore.Save(normalized);
            _settings = normalized;

            if (!_settings.IsRotationEnabled)
            {
                _rotationEngine.Stop();
                _statusMessage = "自動切り替えは OFF です";
            }
            else
            {
                var switchImmediately =
                    !previous.IsRotationEnabled ||
                    previous.SelectedGroupId != _settings.SelectedGroupId;

                ConfigureRotation(switchImmediately);
            }

            RaiseStateChanged();
            return OperationResult.Ok("設定を保存しました。");
        }
        catch (Exception exception)
        {
            try
            {
                if (previous.StartWithWindows != normalized.StartWithWindows)
                {
                    _startupManager.SetEnabled(previous.StartWithWindows);
                }
            }
            catch
            {
                // 元の自動起動状態への復帰に失敗しても、最初のエラーを返す。
            }

            _settings = previous;
            var message = $"設定を保存できませんでした: {exception.Message}";
            ReportError(message);
            return OperationResult.Fail(message);
        }
    }

    public OperationResult SetRotationEnabled(bool enabled)
    {
        var updated = _settings.DeepClone();
        updated.IsRotationEnabled = enabled;
        return UpdateSettings(updated);
    }

    public OperationResult SelectGroup(Guid groupId)
    {
        if (_settings.Groups.All(group => group.Id != groupId))
        {
            return OperationResult.Fail("選択したグループが見つかりません。");
        }

        var updated = _settings.DeepClone();
        updated.SelectedGroupId = groupId;
        return UpdateSettings(updated);
    }

    public OperationResult ApplyPresetNow(Guid presetId)
    {
        ThrowIfDisposed();

        var preset = FindPreset(presetId);
        if (preset is null)
        {
            return OperationResult.Fail("選択したカーソルが見つかりません。");
        }

        var result = ApplyPresetCore(preset, automatic: false);
        if (result.Success)
        {
            _rotationEngine.RestartAfterManualSelection(preset.Id);
        }

        RaiseStateChanged();
        return result;
    }

    public OperationResult RestoreBaseline()
    {
        ThrowIfDisposed();

        if (_baseline is null)
        {
            return OperationResult.Fail("復元用カーソルが保存されていません。");
        }

        var disableResult = DisableRotation();
        if (!disableResult.Success)
        {
            return disableResult;
        }

        try
        {
            _cursorRegistry.Restore(_baseline);
            _activePresetId = null;
            _activePresetName = "切り替え開始前のカーソル";
            _statusMessage = "切り替え開始前のカーソルへ戻しました";
            RaiseStateChanged();
            return OperationResult.Ok(_statusMessage);
        }
        catch (Exception exception)
        {
            var message = $"カーソルを復元できませんでした: {exception.Message}";
            ReportError(message);
            return OperationResult.Fail(message);
        }
    }

    public OperationResult RestoreWindowsDefault()
    {
        ThrowIfDisposed();

        var disableResult = DisableRotation();
        if (!disableResult.Success)
        {
            return disableResult;
        }

        try
        {
            _cursorRegistry.RestoreWindowsDefault();
            _activePresetId = null;
            _activePresetName = "Windows 標準";
            _statusMessage = "Windows 標準カーソルへ戻しました";
            RaiseStateChanged();
            return OperationResult.Ok(_statusMessage);
        }
        catch (Exception exception)
        {
            var message = $"Windows 標準カーソルへ戻せませんでした: {exception.Message}";
            ReportError(message);
            return OperationResult.Fail(message);
        }
    }

    public OperationResult SaveCurrentAsBaseline()
    {
        ThrowIfDisposed();

        try
        {
            _baseline = _cursorRegistry.Capture();
            _baselineStore.Save(_baseline);
            _statusMessage = "現在のカーソルを復元先として保存しました";
            RaiseStateChanged();
            return OperationResult.Ok(_statusMessage);
        }
        catch (Exception exception)
        {
            var message = $"復元用カーソルを保存できませんでした: {exception.Message}";
            ReportError(message);
            return OperationResult.Fail(message);
        }
    }

    private void ConfigureRotation(bool switchImmediately)
    {
        var selectedGroup = _settings.GetSelectedGroup();
        if (selectedGroup is null || selectedGroup.Presets.Count == 0)
        {
            _rotationEngine.Configure(
                false,
                _settings.IntervalSeconds,
                _settings.SelectionMode,
                [],
                _activePresetId,
                false);

            _statusMessage = "切り替え対象のカーソルがありません";
            return;
        }

        _statusMessage = "次の自動切り替えを待機中";
        _rotationEngine.Configure(
            true,
            _settings.IntervalSeconds,
            _settings.SelectionMode,
            selectedGroup.Presets,
            _activePresetId,
            switchImmediately);
    }

    private void HandlePresetDue(CursorPreset preset)
    {
        ApplyPresetCore(preset, automatic: true);
        RaiseStateChanged();
    }

    private OperationResult ApplyPresetCore(CursorPreset preset, bool automatic)
    {
        if (!EnsureBaseline())
        {
            return OperationResult.Fail("復元用カーソルを保存できないため、切り替えを中止しました。");
        }

        var scan = _folderScanner.Scan(preset);
        if (!scan.IsValid)
        {
            var detail = scan.Warnings.FirstOrDefault() ?? "カーソル一式を検出できません。";
            var invalidMessage = $"「{preset.Name}」を適用できません: {detail}";
            _statusMessage = invalidMessage;
            ReportError(invalidMessage);
            return OperationResult.Fail(invalidMessage);
        }

        try
        {
            _cursorRegistry.Apply(scan);
            _activePresetId = preset.Id;
            _activePresetName = preset.Name;
            _settings.LastActivePresetId = preset.Id;
            _statusMessage = automatic
                ? $"自動切り替え: {preset.Name}"
                : $"今すぐ切り替え: {preset.Name}";

            try
            {
                _settingsStore.Save(_settings);
            }
            catch (Exception saveException)
            {
                ReportError($"現在のカーソル状態を保存できませんでした: {saveException.Message}");
            }

            return OperationResult.Ok(_statusMessage);
        }
        catch (Exception exception)
        {
            var message = $"「{preset.Name}」へ切り替えられませんでした: {exception.Message}";
            _statusMessage = message;
            ReportError(message);
            return OperationResult.Fail(message);
        }
    }

    private OperationResult DisableRotation()
    {
        if (!_settings.IsRotationEnabled)
        {
            _rotationEngine.Stop();
            return OperationResult.Ok();
        }

        var updated = _settings.DeepClone();
        updated.IsRotationEnabled = false;
        return UpdateSettings(updated);
    }

    private bool EnsureBaseline()
    {
        if (_baseline is not null)
        {
            return true;
        }

        _baseline = _baselineStore.Load();
        if (_baseline is not null)
        {
            return true;
        }

        try
        {
            _baseline = _cursorRegistry.Capture();
            _baselineStore.Save(_baseline);
            return true;
        }
        catch (Exception exception)
        {
            _baseline = null;
            ReportError($"復元用カーソルを保存できませんでした: {exception.Message}");
            return false;
        }
    }

    private CursorPreset? FindPreset(Guid? presetId)
    {
        if (presetId is null)
        {
            return null;
        }

        return _settings.Groups
            .SelectMany(group => group.Presets)
            .FirstOrDefault(preset => preset.Id == presetId);
    }

    private void ReportError(string message)
    {
        ErrorOccurred?.Invoke(message);
    }

    private void RaiseStateChanged()
    {
        if (_disposed)
        {
            return;
        }

        StateChanged?.Invoke(GetState());
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
        _rotationEngine.PresetDue -= HandlePresetDue;
        _rotationEngine.ScheduleChanged -= RaiseStateChanged;
        _rotationEngine.Dispose();
    }
}
