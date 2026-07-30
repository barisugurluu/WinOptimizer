using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WinOptimizer.Orchestration;
using WinOptimizer.Orchestration.Preflight;

namespace WinOptimizer.App.ViewModels;

/// <summary>Gereksinim listesinde gösterilen tek satır.</summary>
public sealed class RequirementRow
{
    public required string Glyph { get; init; }
    public required string Title { get; init; }
    public required string Detail { get; init; }
    public string? RemedyHint { get; init; }
    public bool HasRemedy => !string.IsNullOrEmpty(RemedyHint);
}

/// <summary>
/// İlk açılış penceresi görünüm modeli — gereksinim raporu + "bu araç ne yapar/ne yapmaz"
/// özeti + isteğe bağlı hizmet kurulumu.
/// </summary>
/// <remarks>
/// Bu ekrandan önce uygulamanın hiçbir ön koşul kontrolü yoktu: yönetici değilse, WMI
/// bozuksa ya da veri dizini yazılamıyorsa kullanıcı bunu ancak modüller tek tek
/// başarısız olurken (ya da hiç) anlıyordu.
/// </remarks>
public partial class FirstRunViewModel : ObservableObject
{
    private readonly SystemRequirementsChecker _checker;
    private readonly GuardServiceController _guardService;
    private readonly SettingsService _settings;

    [ObservableProperty] private bool _isChecking = true;
    [ObservableProperty] private bool _hasBlocking;
    [ObservableProperty] private bool _hasWarnings;
    [ObservableProperty] private bool _installGuardService;
    [ObservableProperty] private bool _canInstallGuardService;
    [ObservableProperty] private string _statusMessage = string.Empty;

    /// <summary>Kullanıcı "Başla"ya bastı mı (pencere kapanırken okunur).</summary>
    public bool Continued { get; private set; }

    public ObservableCollection<RequirementRow> Requirements { get; } = new();

    /// <summary>Kullanıcıya gösterilen "ne yapar / ne yapmaz" maddeleri (CLAUDE.md §3).</summary>
    public IReadOnlyList<string> Principles { get; } =
    [
        "Yıkıcı değil, onarıcıdır — silmek yerine önce onarmayı dener.",
        "Geri alınabilirdir — her değişiklik kayda geçer ve geri alınabilir.",
        "Şeffaftır — uygulamadan önce hangi eylemin ne yapacağını gösterir.",
        "Güvenli varsayılanlar — riskli ayarlar kapalı gelir, ek onay ister.",
        "Windows Defender ASLA kapatılmaz; kritik hizmetlere dokunulmaz.",
    ];

    public FirstRunViewModel(
        SystemRequirementsChecker checker,
        GuardServiceController guardService,
        SettingsService settings)
    {
        _checker = checker;
        _guardService = guardService;
        _settings = settings;
    }

    /// <summary>Gereksinim kontrolünü çalıştırıp listeyi doldurur.</summary>
    public async Task LoadAsync()
    {
        IsChecking = true;
        try
        {
            var report = await _checker.RunAsync();
            Requirements.Clear();
            foreach (var check in report.Checks)
            {
                Requirements.Add(new RequirementRow
                {
                    Glyph = check.Severity switch
                    {
                        RequirementSeverity.Ok => "✓",
                        RequirementSeverity.Warning => "⚠",
                        _ => "✕",
                    },
                    Title = check.Title,
                    Detail = check.Detail,
                    RemedyHint = check.RemedyHint,
                });
            }

            HasBlocking = report.HasBlocking;
            HasWarnings = report.HasWarnings;

            // Hizmet zaten kuruluysa ya da exe yoksa kutu anlamsız.
            CanInstallGuardService =
                GuardServiceController.ResolveServiceExePath() is not null &&
                _guardService.GetState() == GuardServiceState.NotInstalled;
        }
        finally
        {
            IsChecking = false;
        }
    }

    /// <summary>Gereksinim raporunun düz metni (teşhis paketi ve destek için).</summary>
    public async Task<string> BuildReportTextAsync() => (await _checker.RunAsync()).ToPlainText();

    [RelayCommand]
    private async Task ContinueAsync()
    {
        if (InstallGuardService && CanInstallGuardService)
        {
            StatusMessage = "Hizmet kuruluyor…";
            bool ok = await _guardService.InstallAsync();
            StatusMessage = ok
                ? "Hizmet kuruldu."
                : "Hizmet kurulamadı — Guard sekmesinden tekrar deneyebilirsiniz.";
        }

        // Sihirbaz bir daha gösterilmez (bu sürüm için).
        _settings.Current.FirstRunCompletedVersion =
            typeof(FirstRunViewModel).Assembly.GetName().Version?.ToString() ?? "0.0.0";
        _settings.Save();

        Continued = true;
        RequestClose?.Invoke(this, EventArgs.Empty);
    }

    [RelayCommand]
    private void Close()
    {
        Continued = false;
        RequestClose?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Pencerenin kapanmasını ister (View bu olaya abone olur).</summary>
    public event EventHandler? RequestClose;
}
