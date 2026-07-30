# CLAUDE.md — WinOptimizer Proje Belleği

> Bu dosya, Claude Code'un projeye her girişte okuduğu **proje bağlamı ve talimat dosyasıdır**.
> `maintenance_master_plan.md` (1822 satır) ve `PLAN.md` (devam noktası) ile birlikte kullanılır.

---

## 0. Bu Proje Nedir?

**WinOptimizer** — Windows'u "tek tıkla en iyi haline getiren" sistem bakım & optimizasyon
yazılımı. C# 12 / .NET 8 WPF + Fluent Dark (WPF-UI). Katmanlı & modüler mimari. 19 motor
modülü, gerçek zamanlı koruma servisi, CLI, WPF arayüzü. **Durum: üretim sertleştirme
tamamlandı (Faz A–I), 174+ test yeşil.**

## 1. Hızlı Komutlar

> .NET 8 SDK `~/.dotnet`'te (8.0.423). PATH'e eklendi; **yeni terminalde `dotnet` doğrudan çalışır.**
> Mevcut shell eskidirse: `& "$env:USERPROFILE\.dotnet\dotnet.exe" <cmd>`.

```powershell
dotnet build WinOptimizer.sln -c Release          # 0 hata hedefi (TreatWarningsAsErrors)
dotnet test  WinOptimizer.sln -c Release --no-build
dotnet test  WinOptimizer.sln -c Release --no-build --collect:"XPlat Code Coverage"  # kapsam
dotnet format WinOptimizer.sln --verify-no-changes # format kapısı (CI'da zorunlu)
dotnet format WinOptimizer.sln                      # uygula (CRLF/FINALNEWLINE vb.)
```

**Tam paketleme (TEK hat → setup.exe):**
```powershell
.\build\build-installer.ps1              # derle+test+SC publish+ISCC+SHA256 → installer\build\*-setup.exe
.\build\build-installer.ps1 -SkipTests   # test adımını atla (hızlı yineleme)
.\build\generate-icon.ps1                # WinOptimizer.ico'yu yeniden üret
```
> MSI/WiX hattı **kaldırıldı** (`installer/wix/`, `generate-payload.ps1` silindi).
> Dağıtım **imzasız**dır: `sign-release.ps1` diskte ama hiçbir yerden çağrılmaz —
> kendinden imzalı sertifika ASLA kullanılmaz. Gerekçeler: `docs/KURULUM.md`.
> Sürümün tek kaynağı `Directory.Build.props` → `VersionPrefix`/`VersionSuffix`;
> `.iss` sürümü `/D` ile alır. Inno Setup gerekir (`winget install JRSoftware.InnoSetup`).
> `build/*.ps1` dosyaları **UTF-8 BOM'lu** olmalı — PS 5.1 BOM'suz dosyayı ANSI okur ve
> Türkçe/em-dash karakterleri ayrıştırma hatasına yol açar.

## 2. Mimari (katmanlı — bağımlılık yukarıdan aşağıya)

```
UI (WPF App)  →  Orchestration (JobEngine/Registry/Scheduler/Rollback)  →
  Modüller (IOptimizationModule × 19)  →  Safety (SafetyNet)  →  Native (P/Invoke)
```

**Kritik katmanlama kuralı:** `WinOptimizer.Modules` ve `WinOptimizer.Safety` **ASLA**
`WinOptimizer.App`'e bağımlı olamaz (döngüsel bağımlılık). Bu yüzden i18n `App`'te
`ModuleDisplayNameResolver` ile çözülür — modüller saf kalır.

### Modül sözleşmesi (her modül uygular)
`AnalyzeAsync` (tara, değişiklik yok) → `PreviewAsync` (dry-run plan) →
`ExecuteAsync` (uygula + journal) → `RollbackAsync` (ters işlem).
Sözleşme simetrisi `ModuleContractTests` ile 7 modülde doğrulanır.

## 3. Güvenlik İlkeleri (DEĞİŞTİRMEYE ÖZEN GÖSTER)

1. **Yıkıcı değil, onarıcıdır** — silmek yerine önce onarır.
2. **Geri alınabilirdir** — her değişiklik `journal/*.jsonl`'e yazılır, HMAC imzalı (§17.4).
3. **Şeffaftır** — kullanıcı her veriyi/komutu görür (önizleme).
4. **Güvenli varsayılanlar** — riskli tweak'ler varsayılan KAPALI; Medium/High riskli eylemler
   ek onay ister (`RequiresExtraConfirmation`).
