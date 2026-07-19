using CursorCycle.Application;
using CursorCycle.Domain;
using CursorCycle.Infrastructure;

namespace CursorCycle.UI;

public sealed class SettingsForm : Form
{
    private static readonly Color PageBackground = Color.FromArgb(246, 248, 252);
    private static readonly Color SurfaceColor = Color.White;
    private static readonly Color BorderColor = Color.FromArgb(222, 228, 238);
    private static readonly Color TextColor = Color.FromArgb(31, 42, 68);
    private static readonly Color MutedTextColor = Color.FromArgb(104, 116, 139);
    private static readonly Color PrimaryColor = Color.FromArgb(71, 192, 220);
    private static readonly Color PrimaryDarkColor = Color.FromArgb(21, 46, 103);
    private static readonly Color AccentColor = Color.FromArgb(177, 145, 235);

    private readonly AppController _controller;
    private readonly Action _exitApplication;
    private AppSettings _draft;

    private readonly Label _statusLabel = new();
    private readonly Label _activePresetLabel = new();
    private readonly Label _nextSwitchLabel = new();
    private readonly PictureBox _activeCursorThumbnail = new();
    private readonly Button _toggleButton = new();
    private readonly ComboBox _groupCombo = new();
    private readonly ComboBox _modeCombo = new();
    private readonly NumericUpDown _intervalValue = new();
    private readonly ComboBox _intervalUnitCombo = new();
    private readonly ComboBox _quickPresetCombo = new();
    private readonly CheckBox _startWithWindowsCheckBox = new();
    private readonly ThemeSwitch _themeSwitch = new();
    private readonly Label _saveFeedbackLabel = new();

    private readonly ListBox _groupList = new();
    private readonly DataGridView _presetsGrid = new();
    private readonly Button _generalNavButton = new();
    private readonly Button _groupsNavButton = new();
    private Control? _generalPage;
    private Control? _groupsPage;
    private int _selectedPageIndex;

    private bool _loading;
    private Guid? _thumbnailPresetId;
    private string? _thumbnailFolderPath;
    private Image? _headerImage;
    private bool _isDarkMode;

    private Color CurrentPageBackground => _isDarkMode
        ? Color.FromArgb(20, 24, 34)
        : PageBackground;
    private Color CurrentSurfaceColor => _isDarkMode
        ? Color.FromArgb(31, 37, 50)
        : SurfaceColor;
    private Color CurrentBorderColor => _isDarkMode
        ? Color.FromArgb(61, 71, 91)
        : BorderColor;
    private Color CurrentTextColor => _isDarkMode
        ? Color.FromArgb(232, 237, 247)
        : TextColor;
    private Color CurrentMutedTextColor => _isDarkMode
        ? Color.FromArgb(166, 177, 199)
        : MutedTextColor;
    private Color CurrentPrimaryTextColor => _isDarkMode
        ? Color.FromArgb(127, 219, 237)
        : PrimaryDarkColor;

    public SettingsForm(AppController controller, Action exitApplication)
    {
        _controller = controller;
        _exitApplication = exitApplication;
        _draft = controller.GetState().Settings.DeepClone();

        Text = "OshiCursour 設定";
        Icon = Icon.ExtractAssociatedIcon(System.Windows.Forms.Application.ExecutablePath);
        StartPosition = FormStartPosition.CenterScreen;
        MinimumSize = new Size(1120, 720);
        ClientSize = new Size(1120, 720);
        FormBorderStyle = FormBorderStyle.FixedSingle;
        MaximizeBox = false;
        Font = new Font("Yu Gothic UI", 9.5F);
        BackColor = PageBackground;
        ForeColor = TextColor;
        ShowInTaskbar = true;

        BuildInterface();
        _controller.StateChanged += HandleControllerStateChanged;
        LoadDraftIntoControls();
        ApplyTheme(_draft.UseDarkMode);
        UpdateRuntimeState(_controller.GetState());
    }

    public bool SavePendingSettings(bool silent)
    {
        ReadGeneralControlsIntoDraft();
        _draft.Normalize();

        var result = _controller.UpdateSettings(_draft);
        if (!result.Success)
        {
            if (!silent)
            {
                MessageBox.Show(
                    this,
                    result.Message,
                    "設定の保存に失敗しました",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }

            return false;
        }

        _draft = _controller.GetState().Settings.DeepClone();
        LoadDraftIntoControls();
        _saveFeedbackLabel.Text = $"保存しました  {DateTime.Now:HH:mm:ss}";
        return true;
    }

    private void BuildInterface()
    {
        SuspendLayout();
        var header = BuildHeader();
        var footer = BuildFooter();
        _generalPage = BuildGeneralTab();
        _groupsPage = BuildGroupsTab();
        _generalPage.Dock = DockStyle.Fill;
        _groupsPage.Dock = DockStyle.Fill;

        var pageHost = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = PageBackground
        };
        pageHost.Controls.Add(_groupsPage);
        pageHost.Controls.Add(_generalPage);

        ConfigureNavigationButton(_generalNavButton, "基本設定", 0);
        ConfigureNavigationButton(_groupsNavButton, "カーソルグループ", 1);
        var navigation = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            Height = 44,
            WrapContents = false,
            FlowDirection = FlowDirection.LeftToRight,
            Margin = Padding.Empty,
            Padding = Padding.Empty,
            BackColor = PageBackground
        };
        navigation.Controls.Add(_generalNavButton);
        navigation.Controls.Add(_groupsNavButton);

