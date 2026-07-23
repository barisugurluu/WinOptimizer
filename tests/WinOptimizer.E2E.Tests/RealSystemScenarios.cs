using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using WinOptimizer.Modules.CleanEngine;
using WinOptimizer.Modules.HardwareMonitor;
using WinOptimizer.Modules.MemoryEngine;
using WinOptimizer.Safety;
using Xunit;

namespace WinOptimizer.E2E.Tests;

/// <summary>
/// E2E senaryoları — gerçek sistemde modül akışını doğrular (master plan Bölüm 8.2).
/// Bu testler yönetici/yetki gerektirmeyen salt-okunur ve güvenli adımları çalıştırır.
/// </summary>
public class RealSystemScenarios
{
    private static SafetyNet MakeSafetyNet()
    {
        var baseDir = Path.Combine(Path.GetTempPath(), "wo-e2e-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(baseDir);
        return new SafetyNet(
            new RestorePointService(NullLogger<RestorePointService>.Instance),
            new ChangeJournal(baseDir, NullLogger<ChangeJournal>.Instance),
            new RegistryBackup(baseDir, NullLogger<RegistryBackup>.Instance),
            new SafetyGuard(NullLogger<SafetyGuard>.Instance),
            NullLogger<SafetyNet>.Instance);
    }

    /// <summary>Senaryo: CleanEngine gerçek TEMP klasörünü analiz eder (Bölüm 8.2).</summary>
    [Fact]
    public async Task CleanEngine_analyzes_real_temp_folder()
    {
        var safety = MakeSafetyNet();
        var engine = new CleanEngineModule(safety, NullLogger<CleanEngineModule>.Instance);

        var analysis = await engine.AnalyzeAsync();

        analysis.ModuleId.Should().Be("CleanEngine");
        analysis.Summary.Should().NotBeEmpty();
        // TEMP klasörü her zaman mevcut — en az bir hedef taranmalı
        analysis.Details.Should().ContainKey("Temp");
    }

    /// <summary>Senaryo: CleanEngine önizleme üretir (Preview — hiçbir şey silmez).</summary>
    [Fact]
    public async Task CleanEngine_preview_does_not_modify()
    {
        var safety = MakeSafetyNet();
        var engine = new CleanEngineModule(safety, NullLogger<CleanEngineModule>.Instance);
        var analysis = await engine.AnalyzeAsync();

        var preview = await engine.PreviewAsync(analysis);

        preview.IsDryRun.Should().BeTrue("önizleme hiçbir şey değiştirmez");
        preview.ModuleId.Should().Be("CleanEngine");
    }

    /// <summary>Senaryo: HardwareMonitor gerçek donanımı okur (Bölüm 8.2 — salt okunur).</summary>
    [Fact]
    public async Task HardwareMonitor_reads_real_cpu_ram()
    {
        var monitor = new HardwareMonitorModule(NullLogger<HardwareMonitorModule>.Instance);
        var analysis = await monitor.AnalyzeAsync();

        analysis.Details.Should().ContainKey("CpuName");
        analysis.Details.Should().ContainKey("RamUsedPct");
    }

    /// <summary>Senaryo: MemoryEngine boştaki süreçleri analiz eder.</summary>
    [Fact]
    public async Task MemoryEngine_analyzes_processes()
    {
        var safety = MakeSafetyNet();
        var engine = new MemoryEngineModule(safety, NullLogger<MemoryEngineModule>.Instance);
        var analysis = await engine.AnalyzeAsync();

        analysis.ModuleId.Should().Be("MemoryEngine");
        analysis.Summary.Should().NotBeEmpty();
    }

    /// <summary>Senaryo: Modüller ortak sözleşmeye uyumlu (Analyze→Preview).</summary>
    [Theory]
    [InlineData(typeof(CleanEngineModule))]
    [InlineData(typeof(HardwareMonitorModule))]
    public async Task All_modules_produce_valid_analysis(Type moduleType)
    {
        var safety = MakeSafetyNet();
        dynamic module = moduleType == typeof(CleanEngineModule)
            ? (object)new CleanEngineModule(safety, NullLogger<CleanEngineModule>.Instance)
            : new HardwareMonitorModule(NullLogger<HardwareMonitorModule>.Instance);

        var analysis = (WinOptimizer.Core.AnalysisResult)await module.AnalyzeAsync();
        var preview = (WinOptimizer.Core.PreviewResult)await module.PreviewAsync(analysis);

        analysis.ModuleId.Should().NotBeNullOrEmpty();
        preview.ModuleId.Should().Be(analysis.ModuleId);
    }
}
