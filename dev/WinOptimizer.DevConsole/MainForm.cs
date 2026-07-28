using System.Drawing;
using System.Windows.Forms;
using WinOptimizer.DevConsole.Views;
using WinOptimizer.DevConsole.Models;
using WinOptimizer.DevConsole.Services;

namespace WinOptimizer.DevConsole;

/// <summary>
/// Gelistirici kontrol paneli ana penceresi. Sol panelde kategori/komut butonlari,
/// sagda renk kodlu canli cikti konsolu, altta CLI secici + Durdur butonu.
/// WinForms (kod-gomulu UI — designer .cs yok; tek dosyada yerlesim).
/// </summary>
public sealed class MainForm : Form
{
    private readonly CommandRunner _runner = new();
    private readonly RichTextBox _output = new();
    private readonly Label _status = new();
    private readonly Button _stopBtn = new();
    private readonly ComboBox _cliCombo = new();
    private readonly Button _cliRunBtn = new();
    private readonly FlowLayoutPanel _leftPanel = new();
    private readonly ListBox _history = new();
    private readonly ToolTip _toolTip = new();

    private const int MaxOutputLines = 5000;

    public MainForm()
    {
        Text = "WinOptimizer DevConsole - Gelistirici Kontrol Paneli";
        Width = 1180;
        Height = 760;
        StartPosition = FormStartPosition.CenterScreen;
        MinimumSize = new Size(1000, 640);
        Font = new Font("Segoe UI", 9F);

        BuildLayout();
        WireRunner();
        PopulateButtons();
        PopulateCliCombo();
        UpdateStatus();
    }

