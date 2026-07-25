using System.Globalization;
using System.Management;

namespace WinOptimizer.Modules.BenchmarkEngine;

/// <summary>
/// Benchmark ölçümü yapan yardımcı (WMI tabanlı). Master plan Bölüm 13.1 metrikleri.
/// </summary>
public sealed class BenchmarkMeasurer
{
    /// <summary>Yeni bir sistem ölçümü alır.</summary>
    public BenchmarkSnapshot Measure()
    {
        double? boot = MeasureBootTimeSec();
        long? freeRam = null;
        double? diskFree = null;
        int? cpuLoad = null;
        bool? rtp = null;

        try
        {
            using var os = new ManagementObjectSearcher("SELECT FreePhysicalMemory FROM Win32_OperatingSystem");
            var m = os.Get().Cast<ManagementObject>().FirstOrDefault();
            if (m is not null) freeRam = Convert.ToInt64(m["FreePhysicalMemory"]) / 1024; // KB→MB
        }
        catch { }

        try
        {
            using var ld = new ManagementObjectSearcher("SELECT FreeSpace FROM Win32_LogicalDisk WHERE DeviceID='C:'");
            var m = ld.Get().Cast<ManagementObject>().FirstOrDefault();
            if (m is not null) diskFree = Convert.ToDouble(m["FreeSpace"]) / 1e9;
        }
        catch { }

        try
        {
            using var cpu = new ManagementObjectSearcher("SELECT LoadPercentage FROM Win32_Processor");
            var m = cpu.Get().Cast<ManagementObject>().FirstOrDefault();
            if (m is not null) cpuLoad = Convert.ToInt32(m["LoadPercentage"]);
        }
        catch { }

        int secScore = ComputeSecurityScore(out rtp);

        return new BenchmarkSnapshot
        {
            BootSec = boot,
            FreeRamMb = freeRam,
            DiskFreeGb = diskFree,
            CpuLoadPct = cpuLoad,
            RealTimeProtection = rtp,
            SecurityScore = secScore
        };
    }

    /// <summary>İki snapshot arasındaki farkı hesaplar (son - önce).</summary>
    public static BenchmarkDelta Diff(BenchmarkSnapshot before, BenchmarkSnapshot after) => new(
        BootSec: (before.BootSec is double b && after.BootSec is double a) ? a - b : null,
        FreeRamMb: (before.FreeRamMb is long b1 && after.FreeRamMb is long a1) ? a1 - b1 : null,
        DiskFreeGb: (before.DiskFreeGb is double b2 && after.DiskFreeGb is double a2) ? a2 - b2 : null,
        SecurityScore: (before.SecurityScore is int b3 && after.SecurityScore is int a3) ? a3 - b3 : null);

    private static double? MeasureBootTimeSec()
    {
        try
        {
            using var os = new ManagementObjectSearcher("SELECT LastBootUpTime FROM Win32_OperatingSystem");
            var m = os.Get().Cast<ManagementObject>().FirstOrDefault();
            if (m is not null &&
                ManagementDateTimeConverter.ToDateTime(m["LastBootUpTime"].ToString()!) is DateTime boot)
            {
                return (DateTime.Now - boot).TotalSeconds;
            }
        }
        catch { }
        return null;
    }

    private static int ComputeSecurityScore(out bool? rtp)
    {
        int score = 0;
        rtp = null;
        try
        {
            using var def = new ManagementObjectSearcher(
                @"\\.\root\Microsoft\Windows\Defender",
                "SELECT RealTimeProtectionEnabled, AntivirusSignatureAge FROM MSFT_MpComputerStatus");
            var m = def.Get().Cast<ManagementObject>().FirstOrDefault();
            if (m is not null)
            {
                rtp = Convert.ToBoolean(m["RealTimeProtectionEnabled"]);
                if (rtp == true) score += 50;
                int age = Convert.ToInt32(m["AntivirusSignatureAge"]);
                if (age <= 1) score += 30; else if (age <= 7) score += 15;
            }
        }
        catch { }
        try
        {
            using var key = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(
                @"SYSTEM\CurrentControlSet\Control\DeviceGuard\Scenarios\HypervisorEnforcedCodeIntegrity");
            if (key?.GetValue("Enabled") is int v && v == 1) score += 20;
        }
        catch { }
        return Math.Clamp(score, 0, 100);
    }
}