5. **Kritik servislere dokunulmaz** — `WinDefend`, `RpcSs`, `EventLog`... `SafetyGuard` beyaz
   listesinde. **Defender ASLA kapatılmaz.**

### Güvenlik altyapısı (Faz E — §17)
- **HMAC bütünlük:** `IntegrityGuard` (HMAC-SHA256) → journal + `.reg` yedekleri `.hmac` imzalı.
  Anahtar `IntegrityKeyStore` ile DPAPI korumalı. Kurcalama → geri alma durdurulur.
- **Komut enjeksiyonu kapalı:** `ProcessRunner` güvenli `ArgumentList` imzası kullanır.
  Kullanıcı/sistem verisi ASLA string-interpolation ile komuta gömülmez (kabuklar
  cmd/powershell tek argüman korur).
- **DPAPI:** `SecretProtector` (CurrentUser scope) opsiyonel gizli değerler için.
- **Dayanıklılık:** `Resilience` (Polly retry+timeout) — WMI/process çağrılarında geçici hata.

## 4. Kodlama Kuralları (derlemeyi kırmamak için ZORUNLU)

- **`TreatWarningsAsErrors=true`** + **`AnalysisLevel=latest-recommended`** + **Roslynator**.
  Derleme uyarısı = hata. Sıfır uyarı hedefi.
- **Satır sonları CRLF** (`.editorconfig`: `end_of_line = crlf`). Düzenlenen dosyaları CRLF
  tut; LF olursa `dotnet format` hata verir. Düz metin düzenlemede PowerShell `Replace` + `\r\n`.
- **`Nullable enable`**, **`ImplicitUsings enable`**, C# 12.
- **async void yok**; `.Result`/`.Wait()` yok; `await` + `CancellationToken` tutarlı.
- **Empty catch yok** — her catch `LogDebug(ex, ...)` ile günlükler veya rethrow.

### CA1305 (kültür duyarlı biçimlendirme)
Sayı biçimlendirme için **`FileSizeFormatter`** (Core, InvariantCulture) kullan. CA1305
`.editorconfig`'te `suggestion` — dokunulan yerlerde `InvariantCulture` ile düzelt.

## 5. Test Yazma

- **xUnit + FluentAssertions + Moq.** Ad kuralı: `_method_scenario_expected-result`.
- Modül testleri `WinOptimizer.Modules.Tests/Factories` ile I/O'suz kurulur.
- Yeni güvenlik/safety testleri `WinOptimizer.Core.Tests/` (Safety'e `InternalsVisibleTo` var).
- Kapsam hedefleri (§18.3): Core/Safety ≥%85, modüller ≥%70. coverlet CI'da toplar.

## 6. Do / Don't

✅ **DO:** CRLF kullan · `dotnet format` çalıştırmadan commit etme · modül sözleşmesini koru ·
   güvenli varsayılanları koru · HMAC imzalamayı yeni Safety çıktılarına uygula ·
   `FileSizeFormatter` ile sayı biçimlendir · her değişiklikte `PLAN.md` güncelle.

❌ **DON'T:** Modülleri App'e bağımlı yapma · `ProcessRunner`'a string argüman gömme ·
   `TreatWarningsAsErrors`'ı kapatma · Defender'ı kapatma · empty catch bırak · CRLF'i LF yapma.

## 7. Devam Noktası

**Tamamlanan fazlar:** A (git), B (test), C (uyumluluk), D (analyzer/EULA), E (güvenlik),
F (dayanıklılık), G (kalite kapısı), H (i18n), I (güvenlik/facade testleri).

**Dağıtım/kullanılabilirlik planı** (`~/.claude/plans/projeyi-detayl-oku-kurulum-*.md`):
- **Faz 0–1 TAMAM:** repo hijyeni + `global.json`/`NuGet.config` + `LICENSE`/`PRIVACY.md` ·
  tek-kaynak sürüm · `ServiceInstaller` verb'leri (`install-service`/`uninstall-service`/
  `service-status` — kurulum donmasının kök nedeni giderildi) · uygulama ikonu ·
  self-contained publish varsayılanı + sağlık kontrolü · `.iss` yeniden yazımı (kısayol,
  PATH, opsiyonel servis, ProgramData sorusu) · WiX silindi · winget `inno` tipine geçti ·
  CI tek hatta indi · Updater gerçek depoya bağlandı ve 404'te artık "Güncel" demiyor.
