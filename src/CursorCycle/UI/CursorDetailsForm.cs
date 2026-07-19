using System.Diagnostics;
using CursorCycle.Domain;
using CursorCycle.Infrastructure;

namespace CursorCycle.UI;

public sealed class CursorDetailsForm : Form
{
    private static readonly Color PageBackground = Color.FromArgb(246, 248, 252);
    private static readonly Color BorderColor = Color.FromArgb(222, 228, 238);
    private static readonly Color TextColor = Color.FromArgb(31, 42, 68);
    private static readonly Color MutedTextColor = Color.FromArgb(104, 116, 139);
    private static readonly Color PrimaryDarkColor = Color.FromArgb(21, 46, 103);

    private readonly CursorPreset _preset;
    private readonly Func<CursorSetScanResult> _scanPreset;
    private readonly DataGridView _grid = new();
    private readonly Label _summaryLabel = new();
    private readonly Label _folderLabel = new();
    private readonly Font _missingFont;
    private readonly bool _useDarkMode;

    public CursorDetailsForm(
        CursorPreset preset,
        Func<CursorSetScanResult> scanPreset,
        bool useDarkMode)
    {
        _preset = preset;
        _scanPreset = scanPreset;
        _useDarkMode = useDarkMode;
        _missingFont = new Font("Yu Gothic UI", 9.5F, FontStyle.Bold);

        Text = $"{preset.Name} のカーソル一覧";
        Icon = Icon.ExtractAssociatedIcon(System.Windows.Forms.Application.ExecutablePath);
        StartPosition = FormStartPosition.CenterParent;
        ClientSize = new Size(1040, 620);
        MinimumSize = Size;
        MaximumSize = Size;
        MaximizeBox = false;
        Font = new Font("Yu Gothic UI", 9.5F);
        BackColor = PageBackground;
        ForeColor = TextColor;
        ShowInTaskbar = false;

        Controls.Add(BuildGrid());
        Controls.Add(BuildFooter());
        Controls.Add(BuildHeader());
        RefreshScan();
        ApplyTheme(useDarkMode);
    }

    private Control BuildHeader()
    {
        var header = new Panel
        {
            Dock = DockStyle.Top,
            Height = 94,
            BackColor = Color.White,
            Padding = new Padding(22, 14, 22, 10)
        };
        var title = new Label
        {
            AutoSize = true,
            Location = new Point(22, 14),
            Font = new Font(Font.FontFamily, 14F, FontStyle.Bold),
            ForeColor = PrimaryDarkColor,
            Text = _preset.Name
        };
        _summaryLabel.AutoSize = true;
        _summaryLabel.Location = new Point(24, 44);
        _folderLabel.AutoEllipsis = true;
        _folderLabel.Location = new Point(150, 44);
        _folderLabel.Size = new Size(850, 24);
        _folderLabel.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
        _folderLabel.ForeColor = MutedTextColor;
        _folderLabel.Text = _preset.FolderPath;

        header.Controls.Add(title);
        header.Controls.Add(_summaryLabel);
        header.Controls.Add(_folderLabel);
        return header;
    }

