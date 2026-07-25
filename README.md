# WinOptimizer

**Windows Sistem Bakım & Optimizasyon Yazılımı** — tek tıkla Windows'u "en iyi" haline getiren araç.

C# 12 / .NET 8 WPF · Fluent Dark (WPF-UI) · Katmanlı & modüler mimari.

> Bu iskelet, `maintenance_master_plan.md` (v4.0) belgesinin **Faz 0 (Temel)** ve **Faz 1 (CleanEngine)** çıkışını uygular.

---

## Durum (Bu teslimat)

| Bileşen | Durum |
|--------|-------|
| Çözüm iskeleti + `app.manifest` (requireAdministrator) | ✅ Derlendi |
| `WinOptimizer.Core` — `IOptimizationModule`, modeller, enum'lar | ✅ Derlendi |
| `WinOptimizer.Safety` — ChangeJournal, RestorePoint, SafetyGuard, RegistryBackup, ProcessRunner, SafetyNet | ✅ Derlendi |
| `WinOptimizer.Native` — P/Invoke (psapi, kernel32, shell32) + EmptyWorkingSet | ✅ Derlendi |
| `WinOptimizer.Modules.CleanEngine` — TEMP/Prefetch/Log/WER/Delivery Optimization + tarayıcı cache | ✅ Derlendi |
| `WinOptimizer.Modules.MemoryEngine` — EmptyWorkingSet ile RAM boşaltma | ✅ Derlendi |
| `WinOptimizer.Modules.CpuEngine` — otomatik servisler, yüksek CPU tespiti | ✅ Derlendi |
| `WinOptimizer.Modules.RepairEngine` — SFC/DISM/chkdsk | ✅ Derlendi |
| `WinOptimizer.Modules.SystemTweaker` — registry tweak kataloğu (uygula/geri al) | ✅ Derlendi |
| `WinOptimizer.Modules.HardwareMonitor` — CPU/RAM/disk SMART (salt okunur) | ✅ Derlendi |
| `WinOptimizer.Modules.StorageOptimizer` — TRIM/defrag (disk türü algılama) | ✅ Derlendi |
| `WinOptimizer.Modules.ProfileManager` — Oyun/İş/Pil/Dengeli profilleri | ✅ Derlendi |
| `WinOptimizer.Modules.PrivacyGuard` — telemetri/reklam ID kapatma | ✅ Derlendi |
| `WinOptimizer.Modules.NetworkOptimizer` — DNS/TCP/winsock | ✅ Derlendi |
| `WinOptimizer.Modules.BootOptimizer` — Fast Startup/önyükleme | ✅ Derlendi |
| `WinOptimizer.Modules.AppManager` — UWP/bloatware kaldırma | ✅ Derlendi |
| `WinOptimizer.Modules.UpdateEngine` — WU sıfırlama/usoclient | ✅ Derlendi |
| `WinOptimizer.Modules.SecurityHardening` — Defender/ASR/PUA/HVCI (Defender ASLA kapatılmaz) | ✅ Derlendi |
| `WinOptimizer.Modules.BackupRestore` — wbadmin BMR, sistem durumu, vssadmin gölge kopya | ✅ Derlendi |
| `WinOptimizer.Modules.GpuOptimizer` — GPU tespit, HAGS, VRR | ✅ Derlendi |
| `WinOptimizer.Modules.DevEnvironment` — Hyper-V, WSL2, Geliştirici Modu, uzun yol | ✅ Derlendi |
| `WinOptimizer.Modules.DeepCleanEngine` — Windows.old, hibernation, büyük dosyalar | ✅ Derlendi |
| `WinOptimizer.Modules.BenchmarkEngine` — önce/sonra performans ölçümü + rapor | ✅ Derlendi |
| `WinOptimizer.Updater` — Uygulama oto-güncelleme (GitHub Release + SHA256/WinTrust + msiexec) | ✅ Derlendi |
| `WinOptimizer.Service` — RealtimeGuard Windows servisi (MetricsCollector, ThresholdEngine, Named Pipe IPC) | ✅ Derlendi |
| `WinOptimizer.Cli` — Komut satırı (analyze/optimize/clean/status, --json, --yes) | ✅ Çalıştı |
| `WinOptimizer.Orchestration` — ModuleRegistry + JobOrchestrationEngine + SettingsService + SchedulerService | ✅ Derlendi |
| `WinOptimizer.App` — WPF Fluent Dark Dashboard, 11 modül sayfası, Geri Al çizelgesi, i18n (`.resx` TR/EN, `x:Static` bağlı) | ✅ Derlendi |
| Birim testleri (SafetyGuard, ChangeJournal, RegistryTweak, BenchmarkEngine) — 22 test | ✅ 22/22 geçti |
| **E2E testleri** (`WinOptimizer.E2E.Tests` — gerçek sistem senaryoları) — 6 test | ✅ 6/6 geçti |
| JSON şemaları (settings + example + tweaks catalog) | ⚠ Dosyalar var, koda bağlı değil |
| **i18n** — `.resx` kaynak dosyaları (TR varsayılan + EN), `x:Static` + ViewModel bağlama (Bölüm 12.5) | ✅ Bağlı |
| **JSON tweak kataloğu** — `tweaks.catalog.json` (Bölüm 16.5) | ⚠ Henüz `TweakCatalog.cs` tarafından okunmuyor |
| **CI/CD** — GitHub Actions (build + test Windows runner) (Bölüm 8.6) | ✅ |
| **Canlı doğrulama** — CLI `analyze` 37.774 öğe / 21,67 GB tespit etti | ✅ |
| `SettingsService` + `SchedulerService` — JSON ayar kalıcılığı + Task Scheduler (Faz 8) | ✅ Derlendi |
| Inno Setup kurulum betiği (App + Service + CLI, servis kaydı) (Faz 9) | ✅ |
| `.gitignore` + `settings.example.json` | ✅ |