- **Faz 2 TAMAM:** `WindowsVersionInfo` gerçek `EditionID` okuyor (Home'da wbadmin
  sunulmuyor) · `LoggingBootstrap` → `Safety/Diagnostics` (App+Service+CLI aynı klasöre:
  `app-`/`service-`/`cli-`) · `GuardServiceController` + **Guard sekmesi**
  (kur/başlat/durdur/kaldır/onar, journal'a yazar) · `Orchestration/Preflight`
  (`SystemRequirementsChecker` 9 madde + `Elevation` + `PreflightException`) ·
  **FirstRunWindow** (gereksinim raporu, engelleyen maddede ana pencere açılmaz) ·
  **Sistem & Veri sekmesi** (raporu tekrar çalıştırma + teşhis dışa aktarma) ·
  `OnStartup` → handler'lar önce + `try{Bootstrap()}` + **ErrorDialog** (düz `Window`,
  `%LOCALAPPDATA%\WinOptimizer\startup-error.txt` yedek raporu) · teşhis paketi artık
  servis günlüğü/durumu, dumps, olay günlüğü ve gereksinim raporunu içeriyor ·
  named pipe DACL (SYSTEM + Administrators) · CLI yönetici kapısı · `SchedulerViewModel`
  ve `GuardServiceController` artık var olmayan exe yolunu "tahmin" etmiyor.
