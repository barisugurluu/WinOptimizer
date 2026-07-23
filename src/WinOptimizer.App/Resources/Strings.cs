namespace WinOptimizer.App.Resources;

/// <summary>
/// Strings.resx için elle yazılmış strongly-typed erişimci (master plan Bölüm 12.5).
/// ResourceManager ile kültüre göre TR/EN kaynak yükler.
/// Designer.cs üretilmesini beklemeden doğrudan çalışır.
/// </summary>
public static class Strings
{
    private static readonly System.Resources.ResourceManager _rm =
        new("WinOptimizer.App.Resources.Strings", typeof(Strings).Assembly);

    public static string AppTitle => _rm.GetString("AppTitle") ?? "Sistem Bakım & Optimizasyon";
    public static string Dashboard => _rm.GetString("Dashboard") ?? "Panosu";
    public static string SystemStatus => _rm.GetString("SystemStatus") ?? "Sistem Durumu";
    public static string OneClickOptimize => _rm.GetString("OneClickOptimize") ?? "🚀 Tek Tıkla En İyi Hale Getir";
    public static string Preview => _rm.GetString("Preview") ?? "🔍 Önizle";
    public static string Apply => _rm.GetString("Apply") ?? "🔁 Uygula";
    public static string QuickActions => _rm.GetString("QuickActions") ?? "Hızlı İşlemler";
    public static string Result => _rm.GetString("Result") ?? "Sonuç";
    public static string Rollback => _rm.GetString("Rollback") ?? "↩ Geri Al";
    public static string ConfirmTitle => _rm.GetString("ConfirmTitle") ?? "Onay";
    public static string ConfirmMessage => _rm.GetString("ConfirmMessage") ?? "Optimizasyon başlatılacak. Devam?";
    public static string Analyze => _rm.GetString("Analyze") ?? "Analiz";
    public static string Optimize => _rm.GetString("Optimize") ?? "Optimizasyon";
    public static string Ready => _rm.GetString("Ready") ?? "Hazır.";
    public static string NotAnalyzed => _rm.GetString("NotAnalyzed") ?? "Henüz analiz yapılmadı.";
    public static string Scanning => _rm.GetString("Scanning") ?? "Sistem taranıyor…";
    public static string ModulesActive => _rm.GetString("ModulesActive") ?? "modül aktif • Yönetici ayrıcalığıyla çalışıyor.";
    public static string InstalledModules => _rm.GetString("InstalledModules") ?? "Yüklü modüller:";
    public static string NoModules => _rm.GetString("NoModules") ?? "Henüz modül yok.";
    public static string AnalysisResult => _rm.GetString("AnalysisResult") ?? "dosya temizlenebilir. \"Uygula\" ile devam edin.";
    public static string AnalysisCanceled => _rm.GetString("AnalysisCanceled") ?? "Analiz iptal edildi.";
    public static string OptimizeCanceled => _rm.GetString("OptimizeCanceled") ?? "İşlem iptal edildi.";
    public static string PreparingRestorePoint => _rm.GetString("PreparingRestorePoint") ?? "Geri yükleme noktası alınıyor…";
    public static string CancelRequested => _rm.GetString("CancelRequested") ?? "İptal istendi.";

    /// <summary>Biçimlendirilebilir kaynak — {0}, {1} yer tutucularını değiştirir.</summary>
    public static string OptimizeComplete(long items, string gained) =>
        string.Format(_rm.GetString("OptimizeComplete") ?? "Tamamlandı: {0} öğe temizlendi, {1} kazanıldı.", items, gained);
    public static string Error(string message) =>
        string.Format(_rm.GetString("Error") ?? "Hata: {0}", message);
}
