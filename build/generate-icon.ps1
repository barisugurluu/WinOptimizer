#requires -Version 5.1
<#
.SYNOPSIS
    WinOptimizer uygulama ikonunu (.ico) uretir — cok cozunurluklu, PNG sikistirmali.

.DESCRIPTION
    Ikon binary'sini repoda "gizemli dosya" olarak tutmak yerine kodla uretilir; boylece
    renk/bicim degisikligi diff'lenebilir ve yeniden uretilebilir.

    Neden cok cozunurluk: Windows 16px'i baslik cubugu/gorev cubugunda, 32px'i masaustunde,
    256px'i Explorer buyuk simge gorunumunde kullanir. Tek boyutlu .ico Baslat menusunde
    bulanik/bozuk gorunur.

    Bicim secimi (uyumluluk): 64px ve altindaki girdiler klasik DIB (BITMAPINFOHEADER +
    32bpp BGRA + AND maskesi) olarak, 128/256px girdileri PNG olarak yazilir. Sebebi:
    PNG sikistirmali girdileri Vista+ kabuk okur ama GDI+/eski Win32 ikon API'leri
    (ornegin System.Drawing.Icon.DrawIcon) okuyamaz — kucuk boyutlari DIB tutmak
    "her yerde gorunur" garantisini verir, buyukleri PNG tutmak dosyayi kucuk tutar.

.PARAMETER OutputPath
    Uretilecek .ico yolu.

.EXAMPLE
    .\generate-icon.ps1
#>
[CmdletBinding()]
param(
    [string]$OutputPath = (Join-Path $PSScriptRoot '..\src\WinOptimizer.App\Resources\WinOptimizer.ico')
)

$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing

# Baslat menusunde/gorev cubugunda 16px'te bile okunabilir olmasi icin: dolu yuvarlak kare +
# tek, kalin, beyaz yukari ok (performans artisi). Ince detay (gosterge yelkovani vb.)
# 16px'te lekeye donusur, bu yuzden bilincli olarak sade tutuldu.
$Base = 256