- **Faz 3 TAMAM:** ölü ayarlar tüketicilerine bağlandı (`CultureBootstrap`/`ThemeBootstrap`,
  `AutoRestorePoint`→`PrepareSafetyAsync`, `AutoRegistryBackup`→`SafetyNet` bayrağı,
  `DashboardLiveMetrics`→`OverviewTab`, `MetricsPollSeconds` `SettingsChanged`'da,
  `Save()`→`bool` ve arayüz hatayı gösteriyor) · **onay kapısı**
  (`Orchestration/Confirmation`: `ConfirmationGate` + `IActionConfirmation`; iki çağıran —
  `JobOrchestrationEngine` (tek-tık+CLI) ve `ModulePageViewModel`; `DialogActionConfirmation`,
  `ConsoleActionConfirmation` (`--allow-risky`), `AutoApproveConfirmation`) ·
  **güvenli tek-tık** (`DefaultOneClickModules` = Clean/Memory/Storage/Update; `null` artık
  "tüm modüller" değil `EnabledModules`; `ExecuteAllAsync` ayrı; **Modüller sekmesi**;
  eksik 6 nav öğesi eklendi; ölü 3 ProjectReference çıkarıldı) · **servis ayar okuyor**
  (`GuardSettingsProvider`, 5 sn stat-poll, guard kapatılabilir, IPC `config` komutu) ·
  **`RemediationEngine`** (`AutoRemediate` varsayılan KAPALI + eylem başına izin, temp
  silme izin-listesi/7 gün/500 dosya sınırı, `EmptyRecycleBin` kaldırıldı, journal'a yazıyor) ·
  **journal HMAC doğrulaması rollback'e bağlandı** · `IntegrityKeyStore` → **LocalMachine**
  (göç yollu) · `schtasks` `/ru SYSTEM /np` + `ArgumentList` + stderr · sistem sürücüsü
  artık `C:` sabit değil.
- **Kalan:** Faz 0.1 (git remote — kullanıcı yapacak) · Faz 1.9C (Windows Sandbox'ta temiz
  PC doğrulaması) · UI'ın canlı görsel doğrulaması (uygulama `requireAdministrator`).
- **Bilinen kozmetik borç:** `GuardMetric`/`LiveMetric` alan adları hâlâ `CDriveFree*`
  (davranış sistem sürücüsüne göre doğru; yalnız isim yanıltıcı) · `MainWindow.xaml` nav
  etiketleri ve CLI metinleri hâlâ hardcoded Türkçe · `ActiveProfile` bilinçli tüketilmiyor
  (`ProfileManagerModule` DI'ya kayıtlı değil).

**Test sayısı:** 238 (27 Updater + 101 Core + 96 Modules + 14 E2E).

## 9. Publish sırası tuzağı (build-installer.ps1)

`dotnet publish -o <ortak klasör>` **önceki publish'lerden kalan sahipsiz dosyaları siler.**
Bu yüzden sıra: **App → Service → Cli**. App en sona alınırsa yalnızca Cli'nin referansladığı
`WinOptimizer.Modules.BenchmarkEngine.dll` silinir ve `WinOptimizer.Cli benchmark` kurulumda
patlar. Sağlık kontrolü (adım 4) bu dosyayı ve `en\WinOptimizer.App.resources.dll`'i ayrıca
doğrular — sırayı değiştirirsen kontrol seni uyarır.

## 10. WPF-UI tema tuzağı — açık zeminli pencereler (ZORUNLU KURAL)

`App.xaml` → `Resources/AppResources.xaml` içindeki `<ui:ControlsDictionary />`, WPF-UI'ın
**yerel WPF tiplerine de** örtük stil uygulayan sözlüğüdür (96 tip: `Button`, `CheckBox`,
`TextBox`, `Expander`, `ScrollViewer`, hatta `Window`). Koyu temada bunların `Foreground`'u
**saf beyaz** (`#FFFFFFFF`) olur.

> **Düz `System.Windows.Window` + `SystemColors` kullanan HER pencere
> `Resources/SystemChromeDictionary.xaml`'i merge ETMEK ZORUNDADIR:**
> ```xaml
> <Window.Resources>
>     <ResourceDictionary Source="pack://application:,,,/WinOptimizer.App;component/Resources/SystemChromeDictionary.xaml" />
> </Window.Resources>
> ```

Yoksa: pencere beyaz (`SystemColors.WindowBrush`), düğme yazısı beyaz → **görünmez düğme.**
`TextBlock`'lar görünmeye devam ettiği için hata "yerleşim bozuk" sanılır; Sandbox'ta iki kez
yanlış teşhis edildi. WPF-UI'ın `TextBlock` stili `Foreground` set etmez, diğerleri eder —
asimetrinin sebebi budur.

`ui:FluentWindow` tabanlı pencerelere (MainWindow + sekmeleri) **uygulanmaz**; onlar koyu
temayı ve `ui:Button`'ları bilerek kullanır.

**`pack://` URI'leri derleme-nitelikli yazılır** (`/WinOptimizer.App;component/...`): kısa
form giriş derlemesine göre çözülür ve XAML başka bir host'tan yüklenince bulunamaz.

## 11. UI önizleme aracı — sandbox'a gitmeden pencereyi gör

```powershell
dotnet run --project dev\WinOptimizer.UiPreview -c Release -- <çıktı-dizini>
```

`WinOptimizer.App` `requireAdministrator` ile çalışır: arayüzü görmek normalde kurulum ya da
Windows Sandbox turu gerektirir. Bu araç pencereleri **uygulamayı çalıştırmadan** PNG'ye
render eder (yükseltilmemiş, UAC istemez) — bir XAML değişikliğinin doğru olup olmadığı
paket üretmeden anlaşılır. Üretilenler: FirstRunWindow (normal + sona kaydırılmış),
ErrorDialog (normal + Expander açık), ActionConfirmationDialog.

Araç `AppResources.xaml`'i **merge eder, kopyalamaz** — bu yüzden App.xaml'de kaynak
tanımı yapılmaz, hepsi `AppResources.xaml`'e yazılır; aksi halde önizleme uygulamadan
sürüklenir ve yanlış güven verir. Dağıtıma girmez (`build-installer.ps1` yalnız
App/Service/Cli publish eder).

## 8. Önemli Notlar

- SourceLink "uzak depo yok" uyarıları beklenen (git remote yok); hata değildir.
- Geri alma notları ve journal içeriği **kasıtlı TR** kalır (iç tutarlılık); yalnızca UI TR/EN.
- `WinOptimizer.Modules` `DisplayName`'i hard-coded TR varsayılan; `App` `ModuleDisplayNameResolver`
  ile EN çözer (eksikse TR'ye fallback).
