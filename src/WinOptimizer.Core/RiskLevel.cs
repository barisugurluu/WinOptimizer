namespace WinOptimizer.Core;

/// <summary>
/// Bir optimizasyon adımının risk düzeyi. Güvenlik kurallarına göre
/// (bkz. master plan Bölüm 1.2) riskli tweak'ler varsayılan KAPALI'dır.
/// </summary>
public enum RiskLevel
{
    /// <summary>Salt okunur / zararsız (ör. donanım izleme).</summary>
    None = 0,

    /// <summary>Güvenli, geri alınabilir (ör. TEMP temizliği, DNS flush).</summary>
    Low = 1,

    /// <summary>Etkisi var ama kontrollü (ör. TCP tweak, winsock reset).</summary>
    Medium = 2,

    /// <summary>Sistem davranışını değiştirir — ayrı onay ister (ör. 8.3, pagefile, HVCI).</summary>
    High = 3
}