**Master planın tamamı (Faz 0–9 + üretim sertleştirme + tüm boşluklar kapatıldı) uygulandı.** 28 proje, 75 kaynak dosyası, 0 uyarı/hata, **28/28 test geçti** (22 birim + 6 E2E).

### Kapatılan UI/Test/Entegrasyon Boşlukları
- ✅ **UI navigasyon view'ları** — 11 modül sayfası (Temizlik/Bellek/Onarım/İnce Ayar/Donanım/Disk/Gizlilik/Güvenlik/Ağ/Güncelleme) + Panosu + Geri Al (Bölüm 12.2)
- ✅ **Geri Alma zaman çizelgesi** — change journal'dan 7 günlük kart listesi (Bölüm 12.3 Akış C)
- ✅ **i18n gerçek bağlama** — `.resx` → `x:Static` (XAML) + `Strings.cs` (ViewModel), TR/EN tam yerelleştirme
- ✅ **E2E testleri** — `WinOptimizer.E2E.Tests`: gerçek TEMP analizi, donanım okuma, süreç tarama (Bölüm 8.2)
- ⚠ **JSON tweak kataloğu** — `schemas/tweaks.catalog.json` dosyası mevcut, ancak `SystemTweaker/TweakCatalog.cs` şu an kataloğu koda gömülü tutuyor; "tek kaynak" bağlantısı henüz kurulmadı (bkz. geliştirme planı Faz C)

### Kapatılan Fonksiyonel Boşluklar (şartname uygunluğu)
- ✅ **Geri Dönüşüm kutusu** — `Shell32.EmptyRecycleBin` artık CleanEngine'de gerçekten çağrılıyor (Bölüm 11.5)
- ✅ **RealtimeGuard otomatik müdahale** — `RemediationEngine`: RAM>85%→EmptyWorkingSet, disk<5%→TEMP+Geri Dönüşüm, imza>7gün→Update-MpSignature (10dk cooldown ile) (Bölüm 3.17)
- ✅ **Firefox tarayıcı temizliği** — `profiles.ini` çözümleme + `cache2`/`startupCache` (Bölüm 3.1)
- ✅ **PowerPlanManager** — `powercfg -duplicatescheme` ile Ultimate Performance planı (Bölüm 3.9.A)
- ✅ **Yinelenen dosya bulucu** — MD5 hash-tabanlı yinelenen tespiti (Bölüm 3.2)

---

## Ön Koşullar

- **.NET 8 SDK** (`dotnet --version` çalışmalı). İndir: <https://dotnet.microsoft.com/download>
- Windows 10/11 (64-bit). WMI, yönetici ayrıcalığı gereklidir.

## Derleme & Çalıştırma

```powershell
# Çözümü derle (NuGet paketlerini otomatik indirir)
dotnet build WinOptimizer.sln -c Release

# Uygulamayı çalıştır (yönetici olarak — app.manifest isteyecek)
dotnet run --project src\WinOptimizer.App\WinOptimizer.App.csproj
```

## Testleri Çalıştırma

```powershell
dotnet test WinOptimizer.sln
```

> ✅ **Doğrulandı:** Çözüm sıfır uyarı/hata ile derleniyor (30 proje). Test kapsamı: 22 birim testi
> (SafetyGuard, ChangeJournal, RegistryTweak, BenchmarkEngine) + 6 E2E + 14 Updater testi.
> ⚠ **Kapsam boşluğu:** `src/WinOptimizer.Modules/` altındaki 19 modülün hiçbirinin kendi birim testi
> yok; E2E testleri yalnızca CleanEngine/MemoryEngine/HardwareMonitor'a yüzeysel dokunuyor ve
> `WinOptimizer.Service` (RealtimeGuard) test edilmiyor. Bkz. geliştirme planı Faz B.

