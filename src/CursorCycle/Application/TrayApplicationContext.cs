using CursorCycle.Domain;
using CursorCycle.Infrastructure;
using CursorCycle.UI;

namespace CursorCycle.Application;

public sealed class TrayApplicationContext : ApplicationContext
{
    private readonly AppController _controller = new();
    private readonly Control _dispatcher = new();
    private readonly ContextMenuStrip _trayMenu = new();
    private readonly NotifyIcon _trayIcon;
    private readonly UpdateService _updateService = new();
    private readonly CancellationTokenSource _updateCancellation = new();

    private SettingsForm? _settingsForm;
    private bool _isExiting;
    private bool _disposed;

    public TrayApplicationContext(bool showSettingsOnStart, bool checkForUpdates)
    {
        DiagnosticLogger.Write("TrayApplicationContext construction started.");
        _dispatcher.CreateControl();
        _ = _dispatcher.Handle;
        DiagnosticLogger.Write("UI dispatcher handle created.");

        _trayMenu.Opening += (_, _) => RebuildTrayMenu();

        _trayIcon = new NotifyIcon
        {
            Icon = Icon.ExtractAssociatedIcon(System.Windows.Forms.Application.ExecutablePath)
                ?? SystemIcons.Application,
            Text = "OshiCursour",
            ContextMenuStrip = _trayMenu,
            Visible = true
        };
        DiagnosticLogger.Write("Notification-area icon created.");

        _trayIcon.DoubleClick += (_, _) => ShowSettingsWindow();
        _controller.StateChanged += HandleStateChanged;
        _controller.ErrorOccurred += HandleError;
        _controller.Initialize();
        RebuildTrayMenu();
        DiagnosticLogger.Write("AppController initialized.");

        if (showSettingsOnStart)
        {
            DiagnosticLogger.Write("Opening settings window after manual launch.");
            ShowSettingsWindow();
        }

        if (checkForUpdates)
        {
            _dispatcher.BeginInvoke(new Action(() => _ = CheckForUpdatesAsync()));
        }
    }

