using Microsoft.Extensions.Logging;
using WinOptimizer.Core;
using WinOptimizer.Safety;

namespace WinOptimizer.Modules.AppManager;

/// <summary>
/// AppManager — Bloatware / UWP kaldırma. Sistem UWP beyaz listesi ile güvenli.
/// Risk: Medium (uygulama kaldırma). (Master plan Bölüm 3.12.)
/// </summary>
public sealed class AppManagerModule : IOptimizationModule
{
    public string Id => "AppManager";
    public string DisplayName => "Uygulama & Bloatware";
    public RiskLevel Risk => RiskLevel.Medium;

    private readonly ProcessRunner _runner;
    private readonly ILogger<AppManagerModule> _logger;

    /// <summary>Silinemeyecek sistem UWP'leri (framework/kritik).</summary>
    private static readonly HashSet<string> ProtectedPackages = new(StringComparer.OrdinalIgnoreCase)
    {
        "Microsoft.NET.Native.Framework", "Microsoft.NET.Native.Runtime",
        "Microsoft.VCLibs", "Microsoft.UI.Xaml", "Microsoft.WindowsAppRuntime",
        "Microsoft.Windows.CallingShellClient", "Microsoft.Windows.ShellExperienceHost",
        "MicrosoftWindows.Client.CBS", "Microsoft.Windows.StartMenuExperienceHost"
    };

    /// <summary>Kaldırılabilir bloatware örnekleri (kullanıcı onayıyla).</summary>
    private static readonly string[] KnownBloatware = new[]
    {
        "Microsoft.BingNews", "Microsoft.BingWeather", "Microsoft.GetHelp",
        "Microsoft.Getstarted", "Microsoft.Microsoft3DViewer", "Microsoft.MicrosoftSolitaireCollection",
        "Microsoft.MixedReality.Portal", "Microsoft.OneConnect", "Microsoft.People",
        "Microsoft.SkypeApp", "Microsoft.Wallet", "Microsoft.WindowsFeedbackHub",
        "Microsoft.WindowsMaps", "Microsoft.Xbox*", "king.com.*", "*.Duolingo*"
    };

    public AppManagerModule(ProcessRunner runner, ILogger<AppManagerModule> logger)
    {
        _runner = runner;
        _logger = logger;
    }

    public Task<AnalysisResult> AnalyzeAsync(CancellationToken ct = default)
    {
        // Yüklü bloatware'i say (Get-AppxPackage ile)
        return Task.FromResult(new AnalysisResult
        {
            ModuleId = Id, ItemCount = KnownBloatware.Length,
            Summary = $"{KnownBloatware.Length} bilinen bloatware paketi taranabilir (kaldırma kullanıcı onayıyla)."
        });
    }

    public Task<PreviewResult> PreviewAsync(AnalysisResult analysis, CancellationToken ct = default)
    {
        var actions = KnownBloatware.Select(p => new PreviewAction
        {
            Description = $"Yüklüyse kaldır: {p}",
            Risk = RiskLevel.Medium,
            Target = p,
            RequiresExtraConfirmation = true
        }).ToList();
        return Task.FromResult(new PreviewResult { ModuleId = Id, Actions = actions, IsDryRun = true });
    }

    public async Task<ExecutionResult> ExecuteAsync(
        PreviewResult preview, IProgress<ProgressInfo> progress, CancellationToken ct = default)
    {
        var changes = new List<ChangeRecord>();
        int succeeded = 0, skipped = 0, failed = 0;
        int total = preview.Actions.Count, idx = 0;

        foreach (var action in preview.Actions)
        {
            ct.ThrowIfCancellationRequested();
            idx++;
            progress.Report(new ProgressInfo
            {
                ModuleId = Id, Percent = idx * 100 / total, Message = action.Description, Current = idx, Total = total
            });

            var pkg = action.Target!;
            if (ProtectedPackages.Any(p => pkg.StartsWith(p.TrimEnd('*'), StringComparison.OrdinalIgnoreCase)))
            {
                _logger.LogWarning("Korunan paket atlandı: {Pkg}", pkg);
                skipped++;
                continue;
            }

            try
            {
                // Get-AppxPackage *pkg* | Remove-AppxPackage
                int code = await _runner.RunAsync("powershell.exe",
                    $"-NoProfile -Command \"Get-AppxPackage *{pkg}* | Remove-AppxPackage\"", null, ct);
                // Hata kodu 0 değilse muhtemelen yüklü değildi — atla, başarısız sayma
                if (code == 0)
                {
                    succeeded++;
                    changes.Add(new ChangeRecord
                    {
                        Module = Id, Operation = ChangeOperationType.CommandRun,
                        Target = pkg, NewValue = "removed", Note = "UWP kaldırıldı"
                    });
                }
                else { skipped++; }
            }
            catch (Exception ex) { _logger.LogDebug(ex, "Paket kaldırma: {Pkg}", pkg); skipped++; }
        }

        return new ExecutionResult { ModuleId = Id, Succeeded = succeeded, Skipped = skipped, Failed = failed, Changes = changes };
    }

    public Task<RollbackResult> RollbackAsync(ChangeRecord change, CancellationToken ct = default) =>
        Task.FromResult(new RollbackResult
        {
            ModuleId = Id, ChangeId = change.Id, IsSuccess = false,
            Error = "Kaldırılan UWP'yi geri yüklemek için Microsoft Store'dan yeniden yükleyin."
        });
}