function New-IconBitmap {
    param([int]$Size)

    $bmp = New-Object System.Drawing.Bitmap($Base, $Base, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    try {
        $g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
        $g.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
        $g.Clear([System.Drawing.Color]::Transparent)

        # --- Yuvarlak kare zemin (Fluent aksan mavisi gradyani) ---
        $inset = 10
        $radius = 52
        $rect = New-Object System.Drawing.Rectangle($inset, $inset, ($Base - 2 * $inset), ($Base - 2 * $inset))
        $path = New-Object System.Drawing.Drawing2D.GraphicsPath
        $d = $radius * 2
        $path.AddArc($rect.X, $rect.Y, $d, $d, 180, 90)
        $path.AddArc(($rect.Right - $d), $rect.Y, $d, $d, 270, 90)
        $path.AddArc(($rect.Right - $d), ($rect.Bottom - $d), $d, $d, 0, 90)
        $path.AddArc($rect.X, ($rect.Bottom - $d), $d, $d, 90, 90)
        $path.CloseFigure()

        $brush = New-Object System.Drawing.Drawing2D.LinearGradientBrush(
            $rect,
            [System.Drawing.Color]::FromArgb(255, 61, 143, 255),
            [System.Drawing.Color]::FromArgb(255, 10, 65, 145),
            [System.Drawing.Drawing2D.LinearGradientMode]::ForwardDiagonal)
        $g.FillPath($brush, $path)
        $brush.Dispose()
        $path.Dispose()

        # --- Beyaz yukari ok ---
        $arrow = New-Object System.Drawing.Drawing2D.GraphicsPath
        $pts = @(
            (New-Object System.Drawing.Point(128, 52)),
            (New-Object System.Drawing.Point(198, 124)),
            (New-Object System.Drawing.Point(158, 124)),
            (New-Object System.Drawing.Point(158, 204)),
            (New-Object System.Drawing.Point(98, 204)),
            (New-Object System.Drawing.Point(98, 124)),
            (New-Object System.Drawing.Point(58, 124))
        )
        $arrow.AddPolygon([System.Drawing.Point[]]$pts)
        $white = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(255, 255, 255, 255))
        $g.FillPath($white, $arrow)
        $white.Dispose()
        $arrow.Dispose()
    }
    finally {
        $g.Dispose()
    }

    if ($Size -eq $Base) { return $bmp }

    # Yuksek kaliteli kucultme (dogrudan kucuk canvas'a cizmek yerine): kenarlar daha temiz.
    $scaled = New-Object System.Drawing.Bitmap($Size, $Size, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $sg = [System.Drawing.Graphics]::FromImage($scaled)
    try {
        $sg.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
        $sg.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
        $sg.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
        $sg.Clear([System.Drawing.Color]::Transparent)
        $sg.DrawImage($bmp, 0, 0, $Size, $Size)
    }
    finally {
        $sg.Dispose()
        $bmp.Dispose()
    }
    return $scaled
}

function ConvertTo-IcoDib {
    <#
      Bitmap'i ICO icindeki klasik DIB girdisine cevirir:
      BITMAPINFOHEADER (biHeight = 2*yukseklik: XOR + AND maskesi) + 32bpp BGRA (alttan yukari)
      + 1bpp AND maskesi (satirlar 4 bayta hizali). 32bpp'de seffaflik alfa kanalindan gelir,
      bu yuzden AND maskesi sifirdir; yine de formatin gerektirdigi boyutta bulunmasi sarttir.
    #>
    param([System.Drawing.Bitmap]$Bitmap)

    $w = $Bitmap.Width
    $h = $Bitmap.Height
    $maskStride = [int][Math]::Floor((($w + 31) / 32)) * 4

    $ms = New-Object System.IO.MemoryStream
    $bw = New-Object System.IO.BinaryWriter($ms)
    try {
        $bw.Write([UInt32]40)          # biSize
        $bw.Write([Int32]$w)           # biWidth
        $bw.Write([Int32]($h * 2))     # biHeight (XOR + AND)
        $bw.Write([UInt16]1)           # biPlanes
        $bw.Write([UInt16]32)          # biBitCount
        $bw.Write([UInt32]0)           # biCompression = BI_RGB
        $bw.Write([UInt32]($w * $h * 4 + $maskStride * $h))
        $bw.Write([Int32]0); $bw.Write([Int32]0)    # cozunurluk
        $bw.Write([UInt32]0); $bw.Write([UInt32]0)  # palet

        for ($y = $h - 1; $y -ge 0; $y--) {
            for ($x = 0; $x -lt $w; $x++) {
                $c = $Bitmap.GetPixel($x, $y)
                $bw.Write([Byte]$c.B); $bw.Write([Byte]$c.G)
                $bw.Write([Byte]$c.R); $bw.Write([Byte]$c.A)
            }
        }
        $bw.Write((New-Object Byte[] ($maskStride * $h)))
        $bw.Flush()
        # Onemli: bastaki virgul (unary comma) sart. Aksi halde PowerShell Byte[]'i
        # tek tek bayt olarak pipeline'a acar; cagiran tarafta Object[] olusur ve
        # BinaryWriter.Write yanlis asiri yuklemeyi secip veriyi bozar/kirpar.
        return , $ms.ToArray()
    }
    finally {
        $bw.Dispose()
        $ms.Dispose()
    }
}

$sizes = @(16, 24, 32, 48, 64, 128, 256)
$pngs = @()
foreach ($size in $sizes) {
    $bmp = New-IconBitmap -Size $size
    if ($size -le 64) {
        [byte[]]$bytes = ConvertTo-IcoDib -Bitmap $bmp
    }
    else {
        $ms = New-Object System.IO.MemoryStream
        $bmp.Save($ms, [System.Drawing.Imaging.ImageFormat]::Png)
        [byte[]]$bytes = $ms.ToArray()
        $ms.Dispose()
    }
    $bmp.Dispose()
    $pngs += , @{ Size = $size; Bytes = $bytes }
}

# --- ICO konteyneri ---
# ICONDIR (6 bayt) + her goruntu icin ICONDIRENTRY (16 bayt) + goruntu verileri.
# 256px girdisinde genislik/yukseklik bayti 0 olarak yazilir (format kurali).
$out = New-Object System.IO.MemoryStream
$bw = New-Object System.IO.BinaryWriter($out)
try {
    $bw.Write([UInt16]0)              # reserved
    $bw.Write([UInt16]1)              # type = icon
    $bw.Write([UInt16]$pngs.Count)

    $offset = 6 + (16 * $pngs.Count)
    foreach ($png in $pngs) {
        $dim = if ($png.Size -ge 256) { 0 } else { $png.Size }
        $bw.Write([Byte]$dim)         # width
        $bw.Write([Byte]$dim)         # height
        $bw.Write([Byte]0)            # palette rengi yok (32bpp)
        $bw.Write([Byte]0)            # reserved
        $bw.Write([UInt16]1)          # color planes
        $bw.Write([UInt16]32)         # bits per pixel
        $bw.Write([UInt32]$png.Bytes.Length)
        $bw.Write([UInt32]$offset)
        $offset += $png.Bytes.Length
    }
    foreach ($png in $pngs) {
        $bw.Write([byte[]]$png.Bytes)
    }
    $bw.Flush()

    $dir = Split-Path $OutputPath -Parent
    if (-not (Test-Path $dir)) { New-Item -ItemType Directory -Path $dir -Force | Out-Null }
    [System.IO.File]::WriteAllBytes($OutputPath, $out.ToArray())
}
finally {
    $bw.Dispose()
    $out.Dispose()
}

$info = Get-Item $OutputPath
Write-Host ("==> {0} uretildi: {1} boyut, {2:N0} bayt" -f $info.FullName, $pngs.Count, $info.Length) -ForegroundColor Green