---

## Mimari (master plan Bölüm 2)

```
UI (WPF)  →  Orchestration (JobEngine)  →  Modüller (IOptimizationModule)  →  Altyapı (Win32/WMI/Registry)
                    ↓
              SafetyNet (Restore Point + Change Journal + Registry Backup + SafetyGuard)
```

Her modül ortak sözleşmeyi uygular:
`AnalyzeAsync` → `PreviewAsync` → `ExecuteAsync` → `RollbackAsync`

### Güvenlik ilkeleri (master plan Bölüm 1.2)
1. **Yıkıcı değil, onarıcıdır** — silmek yerine önce onarır.
2. **Geri alınabilirdir** — her değişikliğin tersi `journal/*.jsonl` içinde saklanır.
3. **Şeffaftır** — kullanıcı her veriyi, her komutu görür (önizleme).
4. **Güvenli varsayılanlar** — riskli tweak'ler varsayılan KAPALI.
5. **Kritik servislere dokunulmaz** — `WinDefend`, `RpcSs`, `EventLog`… beyaz listede.

---

## Klasör Yapısı

```
WinOptimizer/
├── WinOptimizer.sln
├── Directory.Build.props          # Ortak derleme ayarları (deterministic, warnings as errors)
├── schemas/settings.schema.json   # Bölüm 16.1 ayar şeması
├── src/
│   ├── WinOptimizer.Core/         # IOptimizationModule, modeller, enum'lar
│   ├── WinOptimizer.Native/       # P/Invoke (psapi, kernel32, shell32)
│   ├── WinOptimizer.Safety/       # SafetyNet, ChangeJournal, RestorePoint…
│   ├── WinOptimizer.Orchestration/# ModuleRegistry, JobOrchestrationEngine
│   ├── WinOptimizer.App/          # WPF giriş noktası (Fluent Dark, Mica)
│   └── WinOptimizer.Modules/
│       └── CleanEngine/           # Disk & önbellek temizliği
└── tests/
    └── WinOptimizer.Core.Tests/   # SafetyGuard + ChangeJournal testleri
```

## Sonraki Adımlar (üretim sertleştirme)

Üretim dağıtım hat altyapısı kuruldu (M9):
- ✅ **Kod imzalama** — `src/WinOptimizer.Native/WinTrust.cs` (WinVerifyTrust P/Invoke) + `build/sign-release.ps1` (signtool EV/PFX) — *EV sertifikası ile etkinleştir*
- ✅ **WiX v4 MSI** — `installer/wix/` (per-machine, Service LocalSystem, CLI tek kurulum) — *0 uyarı/hata ile derlendi, 4.77 MB*
- ✅ **Winget manifest** — `installer/winget/*.yaml` × 3 — *SHA256 doğrulandı*
- ✅ **Deterministic + SourceLink** — `Directory.Build.props` (snupkg, GitHub kaynak bağlantısı)
- ✅ **CI/CD** — `.github/workflows/build.yml` (publish + Payload + MSI + imza + Release + Winget PR)

Kalan adımlar (üretim sertleştirme):
- ✅ **a11y** — AutomationProperties/HelpText, HeadingLevel, FocusVisualStyle, HighContrast→Mica, i18n tam (Bölüm 21.2)
- ✅ **Otomatik güncelleme** — `WinOptimizer.Updater` + CLI `update` komutu (Bölüm 20.6)
- ✅ **Performans benchmark** — CLI `benchmark` komutu: before→optimize→after + diff raporu (Bölüm 13)
- ~ **Serilog** — App katmanında kurulu (`Infrastructure/LoggingBootstrap.cs`, günlük döngülü dosya
  sink'i, `AddSerilog()` ile `ILogger<T>`'a köprülenmiş). ⚠ Modüllerin içinde henüz kullanılmıyor
  (DoD'nin "yapılandırılmış günlük olayları" maddesi modüller için karşılanmadı) — bkz. plan Faz C
- **Ertelendi** — arm64 MSI üretimi (test edilecek arm64 donanım yok; winget manifestinden sahte
  SHA256'lı arm64 girişi kaldırıldı), EV kod imzalama sertifikası (imzalama hattı kod olarak hazır)

### Paketleme Hattı
```powershell
# Tam hat: derle + test + publish + imzala + WiX MSI
.\build\build-installer.ps1
# Sadece geliştirme: imzasız
.\build\build-installer.ps1 -SkipSign
```