    private void BuildLayout()
    {
        var root = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 3 };
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 32));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 64));

        var topBar = new Panel { Dock = DockStyle.Fill, BackColor = Color.FromArgb(32, 32, 32) };
        _status.Dock = DockStyle.Fill;
        _status.ForeColor = Color.White;
        _status.TextAlign = ContentAlignment.MiddleLeft;
        _status.Padding = new Padding(10, 0, 10, 0);
        _status.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
        var settingsBtn = new Button
        {
            Text = "Ayarlar", Dock = DockStyle.Right, Width = 80, FlatStyle = FlatStyle.Flat,
            BackColor = Color.FromArgb(60, 60, 60), ForeColor = Color.White
        };
        settingsBtn.FlatAppearance.BorderSize = 0;
        settingsBtn.Click += (_, _) =>
        {
            using var dlg = new SettingsForm();
            if (dlg.ShowDialog(this) == DialogResult.OK)
            {
                AppendOutput(new OutputLine("dotnet yolu ayari degistirildi - yeniden acinca etkinlesir.", OutputLevel.Warning));
                UpdateStatus();
            }
        };
        topBar.Controls.Add(settingsBtn);
        topBar.Controls.Add(_status);

        var content = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 3, RowCount = 1 };
        content.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 230));
        content.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        content.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 200));

        _leftPanel.Dock = DockStyle.Fill;
        _leftPanel.FlowDirection = FlowDirection.TopDown;
        _leftPanel.WrapContents = false;
        _leftPanel.AutoScroll = true;
        _leftPanel.Padding = new Padding(8);

        _output.Dock = DockStyle.Fill;
        _output.ReadOnly = true;
        _output.BackColor = Color.FromArgb(24, 24, 24);
        _output.Font = new Font("Consolas", 9F);
        _output.WordWrap = false;
        _output.ScrollBars = RichTextBoxScrollBars.Vertical;

        _history.Dock = DockStyle.Fill;
        _history.Font = new Font("Consolas", 8.5F);
        _history.IntegralHeight = false;

        content.Controls.Add(_leftPanel, 0, 0);
        content.Controls.Add(_output, 1, 0);
        content.Controls.Add(WrapWithLabel("Gecmis", _history), 2, 0);

        var bottomBar = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 4, RowCount = 1 };
        bottomBar.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 80));
        bottomBar.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        bottomBar.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 110));
        bottomBar.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 110));

        var cliLabel = new Label { Text = "CLI:", Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft, Padding = new Padding(10, 0, 0, 0) };
        _cliCombo.Dock = DockStyle.Fill;
        _cliCombo.DropDownStyle = ComboBoxStyle.DropDownList;
        _cliCombo.Margin = new Padding(4);
        _cliRunBtn.Text = "Calistir";
        _cliRunBtn.Dock = DockStyle.Fill;
        _cliRunBtn.Margin = new Padding(4);
        _cliRunBtn.Click += async (_, _) => await RunSelectedCliAsync();
        _stopBtn.Text = "Durdur";
        _stopBtn.Dock = DockStyle.Fill;
        _stopBtn.Margin = new Padding(4);
        _stopBtn.Enabled = false;
        _stopBtn.BackColor = Color.FromArgb(180, 40, 40);
        _stopBtn.ForeColor = Color.White;
        _stopBtn.Click += (_, _) => _runner.Cancel();

        bottomBar.Controls.Add(cliLabel, 0, 0);
        bottomBar.Controls.Add(_cliCombo, 1, 0);
        bottomBar.Controls.Add(_cliRunBtn, 2, 0);
        bottomBar.Controls.Add(_stopBtn, 3, 0);

        root.Controls.Add(topBar, 0, 0);
        root.Controls.Add(content, 0, 1);
        root.Controls.Add(bottomBar, 0, 2);
        Controls.Add(root);
    }

    private static Panel WrapWithLabel(string text, Control inner)
    {
        var p = new Panel { Dock = DockStyle.Fill, Padding = new Padding(4) };
        var lbl = new Label { Text = text, Dock = DockStyle.Top, Height = 22, TextAlign = ContentAlignment.MiddleLeft, Font = new Font("Segoe UI", 8.5F, FontStyle.Bold) };
        inner.Dock = DockStyle.Fill;
        p.Controls.Add(inner);
        p.Controls.Add(lbl);
        return p;
    }

    private void WireRunner()
    {
        _runner.OutputReceived += line => BeginInvoke(() => AppendOutput(line));
        _runner.Completed += result => BeginInvoke(() => OnCompleted(result));
    }

    private void PopulateButtons()
    {
        _leftPanel.Controls.Clear();
        string? currentCategory = null;
        foreach (var cmd in CommandCatalog.All)
        {
            if (cmd.Category != currentCategory)
            {
                currentCategory = cmd.Category;
                var header = new Label
                {
                    Text = cmd.Category,
                    Font = new Font("Segoe UI", 9F, FontStyle.Bold),
                    ForeColor = Color.FromArgb(0, 120, 215),
                    Margin = new Padding(0, 10, 0, 2),
                    Width = 210
                };
                _leftPanel.Controls.Add(header);
            }

            var btn = new Button
            {
                Text = cmd.Title,
                Width = 210,
                Height = 30,
                Margin = new Padding(0, 1, 0, 1),
                TextAlign = ContentAlignment.MiddleLeft,
                Padding = new Padding(8, 0, 0, 0),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(45, 45, 45),
                ForeColor = Color.White,
                Tag = cmd
            };
            btn.FlatAppearance.BorderSize = 0;
            if (cmd.Description is not null)
            {
                _toolTip.SetToolTip(btn, cmd.Description);
            }

            btn.Click += async (_, _) => await RunCommandAsync(cmd);
            _leftPanel.Controls.Add(btn);
        }
    }

    private void PopulateCliCombo()
    {
        _cliCombo.Items.Clear();
        foreach (var cmd in CommandCatalog.All.Where(c => c.Category == "CLI"))
        {
            _cliCombo.Items.Add(cmd);
        }

        if (_cliCombo.Items.Count > 0)
        {
            _cliCombo.SelectedIndex = 0;
        }
    }

    private async Task RunCommandAsync(DevCommand cmd)
    {
        if (_runner.IsRunning)
        {
            AppendOutput(new OutputLine("! Onceki komut hala calisiyor - Durdur ile bitirin.", OutputLevel.Warning));
            return;
        }

        if (cmd.IsFolder)
        {
            OpenFolder(cmd);
            return;
        }

        // Ozel isaretli komutlar (surec degil, diyalog).
        if (cmd.File == "__coverage__")
        {
            using var dlg = new CoverageForm();
            dlg.ShowDialog(this);
            return;
        }

        SetBusy(true);
        await _runner.RunAsync(cmd);
    }

    private async Task RunSelectedCliAsync()
    {
        if (_cliCombo.SelectedItem is DevCommand cmd)
        {
            await RunCommandAsync(cmd);
        }
    }

    private void OpenFolder(DevCommand cmd)
    {
        string path = cmd.Args.Count > 0 ? cmd.Args[0] : ".";
        if (path == ".") path = DevPaths.SolutionRoot;
        if (!Path.IsPathRooted(path))
        {
            path = Path.Combine(DevPaths.SolutionRoot, path);
        }

        if (Directory.Exists(path))
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("explorer.exe", path)
            {
                UseShellExecute = true
            });
        }
        else
        {
            AppendOutput(new OutputLine("! Klasor yok: " + path + " (once ilgili komutu calistirin)", OutputLevel.Warning));
        }
    }

    private void AppendOutput(OutputLine line)
    {
        _output.SelectionStart = _output.TextLength;
        _output.SelectionLength = 0;
        _output.SelectionColor = ColorForLevel(line.Level);
        _output.SelectionFont = line.Level == OutputLevel.Command
            ? new Font(_output.Font, FontStyle.Bold)
            : _output.Font;
        _output.AppendText("[" + line.Timestamp.ToString("HH:mm:ss") + "] " + line.Text + Environment.NewLine);
        _output.ScrollToCaret();

        if (_output.Lines.Length > MaxOutputLines)
        {
            int removeChars = _output.GetFirstCharIndexFromLine(_output.Lines.Length - MaxOutputLines);
            _output.Select(0, removeChars);
            _output.SelectedText = "";
        }
    }

    private static Color ColorForLevel(OutputLevel level) => level switch
    {
        OutputLevel.Info => Color.FromArgb(220, 220, 220),
        OutputLevel.Warning => Color.FromArgb(230, 180, 40),
        OutputLevel.Error => Color.FromArgb(230, 80, 80),
        OutputLevel.Success => Color.FromArgb(80, 200, 120),
        OutputLevel.Command => Color.FromArgb(120, 180, 255),
        _ => Color.White
    };

    private void OnCompleted(CommandResult result)
    {
        _history.Items.Insert(0, result.StatusIcon + " " + result.Title + " (" + result.Duration.TotalSeconds.ToString("F1") + "s)");
        if (_history.Items.Count > 50) _history.Items.RemoveAt(_history.Items.Count - 1);
        SetBusy(false);
        UpdateStatus();
    }

    private void SetBusy(bool busy)
    {
        _stopBtn.Enabled = busy;
        _cliRunBtn.Enabled = !busy;
    }

    private void UpdateStatus()
    {
        string root = DevPaths.SolutionRoot;
        _status.Text = "  Cozum: " + root + "    |    dotnet " + DevPaths.DotnetVersion + "    |    " +
                       (_runner.IsRunning ? "MEYGUL - komut calisiyor..." : "Hazir");
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _runner.Dispose();
        }

        base.Dispose(disposing);
    }
}