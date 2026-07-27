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

**Tam paketleme (MSI):**
```powershell
.\build\build-installer.ps1 -SkipSign    # imzasız geliştirme MSI
.\build\build-installer.ps1              # tam hat (EV sertifika gerektir)
```

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

**Kalancak** (bkz. `PLAN.md`): Faz I devamı (düşük riskli modül testleri → §18.3 %) ·
Faz J (gerçek makinede MSI kur/kaldır, arm64 MSI + EV sertifika — ertelendi).

## 8. Önemli Notlar

- SourceLink "uzak depo yok" uyarıları beklenen (git remote yok); hata değildir.
- Geri alma notları ve journal içeriği **kasıtlı TR** kalır (iç tutarlılık); yalnızca UI TR/EN.
- `WinOptimizer.Modules` `DisplayName`'i hard-coded TR varsayılan; `App` `ModuleDisplayNameResolver`
  ile EN çözer (eksikse TR'ye fallback).
