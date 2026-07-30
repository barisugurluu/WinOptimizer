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

    // --- Erişilebilirlik (a11y) kaynakları — master plan Bölüm 21.2 (WCAG 2.1 AA) ---
    // Duyuru metinleri (ekran okuyucu + UI etiketleri) ve AutomationProperties.HelpText değerleri.
    public static string AnalyzeAction => _rm.GetString("AnalyzeAction") ?? "🔍 Analiz Et";
    public static string CancelAction => _rm.GetString("CancelAction") ?? "İptal";
    public static string ActionsHeader => _rm.GetString("ActionsHeader") ?? "Uygulanacak Eylemler";
    public static string RollbackTimelineTitle => _rm.GetString("RollbackTimelineTitle") ?? "↩ Geri Alma Zaman Çizelgesi";
    public static string ProgressText => _rm.GetString("ProgressText") ?? "İlerleme";

    // Ekran okuyucu yardım metinleri (AutomationProperties.HelpText)
    public static string PreviewHelpText => _rm.GetString("PreviewHelpText") ?? "Sistemi analiz eder, değişiklik yapmaz.";
    public static string ApplyHelpText => _rm.GetString("ApplyHelpText") ?? "Önizlenen değişiklikleri uygular. Önce geri yükleme noktası alınır.";
    public static string AnalyzeHelpText => _rm.GetString("AnalyzeHelpText") ?? "Bu modülü analiz eder, değişiklik yapmaz.";
    public static string CancelHelpText => _rm.GetString("CancelHelpText") ?? "Çalışan işlemi iptal eder.";
    public static string ProgressHelpText => _rm.GetString("ProgressHelpText") ?? "İşlem ilerleme yüzdesi.";
    public static string RollbackListHelpText => _rm.GetString("RollbackListHelpText") ?? "Son yapılan değişiklikler.";
    public static string RollbackAction => _rm.GetString("RollbackAction") ?? "Geri Al";
    public static string RollbackActionHelpText => _rm.GetString("RollbackActionHelpText") ?? "Bu değişikliği geri alır.";
    public static string ActionsListHelpText => _rm.GetString("ActionsListHelpText") ?? "Uygulanacak eylemlerin listesi.";
    public static string RiskBadgeHelpText => _rm.GetString("RiskBadgeHelpText") ?? "Modül risk seviyesi.";
    public static string NavPaneHelpText => _rm.GetString("NavPaneHelpText") ?? "Modül navigasyon menüsü.";

    /// <summary>Biçimlendirilebilir kaynak — {0}, {1} yer tutucularını değiştirir.</summary>
    public static string OptimizeComplete(long items, string gained) =>
        string.Format(_rm.GetString("OptimizeComplete") ?? "Tamamlandı: {0} öğe temizlendi, {1} kazanıldı.", items, gained);
    public static string Error(string message) =>
        string.Format(_rm.GetString("Error") ?? "Hata: {0}", message);
    public static string DiagnosticsExported(string path) =>
        string.Format(_rm.GetString("DiagnosticsExported") ?? "✓ Teşhis paketi oluşturuldu: {0}", path);

    public static string DiagnosticsExport => _rm.GetString("DiagnosticsExport") ?? "Teşhis Paketini Dışa Aktar";
    public static string DiagnosticsExportHelpText => _rm.GetString("DiagnosticsExportHelpText")
        ?? "Günlükleri, değişiklik geçmişini ve sistem bilgisini bir .zip dosyasına toplar. Hiçbir yere gönderilmez.";

    // --- Yönetim Merkezi (Control Center) kaynakları — Bölüm 12 genişletmesi ---
    public static string Management => _rm.GetString("Management") ?? "Yönetim";
    public static string ManagementTitle => _rm.GetString("ManagementTitle") ?? "Yönetim Merkezi";
    public static string ManagementHelpText => _rm.GetString("ManagementHelpText") ?? "Yönetim merkezi — ayarlar, zamanlayıcı, modüller, guard ve daha fazlası.";

    public static string TabOverview => _rm.GetString("TabOverview") ?? "Genel Bakış";
    public static string TabSettings => _rm.GetString("TabSettings") ?? "Ayarlar";
    public static string TabScheduler => _rm.GetString("TabScheduler") ?? "Zamanlayıcı";
    public static string TabModules => _rm.GetString("TabModules") ?? "Modüller";
    public static string TabProfiles => _rm.GetString("TabProfiles") ?? "Profiller";
    public static string TabGuard => _rm.GetString("TabGuard") ?? "Guard & Uyarılar";
    public static string TabReports => _rm.GetString("TabReports") ?? "Raporlar";
    public static string TabUpdate => _rm.GetString("TabUpdate") ?? "Güncelleme";
    public static string TabData => _rm.GetString("TabData") ?? "Veri & Geri Yükleme";
    public static string TabComingSoon => _rm.GetString("TabComingSoon") ?? "Bu sekme yakında gelecek.";

    public static string OverviewServiceStatus => _rm.GetString("OverviewServiceStatus") ?? "Servis Durumu";
    public static string OverviewLiveMetrics => _rm.GetString("OverviewLiveMetrics") ?? "Canlı Metrikler";
    public static string OverviewCpu => _rm.GetString("OverviewCpu") ?? "İşlemci (CPU)";
    public static string OverviewRam => _rm.GetString("OverviewRam") ?? "Bellek (RAM)";
    public static string OverviewDisk => _rm.GetString("OverviewDisk") ?? "Disk (C:)";
    public static string OverviewRecentActivity => _rm.GetString("OverviewRecentActivity") ?? "Son Etkinlik";
    public static string OverviewNoActivity => _rm.GetString("OverviewNoActivity") ?? "Henüz etkinlik yok.";
    public static string OverviewRefresh => _rm.GetString("OverviewRefresh") ?? "Yenile";
    public static string ServiceRunning => _rm.GetString("ServiceRunning") ?? "RealtimeGuard servisi çalışıyor";
    public static string ServiceStoppedUsingWmi => _rm.GetString("ServiceStoppedUsingWmi") ?? "Servis çalışmıyor (yerel WMI ölçümü kullanılıyor)";
    public static string ServiceNotInstalled => _rm.GetString("ServiceNotInstalled") ?? "Servis kurulu değil (yerel WMI ölçümü kullanılıyor)";
    public static string ServiceGoToGuardTab => _rm.GetString("ServiceGoToGuardTab") ?? "Guard sekmesinden kurabilir/başlatabilirsiniz.";

    // --- Guard sekmesi (servis yönetimi) ---
    public static string GuardTitle => _rm.GetString("GuardTitle") ?? "RealtimeGuard Hizmeti";
    public static string GuardDescription => _rm.GetString("GuardDescription") ?? "Arka planda çalışan, eşik aşımlarını izleyen Windows hizmeti. İsteğe bağlıdır; kurulmadan da uygulamanın tüm işlevleri kullanılabilir (canlı metrikler yerel WMI ile okunur).";
    public static string GuardStateLabel => _rm.GetString("GuardStateLabel") ?? "Durum";
    public static string GuardStateNotInstalled => _rm.GetString("GuardStateNotInstalled") ?? "Kurulu değil";
    public static string GuardStateStopped => _rm.GetString("GuardStateStopped") ?? "Durduruldu";
    public static string GuardStateStartPending => _rm.GetString("GuardStateStartPending") ?? "Başlatılıyor…";
    public static string GuardStateStopPending => _rm.GetString("GuardStateStopPending") ?? "Durduruluyor…";
    public static string GuardStateRunning => _rm.GetString("GuardStateRunning") ?? "Çalışıyor";
    public static string GuardStateUnknown => _rm.GetString("GuardStateUnknown") ?? "Durum okunamadı";
    public static string GuardInstall => _rm.GetString("GuardInstall") ?? "Kur";
    public static string GuardStart => _rm.GetString("GuardStart") ?? "Başlat";
    public static string GuardStop => _rm.GetString("GuardStop") ?? "Durdur";
    public static string GuardUninstall => _rm.GetString("GuardUninstall") ?? "Kaldır";
    public static string GuardRepair => _rm.GetString("GuardRepair") ?? "Onar";
    public static string GuardExeMissing => _rm.GetString("GuardExeMissing") ?? "Servis dosyası bulunamadı — kurulum eksik veya bozuk olabilir:";
    public static string GuardOpFailed => _rm.GetString("GuardOpFailed") ?? "İşlem başarısız. Ayrıntı için günlüklere bakın (logs\\service-install.log).";
    public static string GuardOpSucceeded => _rm.GetString("GuardOpSucceeded") ?? "✓ İşlem tamamlandı.";
    public static string SchedulerCliMissing => _rm.GetString("SchedulerCliMissing") ?? "Görev oluşturulamaz — WinOptimizer.Cli.exe bulunamadı:";
    public static string GuardAutoTitle => _rm.GetString("GuardAutoTitle") ?? "İzleme ve Otomatik Müdahale";
    public static string GuardAutoDescription => _rm.GetString("GuardAutoDescription") ?? "Bu ayarlar hizmet tarafından 5 saniye içinde okunur; yeniden başlatma gerekmez. Otomatik müdahale varsayılan olarak KAPALIDIR: hizmet SYSTEM yetkisiyle çalışır ve size sormadan dosya silmemelidir.";
    public static string GuardAutoRemediate => _rm.GetString("GuardAutoRemediate") ?? "Otomatik müdahaleye izin ver";
    public static string GuardAutoTrimRam => _rm.GetString("GuardAutoTrimRam") ?? "RAM eşiği aşılınca boştaki süreçlerin belleğini boşalt";
    public static string GuardAutoCleanDisk => _rm.GetString("GuardAutoCleanDisk") ?? "Disk kritik seviyeye inince geçici dosyaları sil (geri alınamaz)";
    public static string GuardAutoDefender => _rm.GetString("GuardAutoDefender") ?? "Defender imzaları eskiyince güncelle (hiçbir şey silmez)";
    public static string GuardSettingsSaved => _rm.GetString("GuardSettingsSaved") ?? "✓ Kaydedildi. Hizmet 5 saniye içinde uygulayacak.";
    public static string GuardAlerts => _rm.GetString("GuardAlerts") ?? "Son Uyarılar";
    public static string GuardNoAlerts => _rm.GetString("GuardNoAlerts") ?? "Uyarı yok.";

    // --- Sistem & Veri sekmesi (gereksinim kontrolü + teşhis) ---
    public static string RequirementsTitle => _rm.GetString("RequirementsTitle") ?? "Sistem Gereksinim Kontrolü";
    public static string RequirementsDescription => _rm.GetString("RequirementsDescription") ?? "Uygulamanın düzgün çalışması için gereken koşullar. Bir sorun bildirirken bu listeyi ve teşhis paketini paylaşmak, sorunu doğrudan gösterir.";
    public static string RequirementsRecheck => _rm.GetString("RequirementsRecheck") ?? "Yeniden denetle";
    public static string DiagnosticsPackage => _rm.GetString("DiagnosticsPackage") ?? "Teşhis paketi";
    public static string OverviewLiveMetricsDisabled => _rm.GetString("OverviewLiveMetricsDisabled") ?? "Canlı metrikler ayarlardan kapatıldı.";
    public static string ModulesTitle => _rm.GetString("ModulesTitle") ?? "Tek Tıkla Kapsamı";
    public static string ModulesDescription => _rm.GetString("ModulesDescription") ?? "\"Tek Tıkla En İyi Hale Getir\" yalnızca burada işaretli modülleri çalıştırır. Varsayılan liste bilinçli olarak dardır: uzun süren (SFC/DISM), yeniden başlatma isteyen (Hyper-V, winsock) veya kullanıcının seçmesi gereken (kayıt defteri, uygulama kaldırma) işlemler dışarıda bırakılmıştır. İşaretlenmemiş modüller kendi sayfalarından elle çalıştırılabilir.";
    public static string ModulesResetSafe => _rm.GetString("ModulesResetSafe") ?? "Güvenli varsayılana dön";
    public static string ModulesSelectAll => _rm.GetString("ModulesSelectAll") ?? "Tümünü seç";
    public static string ModulesResetHint => _rm.GetString("ModulesResetHint") ?? "Güvenli varsayılan seçildi. Kalıcı olması için Kaydet'e basın.";
    public static string ModulesSelectAllHint => _rm.GetString("ModulesSelectAllHint") ?? "Tüm modüller seçildi. Riskli eylemler yine ayrıca onay isteyecek. Kalıcı olması için Kaydet'e basın.";
    public static string SettingsSaveFailed => _rm.GetString("SettingsSaveFailed") ?? "✕ Ayarlar KAYDEDİLEMEDİ. Günlüklere bakın (logs\\app-*.log).";
    public static string SettingsSavedRestartRequired => _rm.GetString("SettingsSavedRestartRequired") ?? "✓ Ayarlar kaydedildi. Dil değişikliği uygulama yeniden başlatıldığında etkin olur.";

    public static string SettingsGeneral => _rm.GetString("SettingsGeneral") ?? "Genel";
    public static string SettingsLanguage => _rm.GetString("SettingsLanguage") ?? "Dil";
    public static string SettingsTheme => _rm.GetString("SettingsTheme") ?? "Tema";
    public static string SettingsSafetyNet => _rm.GetString("SettingsSafetyNet") ?? "Güvenlik Ağı";
    public static string SettingsAutoRestorePoint => _rm.GetString("SettingsAutoRestorePoint") ?? "İşlem öncesi otomatik geri yükleme noktası al";
    public static string SettingsAutoRegistryBackup => _rm.GetString("SettingsAutoRegistryBackup") ?? "Kayıt defteri değişikliğinde otomatik yedek al";
    public static string SettingsRequireConfirm => _rm.GetString("SettingsRequireConfirm") ?? "Yüksek riskli işlemler için ek onay iste";
    public static string SettingsGuardSection => _rm.GetString("SettingsGuardSection") ?? "RealtimeGuard (Gerçek Zamanlı Koruma)";
    public static string SettingsGuardEnabled => _rm.GetString("SettingsGuardEnabled") ?? "Gerçek zamanlı koruma açık";
    public static string SettingsThresholds => _rm.GetString("SettingsThresholds") ?? "Müdahale Eşikleri";
    public static string SettingsRamThreshold => _rm.GetString("SettingsRamThreshold") ?? "RAM kullanım eşiği (%)";
    public static string SettingsDiskThreshold => _rm.GetString("SettingsDiskThreshold") ?? "Disk boş alan uyarı eşiği (%)";
    public static string SettingsDiskCritical => _rm.GetString("SettingsDiskCritical") ?? "Disk boş alan kritik eşiği (%)";
    public static string SettingsCpuThreshold => _rm.GetString("SettingsCpuThreshold") ?? "Süreç başına CPU eşiği (%)";
    public static string SettingsTempThreshold => _rm.GetString("SettingsTempThreshold") ?? "CPU sıcaklık eşiği (°C)";
    public static string SettingsSave => _rm.GetString("SettingsSave") ?? "Kaydet";
    public static string SettingsResetAction => _rm.GetString("SettingsResetAction") ?? "Sıfırla";
    public static string SettingsSaved => _rm.GetString("SettingsSaved") ?? "✓ Ayarlar kaydedildi.";
    public static string SettingsReverted => _rm.GetString("SettingsReverted") ?? "Ayarlar kayıtlı değerlere geri alındı.";

    public static string SchedulerWeekly => _rm.GetString("SchedulerWeekly") ?? "Haftalık Otomatik Bakım";
    public static string SchedulerWeeklyDesc => _rm.GetString("SchedulerWeeklyDesc") ?? "Her hafta belirlenen günde/saatte otomatik optimizasyon çalıştırılır (arka planda, en yüksek ayrıcalıkla).";
    public static string SchedulerEnable => _rm.GetString("SchedulerEnable") ?? "Etkin";
    public static string SchedulerDay => _rm.GetString("SchedulerDay") ?? "Gün";
    public static string SchedulerTime => _rm.GetString("SchedulerTime") ?? "Saat";
    public static string SchedulerCliPath => _rm.GetString("SchedulerCliPath") ?? "CLI yolu";
    public static string SchedulerTaskStatus => _rm.GetString("SchedulerTaskStatus") ?? "Görev durumu";
    public static string SchedulerTaskExists => _rm.GetString("SchedulerTaskExists") ?? "✓ Zamanlanmış görev kurulu";
    public static string SchedulerTaskMissing => _rm.GetString("SchedulerTaskMissing") ?? "Zamanlanmış görev kurulu değil";
    public static string SchedulerCreate => _rm.GetString("SchedulerCreate") ?? "Görevi Oluştur";
    public static string SchedulerDelete => _rm.GetString("SchedulerDelete") ?? "Görevi Sil";
    public static string TaskCreated => _rm.GetString("TaskCreated") ?? "✓ Haftalık görev oluşturuldu.";
    public static string TaskCreateFailed => _rm.GetString("TaskCreateFailed") ?? "⚠ Görev oluşturulamadı (yönetici ayrıcalığı gerekli olabilir).";
    public static string TaskDeleted => _rm.GetString("TaskDeleted") ?? "✓ Görev silindi.";
    public static string TaskDeleteFailed => _rm.GetString("TaskDeleteFailed") ?? "⚠ Görev silinemedi.";

    // --- Modul gorunen adlari (i18n) — master plan Bolum 12.5
    // Modul kimligine gore resx anahtari (Module_{Id}) arar; yoksa null doner
    // (cagiran ModuleDisplayNameResolver, modulun TR varsayilanina geri doner).
    public static string? GetModuleDisplayName(string moduleId) => _rm.GetString("Module_" + moduleId);
}
