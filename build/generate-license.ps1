#requires -Version 5.1
<#
.SYNOPSIS
    docs\EULA.md dosyasını kurulum sihirbazının gösterdiği license.rtf'e dönüştürür.

.DESCRIPTION
    Lisans metninin TEK KAYNAĞI docs\EULA.md'dir. WiX (WixUILicenseRtf) ve Inno Setup
    (LicenseFile) RTF beklediğinden, RTF derleme sırasında üretilir ve commit edilmez —
    böylece Markdown ile kurulumda gösterilen metin birbirinden ayrışamaz.

    Desteklenen Markdown alt kümesi (EULA.md'de fiilen kullanılan): başlıklar (#, ##),
    kalın (**...**), madde imleri (-), tablolar (| ... |), yatay çizgi (---),
    alıntı blokları (>) ve satır içi kod (`...`).

.PARAMETER SourceFile
    Kaynak Markdown (varsayılan: docs\EULA.md).

.PARAMETER OutputFile
    Üretilecek RTF (varsayılan: installer\license.rtf — WiX ve Inno Setup ortak kullanır).

.EXAMPLE
    .\generate-license.ps1
    .\generate-license.ps1 -SourceFile ..\docs\EULA.md -OutputFile ..\installer\license.rtf
#>
[CmdletBinding()]
param(
    [string]$SourceFile,
    [string]$OutputFile
)

$ErrorActionPreference = 'Stop'
$root = Resolve-Path (Join-Path $PSScriptRoot '..')

if (-not $SourceFile) { $SourceFile = Join-Path $root 'docs\EULA.md' }
if (-not $OutputFile) { $OutputFile = Join-Path $root 'installer\license.rtf' }

if (-not (Test-Path $SourceFile)) {
    throw "Lisans kaynagi bulunamadi: $SourceFile"
}

# RTF'te ters bolu, susler ve ASCII disi karakterler kacislanmalidir.
# Turkce karakterler \uNNNN? bicimiyle yazilir (RTF Unicode kacisi).
function ConvertTo-RtfText {
    param([string]$Text)
    $sb = New-Object System.Text.StringBuilder
    foreach ($ch in $Text.ToCharArray()) {
        $code = [int]$ch
        switch ($ch) {
            '\' { [void]$sb.Append('\\'); continue }
            '{' { [void]$sb.Append('\{'); continue }
            '}' { [void]$sb.Append('\}'); continue }
            default {
                if ($code -lt 128) {
                    [void]$sb.Append($ch)
                }
                else {
                    # RTF isaretli 16-bit bekler; 32767 ustu degerler negatife cevrilir.
                    $signed = if ($code -gt 32767) { $code - 65536 } else { $code }
                    [void]$sb.Append("\u$signed`?")
                }
            }
        }
    }
    return $sb.ToString()
}

# Satir ici bicimlendirme: **kalin** ve `kod`.
function Convert-InlineMarkdown {
    param([string]$Line)
    $escaped = ConvertTo-RtfText $Line
    # **kalin** -> \b ... \b0
    $escaped = [regex]::Replace($escaped, '\*\*(.+?)\*\*', '\b $1\b0 ')
    # `kod` -> tek aralikli yazi tipi
    $escaped = [regex]::Replace($escaped, '`(.+?)`', '{\f1 $1}')
    # [metin](baglanti) -> yalnizca metin (kurulum sihirbazinda tiklanabilir baglanti yok)
    $escaped = [regex]::Replace($escaped, '\[(.+?)\]\((.+?)\)', '$1')
    return $escaped
}

$lines = Get-Content -Path $SourceFile -Encoding UTF8
$body = New-Object System.Text.StringBuilder
$inTable = $false

foreach ($line in $lines) {
    $trimmed = $line.TrimEnd()

    # Tablo ayirici satiri (|---|---|) atlanir.
    if ($trimmed -match '^\s*\|[\s\-:|]+\|\s*$') { continue }

    # Tablo satiri: hucreleri " - " ile ayrilmis tek satira dokeriz (RTF tablosu asiri karmasik).
    if ($trimmed -match '^\s*\|.*\|\s*$') {
        $cells = ($trimmed.Trim('|') -split '\|') | ForEach-Object { $_.Trim() } | Where-Object { $_ -ne '' }
        $text = Convert-InlineMarkdown ($cells -join '  -  ')
        if (-not $inTable) { $inTable = $true }
        [void]$body.AppendLine("\li360 $text\par")
        continue
    }
    elseif ($inTable) {
        $inTable = $false
        [void]$body.AppendLine('\li0\par')
    }

    switch -Regex ($trimmed) {
        '^\s*$' {
            [void]$body.AppendLine('\par')
        }
        '^---+\s*$' {
            # Yatay cizgi: ince bir ayirici olarak bos paragraf.
            [void]$body.AppendLine('\par')
        }
        '^#\s+(.*)$' {
            $text = Convert-InlineMarkdown $Matches[1]
            [void]$body.AppendLine("\pard\sa180\sb180\qc\b\fs32 $text\b0\fs20\par\pard\sa120")
        }
        '^##\s+(.*)$' {
            $text = Convert-InlineMarkdown $Matches[1]
            [void]$body.AppendLine("\pard\sa120\sb180\b\fs24 $text\b0\fs20\par\pard\sa120")
        }
        '^###\s+(.*)$' {
            $text = Convert-InlineMarkdown $Matches[1]
            [void]$body.AppendLine("\pard\sa120\sb120\b $text\b0\par")
        }
        '^>\s?(.*)$' {
            $text = Convert-InlineMarkdown $Matches[1]
            [void]$body.AppendLine("\pard\li360\ri360\i $text\i0\par\pard\sa120")
        }
        '^[-*]\s+(.*)$' {
            $text = Convert-InlineMarkdown $Matches[1]
            [void]$body.AppendLine("\pard\fi-200\li560\bullet\tab $text\par\pard\sa120")
        }
        default {
            $text = Convert-InlineMarkdown $trimmed
            [void]$body.AppendLine("$text\par")
        }
    }
}

# RTF belgesi: Segoe UI (f0) + Consolas (f1); varsayilan 10pt (\fs20).
$rtf = @"
{\rtf1\ansi\ansicpg1254\deff0
{\fonttbl{\f0\fswiss\fcharset162 Segoe UI;}{\f1\fmodern\fcharset162 Consolas;}}
\viewkind4\uc1\pard\sa120\f0\fs20
$($body.ToString())}
"@

$outDir = Split-Path $OutputFile -Parent
if (-not (Test-Path $outDir)) { New-Item -ItemType Directory -Force -Path $outDir | Out-Null }

# RTF ASCII olmalidir (Unicode karakterler zaten \uNNNN? olarak kacislandi).
[System.IO.File]::WriteAllText($OutputFile, $rtf, [System.Text.Encoding]::ASCII)

Write-Host "==> Lisans RTF uretildi: $OutputFile ($([math]::Round((Get-Item $OutputFile).Length / 1KB, 1)) KB)" -ForegroundColor Green
Write-Host "    Kaynak: $SourceFile" -ForegroundColor DarkGray