    private async Task CheckForUpdatesAsync()
    {
        UpdateInfo? update;
        try
        {
            DiagnosticLogger.Write("Checking GitHub Releases for updates.");
            update = await _updateService.CheckAsync(_updateCancellation.Token);
        }
        catch (OperationCanceledException)
        {
            return;
        }
        catch (Exception exception)
        {
            // ネットワーク未接続やGitHub側の一時的な障害は、起動を妨げずログだけに残す。
            DiagnosticLogger.WriteException("CheckForUpdatesAsync.Check", exception);
            return;
        }

        if (update is null || _disposed || _isExiting)
        {
            return;
        }

        try
        {
            var current = typeof(TrayApplicationContext).Assembly.GetName().Version;
            var confirmation = MessageBox.Show(
                _settingsForm,
                $"新しいバージョン {update.Version.ToString(3)} が利用できます。\n" +
                $"現在のバージョン: {current?.ToString(3) ?? "不明"}\n\n" +
                "更新ファイルをダウンロードして再起動しますか？",
                "OshiCursour 更新",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Information);

            if (confirmation != DialogResult.Yes || _disposed || _isExiting)
            {
                return;
            }

            _trayIcon.BalloonTipTitle = "OshiCursour 更新";
            _trayIcon.BalloonTipText = "更新ファイルをダウンロードしています。";
            _trayIcon.BalloonTipIcon = ToolTipIcon.Info;
            _trayIcon.ShowBalloonTip(3000);

            var prepared = await _updateService.DownloadAndPrepareAsync(
                update,
                _updateCancellation.Token);
            _updateService.LaunchInstaller(prepared);
            DiagnosticLogger.Write(
                $"Updater launched for version {update.Version.ToString(3)}.");
            ExitApplication();
        }
        catch (OperationCanceledException)
        {
            // アプリ終了時のキャンセル。
        }
        catch (Exception exception)
        {
            DiagnosticLogger.WriteException("CheckForUpdatesAsync.Apply", exception);
            if (!_disposed && !_isExiting)
            {
                MessageBox.Show(
                    _settingsForm,
                    "更新をダウンロードまたは適用できませんでした。\n\n" + exception.Message,
                    "OshiCursour 更新エラー",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
            }
        }
    }

    public void RequestShowSettings()
    {
        if (_disposed || _dispatcher.IsDisposed)
        {
            return;
        }

        if (_dispatcher.InvokeRequired)
        {
            _dispatcher.BeginInvoke(new Action(ShowSettingsWindow));
            return;
        }

        ShowSettingsWindow();
    }

    private void RebuildTrayMenu()
    {
        var state = _controller.GetState();

        foreach (var existingItem in _trayMenu.Items.Cast<ToolStripItem>().ToArray())
        {
            existingItem.Dispose();
        }

        _trayMenu.Items.Clear();

        var statusItem = new ToolStripMenuItem(
            state.Settings.IsRotationEnabled
                ? $"● ON － {state.ActivePresetName}"
                : $"○ OFF － {state.ActivePresetName}")
        {
            Enabled = false
        };

        _trayMenu.Items.Add(statusItem);
        _trayMenu.Items.Add(new ToolStripMenuItem(
            "設定を開く",
            null,
            (_, _) => ShowSettingsWindow()));
        _trayMenu.Items.Add(new ToolStripSeparator());

        var toggleItem = new ToolStripMenuItem(
            state.Settings.IsRotationEnabled
                ? "自動切り替えを OFF"
                : "自動切り替えを ON",
            null,
            (_, _) => _controller.SetRotationEnabled(!state.Settings.IsRotationEnabled));

        _trayMenu.Items.Add(toggleItem);

        var groupsMenu = new ToolStripMenuItem("使用するグループ");
        foreach (var group in state.Settings.Groups)
        {
            var groupItem = new ToolStripMenuItem(group.Name)
            {
                Checked = group.Id == state.Settings.SelectedGroupId
            };

            var groupId = group.Id;
            groupItem.Click += (_, _) => _controller.SelectGroup(groupId);
            groupsMenu.DropDownItems.Add(groupItem);
        }

        if (groupsMenu.DropDownItems.Count == 0)
        {
            groupsMenu.Enabled = false;
        }

        _trayMenu.Items.Add(groupsMenu);

        var quickSwitchMenu = new ToolStripMenuItem("今すぐ切り替え");
        var selectedGroup = state.Settings.GetSelectedGroup();
        if (selectedGroup is not null)
        {
            foreach (var preset in selectedGroup.Presets)
            {
                var presetItem = new ToolStripMenuItem(preset.Name)
                {
                    Checked = preset.Id == state.ActivePresetId
                };

                var presetId = preset.Id;
                presetItem.Click += (_, _) => _controller.ApplyPresetNow(presetId);
                quickSwitchMenu.DropDownItems.Add(presetItem);
            }
        }

        if (quickSwitchMenu.DropDownItems.Count == 0)
        {
            quickSwitchMenu.Enabled = false;
        }

        _trayMenu.Items.Add(quickSwitchMenu);
        _trayMenu.Items.Add(new ToolStripSeparator());
        _trayMenu.Items.Add(new ToolStripMenuItem(
            "切り替え開始前のカーソルに戻す",
            null,
            (_, _) => _controller.RestoreBaseline()));
        _trayMenu.Items.Add(new ToolStripMenuItem(
            "Windows 標準カーソルに戻す",
            null,
            (_, _) => _controller.RestoreWindowsDefault()));
        _trayMenu.Items.Add(new ToolStripSeparator());
        _trayMenu.Items.Add(new ToolStripMenuItem(
            "終了（現在のカーソルを維持）",
            null,
            (_, _) => ExitApplication()));
        _trayMenu.Items.Add(new ToolStripMenuItem(
            "元に戻して終了",
            null,
            (_, _) => RestoreAndExit()));
    }

    private void ShowSettingsWindow()
    {
        DiagnosticLogger.Write("ShowSettingsWindow requested.");
        if (_disposed || _isExiting)
        {
            DiagnosticLogger.Write("ShowSettingsWindow ignored because the app is exiting.");
            return;
        }

        if (_settingsForm is not null && !_settingsForm.IsDisposed)
        {
            _settingsForm.WindowState = FormWindowState.Normal;
            _settingsForm.Show();
            _settingsForm.BringToFront();
            _settingsForm.Activate();
            return;
        }

        _settingsForm = new SettingsForm(_controller, ExitApplication);
        _settingsForm.FormClosing += HandleSettingsFormClosing;
        _settingsForm.Resize += HandleSettingsFormResize;
        _settingsForm.FormClosed += (_, _) => _settingsForm = null;
        _settingsForm.Show();
        _settingsForm.Activate();
        DiagnosticLogger.Write("Settings window shown.");
    }

    private void HandleSettingsFormClosing(object? sender, FormClosingEventArgs eventArgs)
    {
        if (_isExiting || eventArgs.CloseReason != CloseReason.UserClosing)
        {
            return;
        }

        eventArgs.Cancel = true;
        _dispatcher.BeginInvoke(new Action(HideSettingsWindow));
    }

    private void HandleSettingsFormResize(object? sender, EventArgs eventArgs)
    {
        if (_isExiting || _settingsForm?.WindowState != FormWindowState.Minimized)
        {
            return;
        }

        _dispatcher.BeginInvoke(new Action(HideSettingsWindow));
    }

    private void HideSettingsWindow()
    {
        if (_settingsForm is null || _settingsForm.IsDisposed)
        {
            return;
        }

        if (!_settingsForm.SavePendingSettings(silent: true))
        {
            _settingsForm.WindowState = FormWindowState.Normal;
            _settingsForm.Show();
            _settingsForm.Activate();
            return;
        }

        var form = _settingsForm;
        _settingsForm = null;
        form.Hide();
        form.Dispose();
    }

    private void HandleStateChanged(AppStateSnapshot state)
    {
        var tooltip = state.Settings.IsRotationEnabled
            ? $"OshiCursour: ON / {state.ActivePresetName}"
            : $"OshiCursour: OFF / {state.ActivePresetName}";

        _trayIcon.Text = tooltip.Length <= 63 ? tooltip : tooltip[..63];
    }

    private void HandleError(string message)
    {
        DiagnosticLogger.Write($"Controller error: {message}");
        if (_disposed)
        {
            return;
        }

        _trayIcon.BalloonTipTitle = "OshiCursour";
        _trayIcon.BalloonTipText = message;
        _trayIcon.BalloonTipIcon = ToolTipIcon.Error;
        _trayIcon.ShowBalloonTip(5000);
    }

    private void RestoreAndExit()
    {
        var result = _controller.RestoreBaseline();
        if (result.Success)
        {
            ExitApplication();
        }
    }

    private void ExitApplication()
    {
        if (_isExiting)
        {
            return;
        }

        _isExiting = true;

        if (_settingsForm is not null && !_settingsForm.IsDisposed)
        {
            // Close() による FormClosed イベントで _settingsForm は null になるため、
            // ローカル変数を保持してから終了処理を行う。
            var form = _settingsForm;
            _settingsForm = null;
            form.SavePendingSettings(silent: true);
            form.Close();
            form.Dispose();
        }

        ExitThread();
    }

    protected override void ExitThreadCore()
    {
        if (!_disposed)
        {
            _disposed = true;
            _updateCancellation.Cancel();
            _trayIcon.Visible = false;
            _controller.StateChanged -= HandleStateChanged;
            _controller.ErrorOccurred -= HandleError;
            _controller.Dispose();
            _trayMenu.Dispose();
            _trayIcon.Dispose();
            _dispatcher.Dispose();
            _updateCancellation.Dispose();
        }

        base.ExitThreadCore();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing && !_disposed)
        {
            ExitThreadCore();
        }

        base.Dispose(disposing);
    }
}
