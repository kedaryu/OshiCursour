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

    private readonly DataGridView _grid = new();

    public CursorDetailsForm(
        CursorPreset preset,
        CursorSetScanResult scan,
        bool useDarkMode)
    {
        Text = $"{preset.Name} のカーソル一覧";
        Icon = Icon.ExtractAssociatedIcon(System.Windows.Forms.Application.ExecutablePath);
        StartPosition = FormStartPosition.CenterParent;
        MinimumSize = new Size(680, 480);
        ClientSize = new Size(840, 600);
        Font = new Font("Yu Gothic UI", 9.5F);
        BackColor = PageBackground;
        ForeColor = TextColor;
        ShowInTaskbar = false;

        Controls.Add(BuildGrid(scan));
        Controls.Add(BuildFooter());
        Controls.Add(BuildHeader(preset, scan));
        ApplyTheme(useDarkMode);
    }

    private Control BuildHeader(CursorPreset preset, CursorSetScanResult scan)
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
            Text = preset.Name
        };
        var summary = new Label
        {
            AutoSize = true,
            Location = new Point(24, 44),
            ForeColor = scan.IsValid ? Color.FromArgb(38, 128, 91) : Color.Firebrick,
            Text = $"検出: {scan.MatchedRoleCount}/{CursorRoles.All.Count} 種類"
        };
        var folder = new Label
        {
            AutoEllipsis = true,
            Location = new Point(150, 44),
            Size = new Size(650, 24),
            Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
            ForeColor = MutedTextColor,
            Text = preset.FolderPath
        };

        header.Controls.Add(title);
        header.Controls.Add(summary);
        header.Controls.Add(folder);
        return header;
    }

    private Control BuildGrid(CursorSetScanResult scan)
    {
        _grid.Dock = DockStyle.Fill;
        _grid.Margin = new Padding(20);
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
            HeaderText = "検出状態",
            Width = 100,
            SortMode = DataGridViewColumnSortMode.NotSortable
        });
        _grid.Columns.Add(new DataGridViewTextBoxColumn
        {
            HeaderText = "ファイル",
            AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill,
            MinimumWidth = 260,
            SortMode = DataGridViewColumnSortMode.NotSortable
        });

        foreach (var role in CursorRoles.All)
        {
            var detected = scan.FilesByRegistryName.TryGetValue(role.RegistryName, out var path);
            var thumbnail = detected ? CursorThumbnailLoader.Load(path) : null;
            var rowIndex = _grid.Rows.Add(
                thumbnail,
                role.DisplayName,
                detected ? "検出済み" : "未検出",
                detected ? Path.GetFileName(path) : "—");
            var row = _grid.Rows[rowIndex];
            row.Cells[3].ToolTipText = detected ? path : string.Empty;
            if (!detected)
            {
                row.Cells[2].Style.ForeColor = Color.Firebrick;
                row.Cells[2].Style.Font = new Font(Font, FontStyle.Bold);
                row.Cells[3].Style.ForeColor = MutedTextColor;
            }
        }

        _grid.ClearSelection();
        _grid.CurrentCell = null;
        _grid.SelectionChanged += (_, _) => _grid.ClearSelection();

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
            Height = 62,
            BackColor = Color.White,
            Padding = new Padding(20, 12, 20, 12)
        };
        var closeButton = new Button
        {
            Text = "閉じる",
            Dock = DockStyle.Right,
            Width = 120,
            FlatStyle = FlatStyle.Flat,
            BackColor = PrimaryDarkColor,
            ForeColor = Color.White,
            Font = new Font(Font, FontStyle.Bold)
        };
        closeButton.FlatAppearance.BorderSize = 0;
        closeButton.Click += (_, _) => Close();
        footer.Controls.Add(closeButton);
        AcceptButton = closeButton;
        CancelButton = closeButton;
        return footer;
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
        WindowsThemeService.ApplyToWindow(this, true);
        WindowsThemeService.ApplyToControl(_grid, true);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            foreach (DataGridViewRow row in _grid.Rows)
            {
                if (row.Cells.Count > 0 && row.Cells[0].Value is Image image)
                {
                    image.Dispose();
                    row.Cells[0].Value = null;
                }

                row.Cells[2].Style.Font?.Dispose();
            }
        }

        base.Dispose(disposing);
    }
}
