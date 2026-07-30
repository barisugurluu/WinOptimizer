using System.Drawing;
using System.Windows.Forms;
using WinOptimizer.DevConsole.Models;
using WinOptimizer.DevConsole.Services;

namespace WinOptimizer.DevConsole.Views;

/// <summary>
/// Kod kapsami goruntuleme diyalogu. Cobertura raporlarini proje bazinda listeler,
/// 18.3 esiklerine gore renklendirir (Core/Safety %85, moduller %70).
/// </summary>
public sealed class CoverageForm : Form
{
    public CoverageForm()
    {
        Text = "Kod Kapsami - Cobertura";
        Width = 720;
        Height = 520;
        StartPosition = FormStartPosition.CenterParent;
        Font = new Font("Segoe UI", 9F);
        MinimumSize = new Size(600, 400);

        var grid = new DataGridView
        {
            Dock = DockStyle.Fill,
            AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
            AllowUserToAddRows = false,
            AllowUserToDeleteRows = false,
            AllowUserToResizeRows = false,
            ReadOnly = true,
            SelectionMode = DataGridViewSelectionMode.FullRowSelect,
            RowHeadersVisible = false,
            BorderStyle = BorderStyle.None,
            BackgroundColor = Color.White,
            DefaultCellStyle = new DataGridViewCellStyle { Font = new Font("Segoe UI", 9.5F), SelectionBackColor = Color.FromArgb(0, 120, 215), SelectionForeColor = Color.White }
        };
        grid.Columns.Add("Project", "Proje");
        grid.Columns.Add("Line", "Satir %");
        grid.Columns["Line"].FillWeight = 70;
        grid.Columns.Add("Branch", "Dal %");
        grid.Columns["Branch"].FillWeight = 70;
        grid.Columns.Add("Status", "Esik (18.3)");
        grid.Columns["Status"].FillWeight = 90;

        var summary = new Label { Dock = DockStyle.Top, Height = 28, TextAlign = ContentAlignment.MiddleLeft, Padding = new Padding(12, 0, 0, 0), Font = new Font("Segoe UI", 9F, FontStyle.Bold) };
        var hint = new Label { Dock = DockStyle.Bottom, Height = 40, Text = "  Eikler: Core/Safety >= %85, moduller >= %70.  |  Veri: tests/*/TestResults ve dev/cov-tmp icindeki cobertura XMLleri.  |  Once Test + Kapsam calistirin.", Font = new Font("Segoe UI", 8F), ForeColor = Color.Gray, TextAlign = ContentAlignment.MiddleLeft };

        Controls.Add(grid);
        Controls.Add(summary);
        Controls.Add(hint);

        Populate(grid, summary);
    }

    private void Populate(DataGridView grid, Label summary)
    {
        var entries = CoverageParser.Collect();
        if (entries.Count == 0)
        {
            summary.Text = "  Kapsam raporu bulunamadi. Once 'Test + Kapsam' calistirin.";
            summary.ForeColor = Color.FromArgb(180, 80, 80);
            return;
        }

        foreach (var e in entries)
        {
            int idx = grid.Rows.Add(e.Project, e.LinePercent.ToString("F1"), (e.BranchPercent?.ToString("F1")) ?? "-", e.MeetsThreshold ? "GECER" : "ALTINDA");
            var row = grid.Rows[idx];
            Color color = !e.MeetsThreshold
                ? Color.FromArgb(255, 220, 220)
                : Color.FromArgb(220, 255, 220);
            Color statusColor = e.MeetsThreshold ? Color.FromArgb(40, 140, 60) : Color.FromArgb(180, 60, 60);
            row.DefaultCellStyle.BackColor = color;
            row.Cells["Status"].Style.ForeColor = statusColor;
            row.Cells["Status"].Style.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
        }

        double avg = entries.Average(e => e.LinePercent);
        int pass = entries.Count(e => e.MeetsThreshold);
        summary.Text = "  " + entries.Count + " proje | ortalama satir kapsami %" + avg.ToString("F1") + " | " + pass + "/" + entries.Count + " esik gecer";
        summary.ForeColor = avg >= 75 ? Color.FromArgb(40, 140, 60) : Color.FromArgb(180, 100, 30);
    }
}
