namespace WinOptimizer.Modules.ProfileManager;

/// <summary>
/// Optimizasyon profili tanımı. Her profil, hangi modüllerin/tweak'lerin
/// etkin olduğunu belirtir (master plan Bölüm 6 profil matrisi).
/// </summary>
public sealed record OptimizerProfile(
    string Id,
    string Name,
    string Description,
    IReadOnlySet<string> EnabledModules,
    IReadOnlyDictionary<string, bool> EnabledTweaks)
{
    /// <summary>Dahili profiller (Oyun / İş / Pil / Dengeli).</summary>
    public static IReadOnlyList<OptimizerProfile> Defaults { get; } = new[]
    {
        new OptimizerProfile(
            "balanced", "Dengeli", "Güvenlik ve performans dengesi — önerilen varsayılan.",
            new HashSet<string> { "CleanEngine", "MemoryEngine", "SystemTweaker", "StorageOptimizer", "UpdateEngine" },
            new Dictionary<string, bool>
            {
                ["NtfsDisableLastAccess"] = true, ["MenuShowDelay"] = true, ["TelemetryAllow"] = true
            }),
        new OptimizerProfile(
            "gaming", "Oyun", "Maksimum performans (prizde önerilir).",
            new HashSet<string> { "CleanEngine", "MemoryEngine", "SystemTweaker", "StorageOptimizer", "CpuEngine" },
            new Dictionary<string, bool>
            {
                ["MenuShowDelay"] = true, ["TelemetryAllow"] = true, ["NtfsMemoryUsage"] = true
            }),
        new OptimizerProfile(
            "work", "İş", "Kararlılık ve güvenlik odaklı.",
            new HashSet<string> { "CleanEngine", "MemoryEngine", "RepairEngine", "UpdateEngine", "HardwareMonitor" },
            new Dictionary<string, bool> { ["TelemetryAllow"] = true }),
        new OptimizerProfile(
            "battery", "Pil", "Dizüstü pil tasarrufu — agresif güç tweak'leri kapalı.",
            new HashSet<string> { "CleanEngine", "MemoryEngine" },
            new Dictionary<string, bool> { ["SystemUsesTransparency"] = true })
    };
}

/// <summary>
/// ProfileManager — aktif profili seçer, saklar ve hangi modüllerin
/// etkin olacağını bildirir. (Master plan Bölüm 6.)
/// </summary>
public sealed class ProfileManager
{
    public OptimizerProfile Active { get; private set; } = OptimizerProfile.Defaults[0];

    public IReadOnlyList<OptimizerProfile> Available => OptimizerProfile.Defaults;

    public OptimizerProfile Select(string profileId)
    {
        var profile = OptimizerProfile.Defaults.FirstOrDefault(p => p.Id == profileId)
                      ?? throw new ArgumentException($"Bilinmeyen profil: {profileId}");
        Active = profile;
        return profile;
    }

    /// <summary>Verilen modül bu profilde etkin mi?</summary>
    public bool IsModuleEnabled(string moduleId) => Active.EnabledModules.Contains(moduleId);

    /// <summary>Verilen tweak bu profilde etkin mi?</summary>
    public bool IsTweakEnabled(string tweakId) =>
        Active.EnabledTweaks.TryGetValue(tweakId, out bool on) && on;
}
