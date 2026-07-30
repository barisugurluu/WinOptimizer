# WinOptimizer — Gizlilik

**Sıfır telemetri.** WinOptimizer kullanım verisi, cihaz kimliği, tarama sonucu veya
kişisel veri toplamaz ve hiçbir sunucuya göndermez. Analiz ve günlük çıktılarının
tamamı yalnızca kendi bilgisayarınızda, `%ProgramData%\WinOptimizer` altında kalır.

**Tek ağ isteği güncelleme denetimidir:** `WinOptimizer.Cli update --check`
komutunu siz çalıştırdığınızda GitHub Releases API'sine sürüm sorgusu yapılır.
Bu istek yalnızca sürüm numarası okur; hiçbir veri gönderilmez.

**Teşhis paketi** (`Sistem & Veri → Teşhis paketi oluştur`) yerel bir `.zip`
üretir ve otomatik olarak hiçbir yere yüklenmez — nereye göndereceğinize siz karar
verirsiniz. İçeriği paket içindeki `OKUBENI.txt` dosyasında listelenir.

Tam metin: [`docs/GIZLILIK.md`](docs/GIZLILIK.md)
