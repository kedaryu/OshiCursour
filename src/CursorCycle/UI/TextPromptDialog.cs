namespace CursorCycle.UI;

internal sealed class TextPromptDialog : Form
{
    private readonly TextBox _textBox = new();

    private TextPromptDialog(string title, string message, string initialValue)
    {
        Text = title;
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowInTaskbar = false;
        ClientSize = new Size(420, 145);
        Font = new Font("Yu Gothic UI", 9F);

        var messageLabel = new Label
        {
            AutoSize = true,
            Text = message,
            Location = new Point(16, 15)
        };

        _textBox.Location = new Point(16, 43);
        _textBox.Size = new Size(388, 27);
        _textBox.Text = initialValue;
        _textBox.SelectAll();

        var okButton = new Button
        {
            Text = "OK",
            DialogResult = DialogResult.OK,
            Location = new Point(242, 92),
            Size = new Size(78, 32)
        };

        var cancelButton = new Button
        {
            Text = "キャンセル",
            DialogResult = DialogResult.Cancel,
            Location = new Point(326, 92),
            Size = new Size(78, 32)
        };

        Controls.AddRange([messageLabel, _textBox, okButton, cancelButton]);
        AcceptButton = okButton;
        CancelButton = cancelButton;
    }

    public static string? Show(
        IWin32Window owner,
        string title,
        string message,
        string initialValue = "")
    {
        using var dialog = new TextPromptDialog(title, message, initialValue);
        return dialog.ShowDialog(owner) == DialogResult.OK
            ? dialog._textBox.Text.Trim()
            : null;
    }
}