    private Control BuildGrid()
    {
        _grid.Dock = DockStyle.Fill;
        _grid.ReadOnly = true;
        _grid.AllowUserToAddRows = false;
        _grid.AllowUserToDeleteRows = false;
        _grid.AllowUserToResizeRows = false;
        _grid.AutoGenerateColumns = false;
        _grid.MultiSelect = false;
        _grid.RowHeadersVisible = false;
        _grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
        _grid.BorderStyle = BorderStyle.None;
        _grid.BackgroundColor = Color.White;
        _grid.GridColor = BorderColor;
        _grid.CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal;
        _grid.EnableHeadersVisualStyles = false;
        _grid.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(237, 244, 250);
        _grid.ColumnHeadersDefaultCellStyle.ForeColor = PrimaryDarkColor;
        _grid.ColumnHeadersDefaultCellStyle.Font = new Font(Font, FontStyle.Bold);
        _grid.ColumnHeadersDefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
        _grid.ColumnHeadersDefaultCellStyle.SelectionBackColor = Color.FromArgb(237, 244, 250);
        _grid.ColumnHeadersDefaultCellStyle.SelectionForeColor = PrimaryDarkColor;
        _grid.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing;
        _grid.ColumnHeadersHeight = 38;
        _grid.DefaultCellStyle.BackColor = Color.White;
        _grid.DefaultCellStyle.ForeColor = TextColor;
        _grid.DefaultCellStyle.SelectionBackColor = Color.White;
        _grid.DefaultCellStyle.SelectionForeColor = TextColor;
        _grid.DefaultCellStyle.Padding = new Padding(5, 3, 5, 3);
        _grid.RowTemplate.Height = 42;
        _grid.CellClick += HandleGridCellClick;

        _grid.Columns.Add(new DataGridViewImageColumn
        {
            HeaderText = "画像",
            Width = 56,
            ImageLayout = DataGridViewImageCellLayout.Zoom,
            DefaultCellStyle = new DataGridViewCellStyle { NullValue = null },
            SortMode = DataGridViewColumnSortMode.NotSortable
        });
        _grid.Columns.Add(new DataGridViewTextBoxColumn
        {
            HeaderText = "カーソルの役割",
            Width = 190,
            SortMode = DataGridViewColumnSortMode.NotSortable
        });
        _grid.Columns.Add(new DataGridViewTextBoxColumn
        {
            HeaderText = "設定状態",
            Width = 110,
            SortMode = DataGridViewColumnSortMode.NotSortable
        });
        _grid.Columns.Add(new DataGridViewTextBoxColumn
        {
            HeaderText = "ファイル",
            AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill,
            MinimumWidth = 350,
            SortMode = DataGridViewColumnSortMode.NotSortable
        });
        _grid.Columns.Add(new DataGridViewButtonColumn
        {
            HeaderText = "個別設定",
            Text = "設定",
            UseColumnTextForButtonValue = true,
            Width = 100,
            FlatStyle = FlatStyle.Standard,
            SortMode = DataGridViewColumnSortMode.NotSortable,
            DefaultCellStyle = new DataGridViewCellStyle
            {
                Alignment = DataGridViewContentAlignment.MiddleCenter,
                Padding = new Padding(8, 4, 8, 4),
                BackColor = Color.FromArgb(237, 244, 250),
                ForeColor = PrimaryDarkColor,
                SelectionBackColor = Color.FromArgb(237, 244, 250),
                SelectionForeColor = PrimaryDarkColor
            }
        });

        var host = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = PageBackground,
            Padding = new Padding(20, 16, 20, 16)
        };
        host.Controls.Add(_grid);
        return host;
    }

    private Control BuildFooter()
    {
        var footer = new Panel
        {
            Dock = DockStyle.Bottom,
            Height = 66,
            BackColor = Color.White,
            Padding = new Padding(20, 12, 20, 12)
        };
        var actions = new FlowLayoutPanel
        {
            Dock = DockStyle.Right,
            AutoSize = true,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            Margin = Padding.Empty
        };
        var configButton = CreateFooterButton("検索設定を開く", 150, false);
        var refreshButton = CreateFooterButton("再検出", 110, false);
        var closeButton = CreateFooterButton("閉じる", 120, true);
        configButton.Click += (_, _) => OpenDetectionConfig();
        refreshButton.Click += (_, _) => RefreshScan();
        closeButton.Click += (_, _) => Close();
        actions.Controls.Add(configButton);
        actions.Controls.Add(refreshButton);
        actions.Controls.Add(closeButton);
        footer.Controls.Add(actions);
        AcceptButton = closeButton;
        CancelButton = closeButton;
        return footer;
    }

    private Button CreateFooterButton(string text, int width, bool primary)
    {
        var button = new Button
        {
            Text = text,
            Width = width,
            Height = 38,
            Margin = new Padding(8, 0, 0, 0),
            FlatStyle = FlatStyle.Flat,
            BackColor = primary ? PrimaryDarkColor : Color.White,
            ForeColor = primary ? Color.White : PrimaryDarkColor,
            Font = new Font(Font, primary ? FontStyle.Bold : FontStyle.Regular)
        };
        button.FlatAppearance.BorderColor = BorderColor;
        button.FlatAppearance.BorderSize = primary ? 0 : 1;
        return button;
    }

    private void RefreshScan()
    {
        DisposeRowImages();
        _grid.Rows.Clear();

        var scan = _scanPreset();
        _summaryLabel.ForeColor = scan.IsValid
            ? Color.FromArgb(38, 128, 91)
            : Color.Firebrick;
        _summaryLabel.Text = $"検出: {scan.MatchedRoleCount}/{CursorRoles.All.Count} 種類";

        foreach (var role in CursorRoles.All)
        {
            var detected = scan.FilesByRegistryName.TryGetValue(role.RegistryName, out var path);
            var manuallyAssigned = scan.ManuallyAssignedRegistryNames.Contains(role.RegistryName);
            var thumbnail = detected ? CursorThumbnailLoader.Load(path) : null;
            var status = manuallyAssigned
                ? "個別設定"
                : detected
                    ? "自動検出"
                    : "未検出";
            var rowIndex = _grid.Rows.Add(
                thumbnail,
                role.DisplayName,
                status,
                detected ? Path.GetFileName(path) : "—",
                "設定");
            var row = _grid.Rows[rowIndex];
            row.Tag = role;
            row.Cells[3].ToolTipText = detected ? path : string.Empty;
            if (manuallyAssigned)
            {
                row.Cells[2].Style.ForeColor = _useDarkMode
                    ? Color.FromArgb(127, 219, 237)
                    : Color.FromArgb(72, 112, 190);
                row.Cells[2].Style.Font = _missingFont;
            }
            else if (!detected)
            {
                row.Cells[2].Style.ForeColor = Color.Firebrick;
                row.Cells[2].Style.Font = _missingFont;
                row.Cells[3].Style.ForeColor = MutedTextColor;
            }
        }

        _grid.ClearSelection();
        _grid.CurrentCell = null;
        if (_useDarkMode)
        {
            _summaryLabel.ForeColor = scan.IsValid
                ? Color.FromArgb(127, 219, 237)
                : Color.FromArgb(255, 116, 126);
        }
    }

    private void HandleGridCellClick(object? sender, DataGridViewCellEventArgs eventArgs)
    {
        if (eventArgs.RowIndex < 0 || eventArgs.ColumnIndex != 4 ||
            _grid.Rows[eventArgs.RowIndex].Tag is not CursorRoleDefinition role)
        {
            return;
        }

        var hasManualAssignment = _preset.ManualFilesByRegistryName.ContainsKey(role.RegistryName);
        if (hasManualAssignment)
        {
            var choice = MessageBox.Show(
                this,
                $"{role.DisplayName} には個別ファイルが設定されています。\n\n" +
                "［はい］ファイルを変更する\n" +
                "［いいえ］個別設定を解除して自動検出に戻す\n" +
                "［キャンセル］何もしない",
                "個別カーソル設定",
                MessageBoxButtons.YesNoCancel,
                MessageBoxIcon.Question);
            if (choice == DialogResult.No)
            {
                _preset.ManualFilesByRegistryName.Remove(role.RegistryName);
                RefreshScan();
                return;
            }

            if (choice != DialogResult.Yes)
            {
                return;
            }
        }

        using var dialog = new OpenFileDialog
        {
            Title = $"{role.DisplayName} に使用するカーソルを選択",
            Filter = "カーソルファイル (*.ani;*.cur)|*.ani;*.cur|すべてのファイル (*.*)|*.*",
            CheckFileExists = true,
            Multiselect = false,
            InitialDirectory = Directory.Exists(_preset.FolderPath)
                ? _preset.FolderPath
                : Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments)
        };
        if (dialog.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }

        _preset.ManualFilesByRegistryName[role.RegistryName] =
            MakePortablePath(_preset.FolderPath, dialog.FileName);
        RefreshScan();
    }

    private static string MakePortablePath(string baseFolder, string selectedPath)
    {
        try
        {
            if (!Directory.Exists(baseFolder))
            {
                return selectedPath;
            }

            var relative = Path.GetRelativePath(baseFolder, selectedPath);
            if (!Path.IsPathRooted(relative) &&
                !string.Equals(relative, "..", StringComparison.Ordinal) &&
                !relative.StartsWith(".." + Path.DirectorySeparatorChar, StringComparison.Ordinal))
            {
                return relative;
            }
        }
        catch
        {
            // 別ドライブなど相対化できない場合は絶対パスを保存する。
        }

        return selectedPath;
    }

    private void OpenDetectionConfig()
    {
        try
        {
            _ = new CursorDetectionConfigStore().Load();
            _ = Process.Start(new ProcessStartInfo
            {
                FileName = "notepad.exe",
                Arguments = $"\"{AppPaths.CursorDetectionConfigFile}\"",
                UseShellExecute = true
            });
            MessageBox.Show(
                this,
                "検索パターンを編集して保存した後、この画面の［再検出］を押してください。\n" +
                "ワイルドカード * と ? を使用できます。",
                "カーソル検索設定",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
        }
        catch (Exception exception)
        {
            MessageBox.Show(
                this,
                "検索設定を開けませんでした。\n\n" + exception.Message,
                "カーソル検索設定",
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);
        }
    }

    private void ApplyTheme(bool darkMode)
    {
        if (!darkMode)
        {
            return;
        }

        var page = Color.FromArgb(20, 24, 34);
        var surface = Color.FromArgb(31, 37, 50);
        var input = Color.FromArgb(39, 46, 61);
        var header = Color.FromArgb(45, 55, 73);
        var border = Color.FromArgb(61, 71, 91);
        var text = Color.FromArgb(232, 237, 247);
        var muted = Color.FromArgb(166, 177, 199);
        var accent = Color.FromArgb(127, 219, 237);

        BackColor = page;
        ForeColor = text;
        foreach (Control control in Controls)
        {
            control.BackColor = control is Panel ? surface : page;
            control.ForeColor = text;
            foreach (Control child in control.Controls)
            {
                if (child is Label label)
                {
                    label.BackColor = Color.Transparent;
                    label.ForeColor = label.Font.Bold ? accent : muted;
                }
                else if (child is Button button)
                {
                    button.BackColor = Color.FromArgb(64, 154, 182);
                    button.ForeColor = Color.White;
                }
                else if (child is FlowLayoutPanel flow)
                {
                    flow.BackColor = surface;
                    foreach (Control item in flow.Controls)
                    {
                        item.BackColor = Color.FromArgb(64, 154, 182);
                        item.ForeColor = Color.White;
                    }
                }
            }
        }

        _grid.BackgroundColor = surface;
        _grid.GridColor = border;
        _grid.DefaultCellStyle.BackColor = input;
        _grid.DefaultCellStyle.ForeColor = text;
        _grid.DefaultCellStyle.SelectionBackColor = input;
        _grid.DefaultCellStyle.SelectionForeColor = text;
        _grid.ColumnHeadersDefaultCellStyle.BackColor = header;
        _grid.ColumnHeadersDefaultCellStyle.ForeColor = accent;
        _grid.ColumnHeadersDefaultCellStyle.SelectionBackColor = header;
        _grid.ColumnHeadersDefaultCellStyle.SelectionForeColor = accent;
        if (_grid.Columns.OfType<DataGridViewButtonColumn>().FirstOrDefault() is { } buttonColumn)
        {
            buttonColumn.FlatStyle = FlatStyle.Flat;
            buttonColumn.DefaultCellStyle.BackColor = header;
            buttonColumn.DefaultCellStyle.ForeColor = text;
            buttonColumn.DefaultCellStyle.SelectionBackColor = header;
            buttonColumn.DefaultCellStyle.SelectionForeColor = text;
        }

        WindowsThemeService.ApplyToWindow(this, true);
        WindowsThemeService.ApplyToControl(_grid, true);
    }

    private void DisposeRowImages()
    {
        foreach (DataGridViewRow row in _grid.Rows)
        {
            if (row.Cells.Count > 0 && row.Cells[0].Value is Image image)
            {
                image.Dispose();
                row.Cells[0].Value = null;
            }
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            DisposeRowImages();
            _missingFont.Dispose();
        }

        base.Dispose(disposing);
    }
}