        Controls.Add(pageHost);
        Controls.Add(navigation);
        Controls.Add(footer);
        Controls.Add(header);
        SelectPage(0);
        ResumeLayout();
    }

    private void ConfigureNavigationButton(Button button, string text, int pageIndex)
    {
        button.Text = text;
        button.Width = 150;
        button.Height = 44;
        button.Margin = Padding.Empty;
        button.FlatStyle = FlatStyle.Flat;
        button.FlatAppearance.BorderSize = 0;
        button.Font = new Font(Font, FontStyle.Bold);
        button.Cursor = Cursors.Hand;
        button.Tag = "theme-navigation";
        button.Click += (_, _) => SelectPage(pageIndex);
    }

    private void SelectPage(int pageIndex)
    {
        _selectedPageIndex = pageIndex;
        if (_generalPage is not null)
        {
            _generalPage.Visible = pageIndex == 0;
            if (_generalPage.Visible)
            {
                _generalPage.BringToFront();
            }
        }

        if (_groupsPage is not null)
        {
            _groupsPage.Visible = pageIndex == 1;
            if (_groupsPage.Visible)
            {
                _groupsPage.BringToFront();
            }
        }

        ApplyNavigationTheme();
    }

    private Control BuildHeader()
    {
        var header = new Panel
        {
            Dock = DockStyle.Top,
            Height = 78,
            BackColor = SurfaceColor,
            Padding = new Padding(22, 14, 22, 10)
        };
        header.Tag = "theme-surface";

        var appIcon = new PictureBox
        {
            Dock = DockStyle.Left,
            Width = 48,
            SizeMode = PictureBoxSizeMode.Zoom,
            Image = _headerImage = LoadEmbeddedAppImage(),
            Margin = new Padding(0, 0, 12, 0)
        };

        var title = new Label
        {
            AutoSize = true,
            Location = new Point(82, 13),
            Font = new Font(Font.FontFamily, 15F, FontStyle.Bold),
            ForeColor = PrimaryDarkColor,
            Text = "OshiCursour"
        };
        var subtitle = new Label
        {
            AutoSize = true,
            Location = new Point(84, 45),
            ForeColor = MutedTextColor,
            Text = "お気に入りのカーソルを、気分に合わせて切り替えよう"
        };
        var version = typeof(SettingsForm).Assembly.GetName().Version;
        var versionLabel = new Label
        {
            AutoSize = false,
            Width = 86,
            Height = 28,
            TextAlign = ContentAlignment.MiddleRight,
            Font = new Font(Font.FontFamily, 8F),
            ForeColor = MutedTextColor,
            Text = version is null
                ? string.Empty
                : $"Version {version.Major}.{version.Minor}.{version.Build}"
        };
        _themeSwitch.Width = 112;
        _themeSwitch.Height = 28;
        _themeSwitch.Margin = new Padding(0, 0, 4, 0);
        _themeSwitch.CheckedChanged += HandleThemeChanged;

        var headerActions = new FlowLayoutPanel
        {
            Dock = DockStyle.Right,
            Width = 202,
            Height = 54,
            WrapContents = false,
            FlowDirection = FlowDirection.LeftToRight,
            BackColor = Color.Transparent,
            Padding = new Padding(0, 18, 0, 0)
        };
        headerActions.Controls.Add(_themeSwitch);
        headerActions.Controls.Add(versionLabel);

        header.Paint += (_, eventArgs) =>
        {
            using var pen = new Pen(CurrentBorderColor);
            eventArgs.Graphics.DrawLine(pen, 0, header.Height - 1, header.Width, header.Height - 1);
        };
        header.Controls.Add(appIcon);
        header.Controls.Add(title);
        header.Controls.Add(subtitle);
        header.Controls.Add(headerActions);
        return header;
    }

    private Control BuildFooter()
    {
        var footer = new Panel
        {
            Dock = DockStyle.Bottom,
            Height = 70,
            Padding = new Padding(22, 14, 22, 14),
            BackColor = SurfaceColor
        };
        footer.Tag = "theme-surface";

        _saveFeedbackLabel.AutoSize = true;
        _saveFeedbackLabel.ForeColor = MutedTextColor;
        _saveFeedbackLabel.Location = new Point(24, 26);

        var closeButton = CreateButton("トレイに閉じる", 130);
        closeButton.Click += (_, _) => Close();

        var exitButton = CreateDangerButton("アプリを終了", 130);
        exitButton.Click += (_, _) => RequestExitApplication();

        var saveButton = CreatePrimaryButton("設定を保存", 130);
        saveButton.Click += (_, _) => SavePendingSettings(silent: false);

        var buttonPanel = new FlowLayoutPanel
        {
            Dock = DockStyle.Right,
            AutoSize = true,
            WrapContents = false,
            FlowDirection = FlowDirection.LeftToRight,
            Margin = Padding.Empty,
            Padding = Padding.Empty
        };
        saveButton.Margin = new Padding(0, 0, 8, 0);
        closeButton.Margin = new Padding(0, 0, 8, 0);
        exitButton.Margin = Padding.Empty;
        buttonPanel.Controls.Add(saveButton);
        buttonPanel.Controls.Add(closeButton);
        buttonPanel.Controls.Add(exitButton);

        footer.Controls.Add(_saveFeedbackLabel);
        footer.Controls.Add(buttonPanel);
        footer.Paint += (_, eventArgs) =>
        {
            using var pen = new Pen(CurrentBorderColor);
            eventArgs.Graphics.DrawLine(pen, 0, 0, footer.Width, 0);
        };
        AcceptButton = saveButton;
        return footer;
    }

    private void RequestExitApplication()
    {
        var confirmation = MessageBox.Show(
            this,
            "現在のカーソルを維持したまま OshiCursour を終了しますか？",
            "OshiCursour を終了",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Question);

        if (confirmation == DialogResult.Yes)
        {
            _exitApplication();
        }
    }

    private Control BuildGeneralTab()
    {
        var page = new Panel
        {
            Padding = new Padding(20, 18, 20, 18),
            BackColor = PageBackground
        };

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoScroll = true,
            ColumnCount = 1,
            RowCount = 3,
            BackColor = PageBackground,
            GrowStyle = TableLayoutPanelGrowStyle.AddRows
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 154F));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        layout.Controls.Add(BuildStatusGroup(), 0, 0);

        var settingsColumns = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 1,
            BackColor = PageBackground,
            Margin = Padding.Empty
        };
        settingsColumns.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 58F));
        settingsColumns.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 42F));

        var leftColumn = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = PageBackground,
            Padding = new Padding(0, 0, 6, 0)
        };
        leftColumn.Controls.Add(BuildResidentSettingsGroup());
        leftColumn.Controls.Add(BuildRotationSettingsGroup());

        var rightColumn = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = PageBackground,
            Padding = new Padding(6, 0, 0, 0)
        };
        var quickSwitch = BuildQuickSwitchGroup();
        quickSwitch.Dock = DockStyle.Fill;
        rightColumn.Controls.Add(quickSwitch);

        settingsColumns.Controls.Add(leftColumn, 0, 0);
        settingsColumns.Controls.Add(rightColumn, 1, 0);
        layout.Controls.Add(settingsColumns, 0, 1);

        var note = new Label
        {
            AutoSize = true,
            ForeColor = MutedTextColor,
            Margin = new Padding(8, 4, 8, 0),
            Text = "ヒント: × または最小化を押しても、OshiCursour は通知領域で動作を続けます。"
        };
        layout.Controls.Add(note, 0, 2);

        page.Controls.Add(layout);
        return page;
    }

    private Control BuildStatusGroup()
    {
        var group = CreateGroupBox("現在の状態", 142);
        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(18, 12, 18, 14),
            ColumnCount = 3,
            RowCount = 3
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 68F));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 180F));

        _activeCursorThumbnail.Dock = DockStyle.Fill;
        _activeCursorThumbnail.Margin = new Padding(0, 4, 14, 4);
        _activeCursorThumbnail.SizeMode = PictureBoxSizeMode.Zoom;
        _activeCursorThumbnail.BackColor = Color.Transparent;

        _statusLabel.AutoSize = true;
        _statusLabel.Font = new Font(Font.FontFamily, 12F, FontStyle.Bold);
        _statusLabel.ForeColor = PrimaryDarkColor;
        _statusLabel.Text = "待機中";

        _activePresetLabel.AutoSize = true;
        _activePresetLabel.ForeColor = MutedTextColor;

        _nextSwitchLabel.AutoSize = true;
        _nextSwitchLabel.ForeColor = MutedTextColor;

        _toggleButton.Dock = DockStyle.Fill;
        _toggleButton.Margin = new Padding(12, 2, 0, 2);
        _toggleButton.FlatStyle = FlatStyle.Flat;
        _toggleButton.FlatAppearance.BorderSize = 0;
        _toggleButton.Font = new Font(Font, FontStyle.Bold);
        _toggleButton.Cursor = Cursors.Hand;
        _toggleButton.Tag = "theme-toggle";
        _toggleButton.Click += ToggleRotation;

        layout.Controls.Add(_activeCursorThumbnail, 0, 0);
        layout.SetRowSpan(_activeCursorThumbnail, 3);
        layout.Controls.Add(_statusLabel, 1, 0);
        layout.Controls.Add(_activePresetLabel, 1, 1);
        layout.Controls.Add(_nextSwitchLabel, 1, 2);
        layout.Controls.Add(_toggleButton, 2, 0);
        layout.SetRowSpan(_toggleButton, 3);
        group.Controls.Add(layout);
        return group;
    }

    private Control BuildRotationSettingsGroup()
    {
        var group = CreateGroupBox("自動切り替え", 200);
        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(18, 10, 18, 14),
            ColumnCount = 2,
            RowCount = 3
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 150F));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));

        ConfigureDropDown(_groupCombo);
        _groupCombo.SelectedIndexChanged += HandleGeneralGroupChanged;

        ConfigureDropDown(_modeCombo);
        _modeCombo.Items.AddRange(
        [
            new ModeChoice("ランダム（連続で同じものは選ばない）", CursorSelectionMode.Random),
            new ModeChoice("上から順番", CursorSelectionMode.Sequential)
        ]);

        _intervalValue.Minimum = 1;
        _intervalValue.Maximum = AppSettings.MaximumIntervalSeconds;
        _intervalValue.Width = 130;
        _intervalValue.TextAlign = HorizontalAlignment.Right;

        ConfigureDropDown(_intervalUnitCombo, compact: true);
        _intervalUnitCombo.Width = 90;
        _intervalUnitCombo.Items.AddRange(
        [
            new IntervalUnitChoice("秒", 1),
            new IntervalUnitChoice("分", 60),
            new IntervalUnitChoice("時間", 3600)
        ]);
        _intervalUnitCombo.SelectedIndexChanged += HandleIntervalUnitChanged;

        var intervalPanel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoSize = true,
            ColumnCount = 3,
            RowCount = 1,
            Margin = Padding.Empty,
            Padding = Padding.Empty
        };
        intervalPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 130F));
        intervalPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 8F));
        intervalPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 90F));
        _intervalValue.Dock = DockStyle.Fill;
        _intervalValue.Margin = Padding.Empty;
        _intervalUnitCombo.Dock = DockStyle.Fill;
        _intervalUnitCombo.Margin = Padding.Empty;
        intervalPanel.Controls.Add(_intervalValue, 0, 0);
        intervalPanel.Controls.Add(_intervalUnitCombo, 2, 0);

        AddSettingRow(layout, 0, "カーソルグループ", _groupCombo);
        AddSettingRow(layout, 1, "切り替え方法", _modeCombo);
        AddSettingRow(layout, 2, "切り替え間隔", intervalPanel);
        group.Controls.Add(layout);
        return group;
    }

    private Control BuildQuickSwitchGroup()
    {
        var group = CreateGroupBox("今すぐ切り替える・元に戻す", 280);
        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(18, 12, 18, 16),
            ColumnCount = 1,
            RowCount = 5
        };
        for (var row = 0; row < 5; row++)
        {
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 20F));
        }

        ConfigureDropDown(_quickPresetCombo);
        _quickPresetCombo.Dock = DockStyle.Fill;
        _quickPresetCombo.Margin = new Padding(0, 0, 0, 8);

        var applyNowButton = CreatePrimaryButton("今すぐ切り替え", 180);
        applyNowButton.Dock = DockStyle.Fill;
        applyNowButton.Margin = new Padding(0, 0, 0, 8);
        applyNowButton.Click += ApplyQuickPreset;

        var restoreBaselineButton = CreateButton("開始前の状態に戻す", 180);
        restoreBaselineButton.Dock = DockStyle.Fill;
        restoreBaselineButton.Margin = new Padding(0, 0, 0, 8);
        restoreBaselineButton.Click += (_, _) => RestoreBaseline();

        var restoreWindowsButton = CreateButton("Windows 標準に戻す", 180);
        restoreWindowsButton.Dock = DockStyle.Fill;
        restoreWindowsButton.Margin = new Padding(0, 0, 0, 8);
        restoreWindowsButton.Click += (_, _) => RestoreWindowsDefault();

        var saveBaselineButton = CreateButton("現在を復元先に保存", 180);
        saveBaselineButton.Dock = DockStyle.Fill;
        saveBaselineButton.Margin = Padding.Empty;
        saveBaselineButton.Click += (_, _) => SaveCurrentAsBaseline();

        layout.Controls.Add(_quickPresetCombo, 0, 0);
        layout.Controls.Add(applyNowButton, 0, 1);
        layout.Controls.Add(restoreBaselineButton, 0, 2);
        layout.Controls.Add(restoreWindowsButton, 0, 3);
        layout.Controls.Add(saveBaselineButton, 0, 4);
        group.Controls.Add(layout);
        return group;
    }

    private Control BuildResidentSettingsGroup()
    {
        var group = CreateGroupBox("Windows 起動時の動作", 86);
        group.Dock = DockStyle.Bottom;
        _startWithWindowsCheckBox.AutoSize = true;
        _startWithWindowsCheckBox.Text = "Windows へのサインイン時に自動で起動する";
        _startWithWindowsCheckBox.Location = new Point(20, 38);
        group.Controls.Add(_startWithWindowsCheckBox);
        return group;
    }

    private Control BuildGroupsTab()
    {
        var page = new Panel
        {
            Padding = new Padding(20, 18, 20, 18),
            BackColor = PageBackground
        };

        var split = new SplitContainer
        {
            Dock = DockStyle.Fill,
            FixedPanel = FixedPanel.Panel1,
            BackColor = PageBackground,
            SplitterWidth = 12
        };

        // SplitContainer は画面へ配置される前だと幅が非常に小さい。
        // その時点で SplitterDistance / PanelMinSize を固定すると、
        // 実際の幅と矛盾して ArgumentOutOfRangeException になるため、
        // レイアウト後に現在幅から安全な位置を算出する。
        var splitterInitialized = false;
        split.SizeChanged += (_, _) =>
        {
            if (splitterInitialized)
            {
                return;
            }

            // 画面構築直後の仮サイズでは確定させない。
            if (split.ClientSize.Width < 600)
            {
                return;
            }

            const int preferredLeftWidth = 245;
            const int safeRightWidth = 420;
            var minimum = split.Panel1MinSize;
            var maximum = split.ClientSize.Width - split.SplitterWidth - safeRightWidth;

            if (maximum < minimum)
            {
                return;
            }

            split.SplitterDistance = Math.Clamp(preferredLeftWidth, minimum, maximum);
            splitterInitialized = true;
        };

        split.Panel1.Padding = new Padding(0, 0, 2, 0);
        split.Panel2.Padding = new Padding(2, 0, 0, 0);
        split.Panel1.Controls.Add(BuildGroupListPanel());
        split.Panel2.Controls.Add(BuildPresetListPanel());
        page.Controls.Add(split);
        return page;
    }

    private Control BuildGroupListPanel()
    {
        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 3,
            Padding = new Padding(16),
            BackColor = SurfaceColor,
            CellBorderStyle = TableLayoutPanelCellBorderStyle.None
        };
        layout.Tag = "theme-surface";
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        var title = CreateSectionTitle("グループ");

        _groupList.Dock = DockStyle.Fill;
        _groupList.IntegralHeight = false;
        _groupList.BorderStyle = BorderStyle.None;
        _groupList.BackColor = Color.FromArgb(249, 250, 253);
        _groupList.Font = new Font(Font.FontFamily, 10F);
        _groupList.SelectedIndexChanged += HandleEditorGroupChanged;

        var buttons = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoSize = true,
            ColumnCount = 2,
            RowCount = 2,
            Padding = new Padding(0, 10, 0, 0)
        };
        buttons.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
        buttons.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
        buttons.RowStyles.Add(new RowStyle(SizeType.Absolute, 42F));
        buttons.RowStyles.Add(new RowStyle(SizeType.Absolute, 42F));

        var addButton = CreatePrimaryButton("＋ 追加", 82);
        addButton.Dock = DockStyle.Fill;
        addButton.Margin = new Padding(0, 0, 5, 6);
        addButton.Click += (_, _) => AddGroup();
        var renameButton = CreateButton("名前変更", 82);
        renameButton.Dock = DockStyle.Fill;
        renameButton.Margin = Padding.Empty;
        renameButton.Click += (_, _) => RenameGroup();
        var deleteButton = CreateDangerButton("削除", 68);
        deleteButton.Dock = DockStyle.Fill;
        deleteButton.Margin = new Padding(5, 0, 0, 6);
        deleteButton.Click += (_, _) => DeleteGroup();

        buttons.Controls.Add(addButton, 0, 0);
        buttons.Controls.Add(deleteButton, 1, 0);
        buttons.Controls.Add(renameButton, 0, 1);
        buttons.SetColumnSpan(renameButton, 2);
        layout.Controls.Add(title, 0, 0);
        layout.Controls.Add(_groupList, 0, 1);
        layout.Controls.Add(buttons, 0, 2);
        return layout;
    }

    private Control BuildPresetListPanel()
    {
        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 3,
            Padding = new Padding(16),
            BackColor = SurfaceColor
        };
        layout.Tag = "theme-surface";
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 132F));

        var title = CreateSectionTitle("グループ内のカーソル（上から順番）");
        ConfigurePresetGrid();

        var actions = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 3,
            RowCount = 3,
            Padding = new Padding(0, 10, 0, 0)
        };
        actions.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.333F));
        actions.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.333F));
        actions.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.334F));
        actions.RowStyles.Add(new RowStyle(SizeType.Percent, 33.333F));
        actions.RowStyles.Add(new RowStyle(SizeType.Percent, 33.333F));
        actions.RowStyles.Add(new RowStyle(SizeType.Percent, 33.334F));

        var addFolderButton = CreatePrimaryButton("＋ 追加", 120);
        addFolderButton.Click += (_, _) => AddPresetFolder();
        var changeFolderButton = CreateButton("フォルダー変更", 120);
        changeFolderButton.Click += (_, _) => ChangePresetFolder();
        var openFolderButton = CreateButton("フォルダーを開く", 120);
        openFolderButton.Click += (_, _) => OpenPresetFolder();
        var renameButton = CreateButton("名前変更", 120);
        renameButton.Click += (_, _) => RenamePreset();
        var removeButton = CreateDangerButton("削除", 120);
        removeButton.Click += (_, _) => DeletePreset();
        var upButton = CreateButton("↑ 上へ", 120);
        upButton.Click += (_, _) => MovePreset(-1);
        var downButton = CreateButton("↓ 下へ", 120);
        downButton.Click += (_, _) => MovePreset(1);
        foreach (var button in new[]
                 {
                     addFolderButton, removeButton, renameButton, changeFolderButton,
                     openFolderButton, upButton, downButton
                 })
        {
            button.Dock = DockStyle.Fill;
            button.Margin = new Padding(4, 3, 4, 3);
        }

        actions.Controls.Add(addFolderButton, 0, 0);
        actions.Controls.Add(removeButton, 1, 0);
        actions.Controls.Add(renameButton, 0, 1);
        actions.Controls.Add(changeFolderButton, 1, 1);
        actions.Controls.Add(openFolderButton, 2, 1);
        actions.Controls.Add(upButton, 0, 2);
        actions.Controls.Add(downButton, 1, 2);

        layout.Controls.Add(title, 0, 0);
        layout.Controls.Add(_presetsGrid, 0, 1);
        layout.Controls.Add(actions, 0, 2);
        return layout;
    }

    private void ConfigurePresetGrid()
    {
        _presetsGrid.Dock = DockStyle.Fill;
        _presetsGrid.AllowUserToAddRows = false;
        _presetsGrid.AllowUserToDeleteRows = false;
        _presetsGrid.AllowUserToResizeRows = false;
        _presetsGrid.AutoGenerateColumns = false;
        _presetsGrid.BackgroundColor = SurfaceColor;
        _presetsGrid.BorderStyle = BorderStyle.None;
        _presetsGrid.CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal;
        _presetsGrid.GridColor = BorderColor;
        _presetsGrid.EnableHeadersVisualStyles = false;
        _presetsGrid.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(237, 244, 250);
        _presetsGrid.ColumnHeadersDefaultCellStyle.ForeColor = PrimaryDarkColor;
        _presetsGrid.ColumnHeadersDefaultCellStyle.Font = new Font(Font, FontStyle.Bold);
        _presetsGrid.ColumnHeadersDefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
        _presetsGrid.ColumnHeadersDefaultCellStyle.SelectionBackColor = Color.FromArgb(237, 244, 250);
        _presetsGrid.ColumnHeadersDefaultCellStyle.SelectionForeColor = PrimaryDarkColor;
        _presetsGrid.DefaultCellStyle.BackColor = SurfaceColor;
        _presetsGrid.DefaultCellStyle.ForeColor = TextColor;
        _presetsGrid.DefaultCellStyle.SelectionBackColor = Color.FromArgb(221, 244, 250);
        _presetsGrid.DefaultCellStyle.SelectionForeColor = PrimaryDarkColor;
        _presetsGrid.DefaultCellStyle.Padding = new Padding(4, 2, 4, 2);
        _presetsGrid.RowTemplate.Height = 34;
        _presetsGrid.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing;
        _presetsGrid.ColumnHeadersHeight = 36;
        _presetsGrid.MultiSelect = false;
        _presetsGrid.ReadOnly = true;
        _presetsGrid.RowHeadersVisible = false;
        _presetsGrid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
        _presetsGrid.CellContentClick += HandlePresetGridContentClick;
        _presetsGrid.Columns.Add(new DataGridViewTextBoxColumn
        {
            HeaderText = "順番",
            Width = 58,
            SortMode = DataGridViewColumnSortMode.NotSortable,
            DefaultCellStyle = new DataGridViewCellStyle
            {
                Alignment = DataGridViewContentAlignment.MiddleCenter
            }
        });
        _presetsGrid.Columns.Add(new DataGridViewImageColumn
        {
            HeaderText = "画像",
            Width = 54,
            ImageLayout = DataGridViewImageCellLayout.Zoom,
            SortMode = DataGridViewColumnSortMode.NotSortable,
            DefaultCellStyle = new DataGridViewCellStyle { NullValue = null }
        });
        _presetsGrid.Columns.Add(new DataGridViewTextBoxColumn
        {
            HeaderText = "名前",
            Width = 130,
            SortMode = DataGridViewColumnSortMode.NotSortable
        });
        _presetsGrid.Columns.Add(new DataGridViewTextBoxColumn
        {
            HeaderText = "フォルダー",
            AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill,
            MinimumWidth = 220,
            SortMode = DataGridViewColumnSortMode.NotSortable
        });
        _presetsGrid.Columns.Add(new DataGridViewTextBoxColumn
        {
            HeaderText = "検出状態",
            Width = 130,
            SortMode = DataGridViewColumnSortMode.NotSortable
        });
        _presetsGrid.Columns.Add(new DataGridViewButtonColumn
        {
            HeaderText = "一覧",
            Text = "開く",
            UseColumnTextForButtonValue = true,
            Width = 94,
            FlatStyle = FlatStyle.Standard,
            SortMode = DataGridViewColumnSortMode.NotSortable,
            DefaultCellStyle = new DataGridViewCellStyle
            {
                Alignment = DataGridViewContentAlignment.MiddleCenter,
                Padding = new Padding(8, 4, 8, 4),
                BackColor = Color.FromArgb(237, 244, 250),
                ForeColor = PrimaryDarkColor,
                SelectionBackColor = Color.FromArgb(221, 244, 250),
                SelectionForeColor = PrimaryDarkColor
            }
        });
    }

    private void LoadDraftIntoControls()
    {
        _loading = true;
        try
        {
            RefreshGroupSources(_draft.SelectedGroupId);

            var mode = _modeCombo.Items
                .OfType<ModeChoice>()
                .FirstOrDefault(item => item.Mode == _draft.SelectionMode);
            _modeCombo.SelectedItem = mode;

            SetIntervalControls(_draft.IntervalSeconds);
            _startWithWindowsCheckBox.Checked = _draft.StartWithWindows;
            _themeSwitch.Checked = _draft.UseDarkMode;
        }
        finally
        {
            _loading = false;
        }

        RefreshQuickPresetCombo();
        RefreshPresetGrid();
    }

    private void RefreshGroupSources(Guid? selectedGroupId)
    {
        var oldLoading = _loading;
        _loading = true;
        try
        {
            _groupCombo.Items.Clear();
            _groupList.Items.Clear();

            foreach (var group in _draft.Groups)
            {
                _groupCombo.Items.Add(group);
                _groupList.Items.Add(group);
            }

            var selectedIndex = selectedGroupId is null
                ? -1
                : _draft.Groups.FindIndex(group => group.Id == selectedGroupId);

            if (selectedIndex < 0 && _draft.Groups.Count > 0)
            {
                selectedIndex = 0;
            }

            _groupCombo.SelectedIndex = selectedIndex;
            _groupList.SelectedIndex = selectedIndex;
            _draft.SelectedGroupId = selectedIndex >= 0
                ? _draft.Groups[selectedIndex].Id
                : null;
        }
        finally
        {
            _loading = oldLoading;
        }
    }

    private void RefreshQuickPresetCombo()
    {
        var activePresetId = _controller.GetState().ActivePresetId;
        var selectedGroup = _draft.GetSelectedGroup();

        var oldLoading = _loading;
        _loading = true;
        try
        {
            _quickPresetCombo.Items.Clear();
            if (selectedGroup is null)
            {
                return;
            }

            foreach (var preset in selectedGroup.Presets)
            {
                _quickPresetCombo.Items.Add(preset);
            }

            var selectedIndex = selectedGroup.Presets.FindIndex(
                preset => preset.Id == activePresetId);
            _quickPresetCombo.SelectedIndex = selectedIndex >= 0
                ? selectedIndex
                : (_quickPresetCombo.Items.Count > 0 ? 0 : -1);
        }
        finally
        {
            _loading = oldLoading;
        }
    }

    private void RefreshPresetGrid(Guid? selectPresetId = null)
    {
        DisposePresetThumbnails();
        _presetsGrid.Rows.Clear();
        var group = GetEditingGroup();
        if (group is null)
        {
            return;
        }

        for (var presetIndex = 0; presetIndex < group.Presets.Count; presetIndex++)
        {
            var preset = group.Presets[presetIndex];
            var scan = _controller.ScanPreset(preset);
            var status = scan.IsValid
                ? $"{scan.MatchedRoleCount}/{CursorRoles.All.Count} 種類"
                : scan.Warnings.FirstOrDefault() ?? "未検出";
            var thumbnail = LoadArrowThumbnail(scan);

            var rowIndex = _presetsGrid.Rows.Add(
                presetIndex + 1,
                thumbnail,
                preset.Name,
                preset.FolderPath,
                status,
                "開く");

            var row = _presetsGrid.Rows[rowIndex];
            row.Tag = preset;
            if (!scan.IsValid)
            {
                row.DefaultCellStyle.ForeColor = Color.Firebrick;
            }

            if (preset.Id == selectPresetId)
            {
                row.Selected = true;
                _presetsGrid.CurrentCell = row.Cells[0];
            }
        }

        if (selectPresetId is null && _presetsGrid.Rows.Count > 0)
        {
            _presetsGrid.Rows[0].Selected = true;
            _presetsGrid.CurrentCell = _presetsGrid.Rows[0].Cells[0];
        }
    }

    private void HandlePresetGridContentClick(object? sender, DataGridViewCellEventArgs eventArgs)
    {
        if (eventArgs.RowIndex < 0 ||
            eventArgs.ColumnIndex < 0 ||
            _presetsGrid.Columns[eventArgs.ColumnIndex] is not DataGridViewButtonColumn ||
            _presetsGrid.Rows[eventArgs.RowIndex].Tag is not CursorPreset preset)
        {
            return;
        }

        using var details = new CursorDetailsForm(
            preset,
            () => _controller.ScanPreset(preset),
            _isDarkMode);
        details.ShowDialog(this);
        RefreshPresetGrid(preset.Id);
    }

    private void ReadGeneralControlsIntoDraft()
    {
        if (_groupCombo.SelectedItem is CursorGroup selectedGroup)
        {
            _draft.SelectedGroupId = selectedGroup.Id;
        }

        if (_modeCombo.SelectedItem is ModeChoice mode)
        {
            _draft.SelectionMode = mode.Mode;
        }

        if (_intervalUnitCombo.SelectedItem is IntervalUnitChoice unit)
        {
            var rawSeconds = decimal.ToInt32(_intervalValue.Value) * unit.Seconds;
            _draft.IntervalSeconds = Math.Clamp(
                rawSeconds,
                AppSettings.MinimumIntervalSeconds,
                AppSettings.MaximumIntervalSeconds);
        }

        _draft.StartWithWindows = _startWithWindowsCheckBox.Checked;
        _draft.UseDarkMode = _themeSwitch.Checked;
    }

    private void HandleThemeChanged(object? sender, EventArgs eventArgs)
    {
        if (_loading)
        {
            return;
        }

        _draft.UseDarkMode = _themeSwitch.Checked;
        ApplyTheme(_themeSwitch.Checked);
    }

    private void SetIntervalControls(int seconds)
    {
        IntervalUnitChoice unit;
        decimal value;

        if (seconds % 3600 == 0)
        {
            unit = _intervalUnitCombo.Items.OfType<IntervalUnitChoice>()
                .First(item => item.Seconds == 3600);
            value = seconds / 3600;
        }
        else if (seconds % 60 == 0)
        {
            unit = _intervalUnitCombo.Items.OfType<IntervalUnitChoice>()
                .First(item => item.Seconds == 60);
            value = seconds / 60;
        }
        else
        {
            unit = _intervalUnitCombo.Items.OfType<IntervalUnitChoice>()
                .First(item => item.Seconds == 1);
            value = seconds;
        }

        _intervalUnitCombo.SelectedItem = unit;
        UpdateIntervalBounds(unit);
        _intervalValue.Value = Math.Clamp(value, _intervalValue.Minimum, _intervalValue.Maximum);
    }

    private void HandleGeneralGroupChanged(object? sender, EventArgs eventArgs)
    {
        if (_loading || _groupCombo.SelectedItem is not CursorGroup group)
        {
            return;
        }

        SelectDraftGroup(group.Id);
    }

    private void HandleEditorGroupChanged(object? sender, EventArgs eventArgs)
    {
        if (_loading || _groupList.SelectedItem is not CursorGroup group)
        {
            return;
        }

        SelectDraftGroup(group.Id);
    }

    private void SelectDraftGroup(Guid groupId)
    {
        _draft.SelectedGroupId = groupId;

        var oldLoading = _loading;
        _loading = true;
        try
        {
            var index = _draft.Groups.FindIndex(group => group.Id == groupId);
            _groupCombo.SelectedIndex = index;
            _groupList.SelectedIndex = index;
        }
        finally
        {
            _loading = oldLoading;
        }

        RefreshQuickPresetCombo();
        RefreshPresetGrid();
    }

    private void HandleIntervalUnitChanged(object? sender, EventArgs eventArgs)
    {
        if (_intervalUnitCombo.SelectedItem is IntervalUnitChoice unit)
        {
            UpdateIntervalBounds(unit);
        }
    }

    private void UpdateIntervalBounds(IntervalUnitChoice unit)
    {
        _intervalValue.Minimum = Math.Max(
            1,
            (decimal)Math.Ceiling((double)AppSettings.MinimumIntervalSeconds / unit.Seconds));
        _intervalValue.Maximum = Math.Max(
            _intervalValue.Minimum,
            AppSettings.MaximumIntervalSeconds / unit.Seconds);

        if (_intervalValue.Value < _intervalValue.Minimum)
        {
            _intervalValue.Value = _intervalValue.Minimum;
        }
        else if (_intervalValue.Value > _intervalValue.Maximum)
        {
            _intervalValue.Value = _intervalValue.Maximum;
        }
    }

    private void ToggleRotation(object? sender, EventArgs eventArgs)
    {
        ReadGeneralControlsIntoDraft();
        _draft.IsRotationEnabled = !_controller.GetState().Settings.IsRotationEnabled;
        SavePendingSettings(silent: false);
    }

    private void ApplyQuickPreset(object? sender, EventArgs eventArgs)
    {
        if (_quickPresetCombo.SelectedItem is not CursorPreset preset)
        {
            MessageBox.Show(
                this,
                "切り替えるカーソルを選択してください。",
                "OshiCursour",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
            return;
        }

        if (!SavePendingSettings(silent: true))
        {
            return;
        }

        ShowOperationFailure(_controller.ApplyPresetNow(preset.Id));
    }

    private void RestoreBaseline()
    {
        if (!SavePendingSettings(silent: true))
        {
            return;
        }

        var result = _controller.RestoreBaseline();
        ShowOperationFailure(result);
        ReloadAfterControllerOperation();
    }

    private void RestoreWindowsDefault()
    {
        var confirmation = MessageBox.Show(
            this,
            "自動切り替えを OFF にして、Windows 標準カーソルへ戻します。",
            "Windows 標準カーソルへ戻す",
            MessageBoxButtons.OKCancel,
            MessageBoxIcon.Question);

        if (confirmation != DialogResult.OK || !SavePendingSettings(silent: true))
        {
            return;
        }

        var result = _controller.RestoreWindowsDefault();
        ShowOperationFailure(result);
        ReloadAfterControllerOperation();
    }

    private void SaveCurrentAsBaseline()
    {
        var confirmation = MessageBox.Show(
            this,
            "現在表示中のカーソルを「切り替え開始前に戻す」の復元先として上書きします。",
            "復元先を更新",
            MessageBoxButtons.OKCancel,
            MessageBoxIcon.Warning);

        if (confirmation != DialogResult.OK)
        {
            return;
        }

        ShowOperationFailure(_controller.SaveCurrentAsBaseline());
    }

    private void AddGroup()
    {
        var name = TextPromptDialog.Show(this, "グループ追加", "グループ名");
        if (string.IsNullOrWhiteSpace(name))
        {
            return;
        }

        var group = new CursorGroup { Name = name };
        _draft.Groups.Add(group);
        _draft.SelectedGroupId = group.Id;
        RefreshGroupSources(group.Id);
        RefreshQuickPresetCombo();
        RefreshPresetGrid();
    }

    private void RenameGroup()
    {
        var group = GetEditingGroup();
        if (group is null)
        {
            return;
        }

        var name = TextPromptDialog.Show(
            this,
            "グループ名を変更",
            "新しいグループ名",
            group.Name);

        if (string.IsNullOrWhiteSpace(name))
        {
            return;
        }

        group.Name = name;
        RefreshGroupSources(group.Id);
        RefreshQuickPresetCombo();
        RefreshPresetGrid();
    }

    private void DeleteGroup()
    {
        var group = GetEditingGroup();
        if (group is null)
        {
            return;
        }

        var confirmation = MessageBox.Show(
            this,
            $"グループ「{group.Name}」を設定から削除しますか？\nカーソルファイル本体は削除しません。",
            "グループを削除",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Warning);

        if (confirmation != DialogResult.Yes)
        {
            return;
        }

        _draft.Groups.Remove(group);
        _draft.SelectedGroupId = _draft.Groups.FirstOrDefault()?.Id;
        RefreshGroupSources(_draft.SelectedGroupId);
        RefreshQuickPresetCombo();
        RefreshPresetGrid();
    }

    private void AddPresetFolder()
    {
        var group = GetEditingGroup();
        if (group is null)
        {
            MessageBox.Show(this, "先にグループを追加してください。", "OshiCursour");
            return;
        }

        using var dialog = CreateFolderDialog("カーソル一式が入ったフォルダーを選択");
        if (dialog.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }

        var preset = new CursorPreset
        {
            Name = CreatePresetName(dialog.SelectedPath),
            FolderPath = dialog.SelectedPath
        };

        group.Presets.Add(preset);
        RefreshQuickPresetCombo();
        RefreshPresetGrid(preset.Id);
    }

    private void ChangePresetFolder()
    {
        var preset = GetSelectedPreset();
        if (preset is null)
        {
            return;
        }

        using var dialog = CreateFolderDialog("新しいカーソルフォルダーを選択");
        if (Directory.Exists(preset.FolderPath))
        {
            dialog.SelectedPath = preset.FolderPath;
        }

        if (dialog.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }

        preset.FolderPath = dialog.SelectedPath;
        RefreshPresetGrid(preset.Id);
    }

    private void OpenPresetFolder()
    {
        var preset = GetSelectedPreset();
        if (preset is null)
        {
            return;
        }

        if (!Directory.Exists(preset.FolderPath))
        {
            MessageBox.Show(
                this,
                "登録されたフォルダーが見つかりません。",
                "フォルダーを開けません",
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);
            return;
        }

        try
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = preset.FolderPath,
                UseShellExecute = true
            });
        }
        catch (Exception exception)
        {
            MessageBox.Show(
                this,
                $"フォルダーを開けませんでした。\n{exception.Message}",
                "フォルダーを開けません",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }
    }

    private void RenamePreset()
    {
        var preset = GetSelectedPreset();
        if (preset is null)
        {
            return;
        }

        var name = TextPromptDialog.Show(
            this,
            "カーソル名を変更",
            "表示名",
            preset.Name);

        if (string.IsNullOrWhiteSpace(name))
        {
            return;
        }

        preset.Name = name;
        RefreshQuickPresetCombo();
        RefreshPresetGrid(preset.Id);
    }

    private void DeletePreset()
    {
        var group = GetEditingGroup();
        var preset = GetSelectedPreset();
        if (group is null || preset is null)
        {
            return;
        }

        var confirmation = MessageBox.Show(
            this,
            $"「{preset.Name}」をグループから外しますか？\nカーソルファイル本体は削除しません。",
            "カーソルを削除",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Warning);

        if (confirmation != DialogResult.Yes)
        {
            return;
        }

        group.Presets.Remove(preset);
        RefreshQuickPresetCombo();
        RefreshPresetGrid();
    }

    private void MovePreset(int direction)
    {
        var group = GetEditingGroup();
        var preset = GetSelectedPreset();
        if (group is null || preset is null)
        {
            return;
        }

        var currentIndex = group.Presets.IndexOf(preset);
        var targetIndex = currentIndex + direction;
        if (targetIndex < 0 || targetIndex >= group.Presets.Count)
        {
            return;
        }

        (group.Presets[currentIndex], group.Presets[targetIndex]) =
            (group.Presets[targetIndex], group.Presets[currentIndex]);

        RefreshQuickPresetCombo();
        RefreshPresetGrid(preset.Id);
    }

    private void HandleControllerStateChanged(AppStateSnapshot state)
    {
        if (IsDisposed)
        {
            return;
        }

        UpdateRuntimeState(state);
    }

    private void UpdateRuntimeState(AppStateSnapshot state)
    {
        _statusLabel.Text = state.StatusMessage;
        _activePresetLabel.Text = $"現在: {state.ActivePresetName}";
        _nextSwitchLabel.Text = state.NextSwitchAt is null
            ? "次回切り替え: －"
            : $"次回切り替え: {state.NextSwitchAt:yyyy/MM/dd HH:mm:ss}";

        _toggleButton.Text = state.Settings.IsRotationEnabled
            ? "● 自動切り替え ON\nクリックして停止"
            : "○ 自動切り替え OFF\nクリックして開始";

        _toggleButton.BackColor = state.Settings.IsRotationEnabled
            ? (_isDarkMode ? Color.FromArgb(48, 105, 119) : Color.FromArgb(199, 241, 246))
            : (_isDarkMode ? Color.FromArgb(72, 58, 91) : Color.FromArgb(239, 232, 251));
        _toggleButton.ForeColor = CurrentPrimaryTextColor;
        UpdateActiveCursorThumbnail(state);
    }

    private void ReloadAfterControllerOperation()
    {
        _draft = _controller.GetState().Settings.DeepClone();
        LoadDraftIntoControls();
        UpdateRuntimeState(_controller.GetState());
    }

    private void ShowOperationFailure(OperationResult result)
    {
        if (result.Success)
        {
            UpdateRuntimeState(_controller.GetState());
            return;
        }

        MessageBox.Show(
            this,
            result.Message,
            "OshiCursour",
            MessageBoxButtons.OK,
            MessageBoxIcon.Error);
    }

    private CursorGroup? GetEditingGroup()
    {
        return _groupList.SelectedItem as CursorGroup ?? _draft.GetSelectedGroup();
    }

    private CursorPreset? GetSelectedPreset()
    {
        return _presetsGrid.SelectedRows.Count == 0
            ? null
            : _presetsGrid.SelectedRows[0].Tag as CursorPreset;
    }

    private static FolderBrowserDialog CreateFolderDialog(string description)
    {
        return new FolderBrowserDialog
        {
            Description = description,
            UseDescriptionForTitle = true,
            ShowNewFolderButton = false
        };
    }

    private static string CreatePresetName(string folderPath)
    {
        var name = new DirectoryInfo(folderPath).Name;
        const string suffix = "_マウスカーソル";
        return name.EndsWith(suffix, StringComparison.OrdinalIgnoreCase)
            ? name[..^suffix.Length]
            : name;
    }

    private void ApplyTheme(bool darkMode)
    {
        _isDarkMode = darkMode;
        BackColor = CurrentPageBackground;
        ForeColor = CurrentTextColor;
        ApplyControlTheme(this, inheritedSurface: false);

        _activePresetLabel.ForeColor = CurrentMutedTextColor;
        _nextSwitchLabel.ForeColor = CurrentMutedTextColor;
        _saveFeedbackLabel.ForeColor = CurrentMutedTextColor;
        _statusLabel.ForeColor = CurrentPrimaryTextColor;
        _activeCursorThumbnail.BackColor = Color.Transparent;

        ApplyPresetGridTheme();
        ApplyNavigationTheme();
        _themeSwitch.DarkPalette = darkMode;
        _themeSwitch.Invalidate();
        WindowsThemeService.ApplyToWindow(this, darkMode);
        WindowsThemeService.ApplyToControl(_presetsGrid, darkMode);
        WindowsThemeService.ApplyToControl(_groupList, darkMode);
        WindowsThemeService.ApplyToControl(_groupCombo, darkMode);
        WindowsThemeService.ApplyToControl(_modeCombo, darkMode);
        WindowsThemeService.ApplyToControl(_intervalUnitCombo, darkMode);
        WindowsThemeService.ApplyToControl(_quickPresetCombo, darkMode);
        Invalidate(true);
    }

    private void ApplyControlTheme(Control control, bool inheritedSurface)
    {
        var isSurface = inheritedSurface ||
            string.Equals(control.Tag?.ToString(), "theme-surface", StringComparison.Ordinal) ||
            control is CardPanel;

        switch (control)
        {
            case CardPanel card:
                card.ApplyPalette(
                    CurrentSurfaceColor,
                    CurrentBorderColor,
                    CurrentPrimaryTextColor,
                    _isDarkMode ? Color.FromArgb(162, 126, 232) : AccentColor);
                break;
            case Button button:
                ApplyButtonTheme(button);
                break;
            case ComboBox comboBox:
                comboBox.BackColor = _isDarkMode ? Color.FromArgb(39, 46, 61) : SurfaceColor;
                comboBox.ForeColor = CurrentTextColor;
                comboBox.FlatStyle = _isDarkMode ? FlatStyle.Flat : FlatStyle.Standard;
                break;
            case NumericUpDown numeric:
                numeric.BackColor = _isDarkMode ? Color.FromArgb(39, 46, 61) : SurfaceColor;
                numeric.ForeColor = CurrentTextColor;
                break;
            case ListBox listBox:
                listBox.BackColor = _isDarkMode ? Color.FromArgb(39, 46, 61) : Color.FromArgb(249, 250, 253);
                listBox.ForeColor = CurrentTextColor;
                break;
            case Label label:
                label.ForeColor = label.Font.Bold
                    ? CurrentPrimaryTextColor
                    : CurrentTextColor;
                break;
            case CheckBox checkBox when checkBox is not ThemeSwitch:
                checkBox.ForeColor = CurrentTextColor;
                break;
            case TabControl tabControl:
                tabControl.BackColor = CurrentPageBackground;
                break;
            case DataGridView:
                break;
            default:
                if (control.BackColor != Color.Transparent)
                {
                    control.BackColor = isSurface
                        ? CurrentSurfaceColor
                        : CurrentPageBackground;
                }
                control.ForeColor = CurrentTextColor;
                break;
        }

        foreach (Control child in control.Controls)
        {
            ApplyControlTheme(child, isSurface);
        }
    }

    private void ApplyButtonTheme(Button button)
    {
        var style = button.Tag?.ToString();
        button.FlatStyle = FlatStyle.Flat;
        if (style == "theme-navigation")
        {
            return;
        }
        if (style == "theme-toggle")
        {
            var enabled = _controller.GetState().Settings.IsRotationEnabled;
            button.BackColor = enabled
                ? (_isDarkMode ? Color.FromArgb(48, 105, 119) : Color.FromArgb(199, 241, 246))
                : (_isDarkMode ? Color.FromArgb(72, 58, 91) : Color.FromArgb(239, 232, 251));
            button.ForeColor = CurrentPrimaryTextColor;
            button.FlatAppearance.BorderSize = 0;
            return;
        }

        if (style == "theme-primary")
        {
            button.BackColor = _isDarkMode
                ? Color.FromArgb(64, 154, 182)
                : PrimaryDarkColor;
            button.ForeColor = Color.White;
            button.FlatAppearance.BorderSize = 0;
            return;
        }

        button.BackColor = CurrentSurfaceColor;
        if (style == "theme-danger")
        {
            button.ForeColor = _isDarkMode
                ? Color.FromArgb(255, 143, 154)
                : Color.FromArgb(180, 58, 70);
            button.FlatAppearance.BorderColor = _isDarkMode
                ? Color.FromArgb(126, 68, 78)
                : Color.FromArgb(235, 193, 199);
        }
        else
        {
            button.ForeColor = CurrentTextColor;
            button.FlatAppearance.BorderColor = CurrentBorderColor;
        }
    }

    private void ApplyNavigationTheme()
    {
        ApplyNavigationButtonTheme(_generalNavButton, _selectedPageIndex == 0);
        ApplyNavigationButtonTheme(_groupsNavButton, _selectedPageIndex == 1);
    }

    private void ApplyNavigationButtonTheme(Button button, bool selected)
    {
        button.BackColor = selected ? CurrentSurfaceColor : CurrentPageBackground;
        button.ForeColor = selected ? CurrentPrimaryTextColor : CurrentMutedTextColor;
        button.FlatAppearance.BorderSize = 0;
        button.FlatAppearance.MouseOverBackColor = _isDarkMode
            ? Color.FromArgb(39, 46, 61)
            : Color.FromArgb(237, 244, 250);
    }

    private void ApplyPresetGridTheme()
    {
        var input = _isDarkMode ? Color.FromArgb(39, 46, 61) : SurfaceColor;
        var header = _isDarkMode ? Color.FromArgb(45, 55, 73) : Color.FromArgb(237, 244, 250);
        var selection = _isDarkMode ? Color.FromArgb(51, 77, 94) : Color.FromArgb(221, 244, 250);
        _presetsGrid.BackgroundColor = CurrentSurfaceColor;
        _presetsGrid.GridColor = CurrentBorderColor;
        _presetsGrid.DefaultCellStyle.BackColor = input;
        _presetsGrid.DefaultCellStyle.ForeColor = CurrentTextColor;
        _presetsGrid.DefaultCellStyle.SelectionBackColor = selection;
        _presetsGrid.DefaultCellStyle.SelectionForeColor = CurrentTextColor;
        _presetsGrid.ColumnHeadersDefaultCellStyle.BackColor = header;
        _presetsGrid.ColumnHeadersDefaultCellStyle.ForeColor = CurrentPrimaryTextColor;
        _presetsGrid.ColumnHeadersDefaultCellStyle.SelectionBackColor = header;
        _presetsGrid.ColumnHeadersDefaultCellStyle.SelectionForeColor = CurrentPrimaryTextColor;
        if (_presetsGrid.Columns
                .OfType<DataGridViewButtonColumn>()
                .FirstOrDefault() is { } buttonColumn)
        {
            buttonColumn.FlatStyle = _isDarkMode ? FlatStyle.Flat : FlatStyle.Standard;
            buttonColumn.DefaultCellStyle.BackColor = header;
            buttonColumn.DefaultCellStyle.ForeColor = CurrentPrimaryTextColor;
            buttonColumn.DefaultCellStyle.SelectionBackColor = selection;
            buttonColumn.DefaultCellStyle.SelectionForeColor = CurrentPrimaryTextColor;
        }
    }

    private static Image? LoadEmbeddedAppImage()
    {
        try
        {
            using var stream = typeof(SettingsForm).Assembly.GetManifestResourceStream(
                "CursorCycle.Resources.OshiCursour.png");
            if (stream is null)
            {
                return null;
            }

            using var source = new Bitmap(stream);
            return new Bitmap(source);
        }
        catch
        {
            return null;
        }
    }

    private static Bitmap? LoadArrowThumbnail(CursorSetScanResult scan)
    {
        return scan.FilesByRegistryName.TryGetValue(
            CursorRoles.Arrow.RegistryName,
            out var arrowPath)
                ? CursorThumbnailLoader.Load(arrowPath)
                : null;
    }

    private void UpdateActiveCursorThumbnail(AppStateSnapshot state)
    {
        var preset = state.ActivePresetId is null
            ? null
            : state.Settings.Groups
                .SelectMany(group => group.Presets)
                .FirstOrDefault(item => item.Id == state.ActivePresetId);

        if (_thumbnailPresetId == preset?.Id &&
            string.Equals(
                _thumbnailFolderPath,
                preset?.FolderPath,
                StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        _activeCursorThumbnail.Image?.Dispose();
        _activeCursorThumbnail.Image = preset is null
            ? null
            : LoadArrowThumbnail(_controller.ScanPreset(preset));
        _thumbnailPresetId = preset?.Id;
        _thumbnailFolderPath = preset?.FolderPath;
    }

    private void DisposePresetThumbnails()
    {
        foreach (DataGridViewRow row in _presetsGrid.Rows)
        {
            if (row.Cells.Count > 1 && row.Cells[1].Value is Image image)
            {
                image.Dispose();
                row.Cells[1].Value = null;
            }
        }
    }

    private static Control CreateGroupBox(string title, int height)
    {
        return new CardPanel
        {
            Text = title,
            Dock = DockStyle.Top,
            Height = height,
            Margin = new Padding(0, 0, 0, 12),
            Padding = new Padding(1, 34, 1, 1),
            BackColor = SurfaceColor
        };
    }

    private Label CreateSectionTitle(string text)
    {
        return new Label
        {
            AutoSize = true,
            Font = new Font(Font.FontFamily, 11F, FontStyle.Bold),
            ForeColor = PrimaryDarkColor,
            Text = text,
            Margin = new Padding(0, 0, 0, 12)
        };
    }

    private static Button CreateButton(string text, int width)
    {
        var button = new Button
        {
            Text = text,
            Width = width,
            Height = 36,
            Margin = new Padding(0, 0, 8, 0),
            FlatStyle = FlatStyle.Flat,
            BackColor = SurfaceColor,
            ForeColor = TextColor,
            Cursor = Cursors.Hand
        };
        button.Tag = "theme-secondary";
        button.FlatAppearance.BorderColor = BorderColor;
        return button;
    }

    private static Button CreatePrimaryButton(string text, int width)
    {
        var button = CreateButton(text, width);
        button.BackColor = PrimaryDarkColor;
        button.ForeColor = Color.White;
        button.Font = new Font(button.Font, FontStyle.Bold);
        button.FlatAppearance.BorderSize = 0;
        button.FlatAppearance.MouseOverBackColor = Color.FromArgb(34, 64, 127);
        button.Tag = "theme-primary";
        return button;
    }

    private static Button CreateDangerButton(string text, int width)
    {
        var button = CreateButton(text, width);
        button.ForeColor = Color.FromArgb(180, 58, 70);
        button.FlatAppearance.BorderColor = Color.FromArgb(235, 193, 199);
        button.Tag = "theme-danger";
        return button;
    }

    private void ConfigureDropDown(ComboBox comboBox, bool compact = false)
    {
        comboBox.DropDownStyle = ComboBoxStyle.DropDownList;
        comboBox.Dock = DockStyle.Fill;
        comboBox.FlatStyle = FlatStyle.Standard;
        comboBox.BackColor = SurfaceColor;
        comboBox.ForeColor = TextColor;
        comboBox.DrawMode = DrawMode.OwnerDrawFixed;
        comboBox.ItemHeight = compact ? 20 : 30;
        comboBox.DropDownHeight = compact ? 160 : 240;
        comboBox.IntegralHeight = false;
        comboBox.DrawItem += DrawDropDownItem;
    }

    private void DrawDropDownItem(object? sender, DrawItemEventArgs eventArgs)
    {
        if (sender is not ComboBox comboBox || eventArgs.Index < 0)
        {
            return;
        }

        var selected = (eventArgs.State & DrawItemState.Selected) != 0;
        using var background = new SolidBrush(
            selected
                ? (_isDarkMode ? Color.FromArgb(51, 77, 94) : Color.FromArgb(221, 244, 250))
                : (_isDarkMode ? Color.FromArgb(39, 46, 61) : SurfaceColor));
        eventArgs.Graphics.FillRectangle(background, eventArgs.Bounds);

        var textBounds = new Rectangle(
            eventArgs.Bounds.Left + 10,
            eventArgs.Bounds.Top,
            Math.Max(0, eventArgs.Bounds.Width - 18),
            eventArgs.Bounds.Height);
        TextRenderer.DrawText(
            eventArgs.Graphics,
            comboBox.Items[eventArgs.Index]?.ToString() ?? string.Empty,
            comboBox.Font,
            textBounds,
            selected ? CurrentPrimaryTextColor : CurrentTextColor,
            TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);

        if ((eventArgs.State & DrawItemState.ComboBoxEdit) == 0)
        {
            using var separator = new Pen(CurrentBorderColor);
            eventArgs.Graphics.DrawLine(
                separator,
                eventArgs.Bounds.Left + 8,
                eventArgs.Bounds.Bottom - 1,
                eventArgs.Bounds.Right - 8,
                eventArgs.Bounds.Bottom - 1);
        }

    }

    private static void AddSettingRow(
        TableLayoutPanel layout,
        int row,
        string labelText,
        Control control)
    {
        var label = new Label
        {
            AutoSize = true,
            Anchor = AnchorStyles.Left,
            Text = labelText
        };

        control.Margin = new Padding(4, 4, 4, 8);
        layout.Controls.Add(label, 0, row);
        layout.Controls.Add(control, 1, row);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _controller.StateChanged -= HandleControllerStateChanged;
            _headerImage?.Dispose();
            _activeCursorThumbnail.Image?.Dispose();
            DisposePresetThumbnails();
        }

        base.Dispose(disposing);
    }

    private sealed record ModeChoice(string DisplayName, CursorSelectionMode Mode)
    {
        public override string ToString() => DisplayName;
    }

    private sealed record IntervalUnitChoice(string DisplayName, int Seconds)
    {
        public override string ToString() => DisplayName;
    }

    private sealed class CardPanel : Panel
    {
        private Color _borderColor = BorderColor;
        private Color _titleColor = PrimaryDarkColor;
        private Color _accentColor = AccentColor;

        public CardPanel()
        {
            SetStyle(
                ControlStyles.UserPaint |
                ControlStyles.AllPaintingInWmPaint |
                ControlStyles.OptimizedDoubleBuffer,
                true);
        }

        public void ApplyPalette(
            Color surfaceColor,
            Color borderColor,
            Color titleColor,
            Color accentColor)
        {
            BackColor = surfaceColor;
            _borderColor = borderColor;
            _titleColor = titleColor;
            _accentColor = accentColor;
            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs eventArgs)
        {
            base.OnPaint(eventArgs);
            var bounds = new Rectangle(0, 0, Width - 1, Height - 1);
            using var borderPen = new Pen(_borderColor);
            eventArgs.Graphics.DrawRectangle(borderPen, bounds);

            using var accentBrush = new SolidBrush(_accentColor);
            eventArgs.Graphics.FillRectangle(accentBrush, 0, 0, 5, 34);

            using var titleFont = new Font(Font, FontStyle.Bold);
            TextRenderer.DrawText(
                eventArgs.Graphics,
                Text,
                titleFont,
                new Rectangle(18, 0, Width - 28, 34),
                _titleColor,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
        }
    }

    private sealed class ThemeSwitch : CheckBox
    {
        public bool DarkPalette { get; set; }

        public ThemeSwitch()
        {
            Appearance = Appearance.Button;
            FlatStyle = FlatStyle.Flat;
            FlatAppearance.BorderSize = 0;
            Cursor = Cursors.Hand;
            Text = string.Empty;
            SetStyle(ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer, true);
        }

        protected override void OnPaint(PaintEventArgs eventArgs)
        {
            eventArgs.Graphics.SmoothingMode =
                System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            eventArgs.Graphics.Clear(
                DarkPalette ? Color.FromArgb(31, 37, 50) : Color.White);

            var track = new Rectangle(2, 7, 44, 20);
            using var trackBrush = new SolidBrush(
                Checked ? Color.FromArgb(71, 192, 220) : Color.FromArgb(166, 177, 199));
            eventArgs.Graphics.FillEllipse(trackBrush, track.Left, track.Top, track.Height, track.Height);
            eventArgs.Graphics.FillEllipse(
                trackBrush,
                track.Right - track.Height,
                track.Top,
                track.Height,
                track.Height);
            eventArgs.Graphics.FillRectangle(
                trackBrush,
                track.Left + track.Height / 2,
                track.Top,
                track.Width - track.Height,
                track.Height);

            var knobSize = 16;
            var knobX = Checked ? track.Right - knobSize - 2 : track.Left + 2;
            using var knobBrush = new SolidBrush(Color.White);
            eventArgs.Graphics.FillEllipse(knobBrush, knobX, track.Top + 2, knobSize, knobSize);

            TextRenderer.DrawText(
                eventArgs.Graphics,
                Checked ? "ダーク" : "ライト",
                Font,
                new Rectangle(54, 0, Width - 54, Height),
                DarkPalette ? Color.FromArgb(232, 237, 247) : TextColor,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter);
        }
    }
}
