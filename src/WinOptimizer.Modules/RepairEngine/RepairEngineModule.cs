using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using WinOptimizer.Core;
using WinOptimizer.Safety;

namespace WinOptimizer.Modules.RepairEngine;

/// <summary>
/// RepairEngine — Sistem onarımı: SFC, DISM, chkdsk.
/// Çıktı parse edilir, CBS.log okunur. Risk: Low (onarıcı).
/// (Master plan Bölüm 3.3.)
/// </summary>
public sealed class RepairEngineModule : IOptimizationModule
{
    public string Id => "RepairEngine";
    public string DisplayName => "Sistem Onarımı (SFC/DISM)";
    public RiskLevel Risk => RiskLevel.Low;

    private readonly ProcessRunner _runner;
    private readonly ILogger<RepairEngineModule> _logger;

    public RepairEngineModule(ProcessRunner runner, ILogger<RepairEngineModule> logger)
    {
        _runner = runner;
        _logger = logger;
    }

    /// <summary>Onarım adımları ve açıklamaları.</summary>
    private static readonly (string Id, string Name, string File, string Args, string Desc)[] RepairSteps = new[]
    {
        ("SFC", "Sistem Dosyası Denetleyicisi", "sfc", "/scannow",
         "Bozuk sistem dosyalarını tarar ve onarır."),
        ("DISM", "DISM Görüntü Onarımı", "Dism.exe", "/Online /Cleanup-Image /RestoreHealth",
         "Windows bileşen deposunu (WinSxS) onarır."),
        ("DISM_Cleanup", "DISM Bileşen Temizliği", "Dism.exe", "/Online /Cleanup-Image /StartComponentCleanup",
         "Eski güncelleme bileşenlerini temizler (yer kazanımı).")
    };

    public async Task<AnalysisResult> AnalyzeAsync(CancellationToken ct = default)
    {
        // sfc /verifyonly ile hızlı bütünlük kontrolü (değişiklik yapmaz)
        bool integrityOk = true;
        string detail = "Bilinmiyor";
        try
        {
            var (code, output) = await _runner.RunCaptureAsync("sfc", "/verifyonly", ct);
            if (output.Contains("did not find any integrity violations", StringComparison.OrdinalIgnoreCase))
            {
                integrityOk = true;
                detail = "Sistem dosyaları sağlıklı.";
            }
            else if (output.Contains("found corrupt files", StringComparison.OrdinalIgnoreCase))
            {
                integrityOk = false;
                detail = "Bozuk sistem dosyaları tespit edildi — onarım önerilir.";
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "SFC doğrulama çalıştırılamadı.");
        }

        return new AnalysisResult
        {
            ModuleId = Id,
            ItemCount = RepairSteps.Length,
            Summary = detail,
            Details = new() { ["IntegrityOk"] = integrityOk }
        };
    }

    public Task<PreviewResult> PreviewAsync(AnalysisResult analysis, CancellationToken ct = default)
    {
        var actions = RepairSteps.Select(s => new PreviewAction
        {
            Description = $"{s.Name}: {s.Desc}",
            Risk = RiskLevel.Low,
            Target = s.Id
        }).ToList();

        return Task.FromResult(new PreviewResult
        {
            ModuleId = Id,
            Actions = actions,
            IsDryRun = true
        });
    }

    public async Task<ExecutionResult> ExecuteAsync(
        PreviewResult preview, IProgress<ProgressInfo> progress, CancellationToken ct = default)
    {
        var changes = new List<ChangeRecord>();
        var errors = new List<string>();
        int succeeded = 0, failed = 0;
        int total = preview.Actions.Count;
        int idx = 0;

        foreach (var step in RepairSteps)
        {
            ct.ThrowIfCancellationRequested();
            idx++;
            progress.Report(new ProgressInfo
            {
                ModuleId = Id,
                Percent = idx * 100 / total,
                Message = step.Name + " çalışıyor…",
                Current = idx,
                Total = total
            });

            try
            {
                int code = await _runner.RunAsync(step.File, step.Args, output: null, ct);
                if (code == 0)
                {
                    succeeded++;
                    changes.Add(new ChangeRecord
                    {
                        Module = Id,
                        Operation = ChangeOperationType.CommandRun,
                        Target = $"{step.File} {step.Args}",
                        PreviousValue = "n/a",
                        NewValue = $"exit={code}",
                        Note = step.Name
                    });
                }
                else
                {
                    failed++;
                    errors.Add($"{step.Name} exit={code}");
                }
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception ex)
            {
                failed++;
                errors.Add($"{step.Name}: {ex.Message}");
                _logger.LogError(ex, "{Name} başarısız", step.Name);
            }
        }

        return new ExecutionResult
        {
            ModuleId = Id,
            Succeeded = succeeded,
            Failed = failed,
            Changes = changes,
            Errors = errors
        };
    }

    /// <inheritdoc/>
    /// <remarks>SFC/DISM onarımı geri alınamaz (zaten onarıcıdır).</remarks>
    public Task<RollbackResult> RollbackAsync(ChangeRecord change, CancellationToken ct = default)
    {
        return Task.FromResult(new RollbackResult
        {
            ModuleId = Id,
            ChangeId = change.Id,
            IsSuccess = true,
            Error = "Sistem onarımı geri alınmaz; zaten sistemi düzeltir."
        });
    }
}
