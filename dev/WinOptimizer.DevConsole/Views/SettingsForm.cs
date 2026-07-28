using System.Drawing;
using System.Windows.Forms;
using WinOptimizer.DevConsole.Services;

namespace WinOptimizer.DevConsole.Views;

/// <summary>
/// Ayarlar diyalogu — dotnet exe yolunu elle gecersiz kilmayi saglar.
/// Bos birakilirsa DevPaths otomatik tespite (kullanici ayari > ~/.dotnet > PATH) doner.
/// </summary>
public sealed class SettingsForm : Form
{
    private readonly TextBox _dotnetPath = new();
    private readonly UserSettings _settings;

    public SettingsForm()
    {
        _settings = UserSettings.Load();

        Text = "Ayarlar - DevConsole";
        Width = 540;
        Height = 240;
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        Font = new Font("Segoe UI", 9F);

        var table = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(16), ColumnCount = 1, RowCount = 4 };
        table.RowStyles.Add(new RowStyle(SizeType.Absolute, 28));
        table.RowStyles.Add(new RowStyle(SizeType.Absolute, 32));
        table.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        table.RowStyles.Add(new RowStyle(SizeType.Absolute, 40));

        var label = new Label { Text = "dotnet.exe yolu (gecersiz kilma)", Dock = DockStyle.Fill, TextAlign = ContentAlignment.BottomLeft };
        var pathRow = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1 };
        pathRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        pathRow.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 90));
        _dotnetPath.Dock = DockStyle.Fill;
        _dotnetPath.Text = _settings.DotnetOverride ?? string.Empty;
        _dotnetPath.Margin = new Padding(0, 0, 4, 0);
        var browse = new Button { Text = "Goza...", Dock = DockStyle.Fill };
        browse.Click += (_, _) => Browse();
        pathRow.Controls.Add(_dotnetPath, 0, 0);
        pathRow.Controls.Add(browse, 1, 0);

        var auto = new Label
        {
            Dock = DockStyle.Fill,
            Text = "  Bos birakilirsa otomatik tespit kullanilir:  kullanici ayari > ~/.dotnet/dotnet.exe > PATH." + Environment.NewLine + "  Mevcut tespit edilen: " + DevPaths.Dotnet,
            ForeColor = Color.Gray,
            Font = new Font("Segoe UI", 8.5F)
        };

        var btns = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft, WrapContents = false };
        var save = new Button { Text = "Kaydet", Width = 90, Height = 30 };
        var cancel = new Button { Text = "Iptal", Width = 90, Height = 30, DialogResult = DialogResult.Cancel };
        save.Click += (_, _) =>
        {
            _settings.DotnetOverride = string.IsNullOrWhiteSpace(_dotnetPath.Text) ? null : _dotnetPath.Text.Trim();
            _settings.Save();
            DialogResult = DialogResult.OK;
            Close();
        };
        btns.Controls.Add(cancel);
        btns.Controls.Add(save);

        table.Controls.Add(label, 0, 0);
        table.Controls.Add(pathRow, 0, 1);
        table.Controls.Add(auto, 0, 2);
        table.Controls.Add(btns, 0, 3);
        Controls.Add(table);
        AcceptButton = save;
        CancelButton = cancel;
    }

    private void Browse()
    {
        using var dlg = new OpenFileDialog
        {
            Filter = "dotnet.exe|dotnet.exe|Calistirilabilir (*.exe)|*.exe|Tum dosyalar (*.*)|*.*",
            Title = "dotnet.exe sec"
        };
        if (dlg.ShowDialog(this) == DialogResult.OK)
        {
            _dotnetPath.Text = dlg.FileName;
        }
    }
}