param(
    [Parameter(Mandatory=$true)]
    [string]$Url,

    [string]$OutputPath = ""
)

$ErrorActionPreference = "Stop"

$baseDir = Split-Path -Parent (Split-Path -Parent $MyInvocation.MyCommand.Path)
$ytDlp = Join-Path $baseDir "yt-dlp.exe"
$cookiesFile = Join-Path $baseDir "youtube-cookies.txt"

if (-not (Test-Path -LiteralPath $ytDlp)) {
    throw "No se encontro yt-dlp.exe en $baseDir"
}

if ([string]::IsNullOrWhiteSpace($OutputPath)) {
    $exportDir = Join-Path $baseDir "exports"
    New-Item -ItemType Directory -Force -Path $exportDir | Out-Null
    $stamp = Get-Date -Format "yyyyMMdd-HHmmss"
    $OutputPath = Join-Path $exportDir "playlist-links-$stamp.txt"
}

function Get-PlaylistUrl {
    param([string]$InputUrl)
    if ($InputUrl -match "[?&]list=([^&]+)") {
        return "https://www.youtube.com/playlist?list=$($Matches[1])"
    }
    return $InputUrl
}

function Invoke-YtDlpFlat {
    param(
        [string]$TargetUrl,
        [string[]]$CookieArgs,
        [string]$Label
    )

    $print = "%(playlist_index)s`t%(title)s`t%(uploader)s`t%(duration_string)s`t%(webpage_url)s"
    $args = @()
    if ($CookieArgs) { $args += $CookieArgs }
    $args += @("--flat-playlist", "--ignore-errors", "--print", $print, $TargetUrl)

    $previousErrorActionPreference = $ErrorActionPreference
    $ErrorActionPreference = "Continue"
    try {
        $raw = (& $ytDlp @args 2>&1 | ForEach-Object { $_.ToString() }) -join [Environment]::NewLine
        $exitCode = $LASTEXITCODE
    }
    finally {
        $ErrorActionPreference = $previousErrorActionPreference
    }

    return [pscustomobject]@{
        Label = $Label
        TargetUrl = $TargetUrl
        ExitCode = $exitCode
        Output = $raw
        Args = ($args -join " ")
    }
}

function Convert-YtDlpRows {
    param([string]$Raw)

    $rows = New-Object System.Collections.Generic.List[object]
    if ([string]::IsNullOrWhiteSpace($Raw)) { return $rows }

    foreach ($line in ($Raw -split "\r?\n")) {
        if ([string]::IsNullOrWhiteSpace($line)) { continue }
        $parts = $line -split "`t"
        if ($parts.Count -lt 5) { continue }
        if ($parts[4] -notmatch "^https?://") { continue }
        $rows.Add([pscustomobject]@{
            Index = $parts[0]
            Title = $parts[1]
            Channel = $parts[2]
            Duration = $parts[3]
            Url = $parts[4]
        })
    }

    return $rows
}

function Add-Diagnostics {
    param(
        [System.Collections.Generic.List[string]]$TargetLines,
        [object[]]$AttemptList
    )

    $TargetLines.Add("# Diagnostico:")
    foreach ($attempt in $AttemptList) {
        $TargetLines.Add("# Intento: $($attempt.Label)")
        $TargetLines.Add("# ExitCode: $($attempt.ExitCode)")
        if ($attempt.Output) {
            foreach ($errLine in ($attempt.Output -split "\r?\n")) {
                if ([string]::IsNullOrWhiteSpace($errLine)) { continue }
                if ($errLine -match "^\s*(WARNING|ERROR|\[youtube|YouTube said|HTTP Error)") {
                    $TargetLines.Add("# $errLine")
                }
            }
        }
    }
}

function Add-UniqueRows {
    param(
        [System.Collections.Generic.List[object]]$SourceRows,
        [System.Collections.Generic.List[string]]$TargetLines
    )

    $seen = @{}
    foreach ($row in $SourceRows) {
        if ($seen.ContainsKey($row.Url)) { continue }
        $seen[$row.Url] = $true
        $TargetLines.Add("$($row.Index)`t$($row.Title)`t$($row.Channel)`t$($row.Duration)`taudio/video`t$($row.Url)")
    }
}

$playlistUrl = Get-PlaylistUrl $Url
$attempts = New-Object System.Collections.Generic.List[object]

if (Test-Path -LiteralPath $cookiesFile) {
    $attempts.Add((Invoke-YtDlpFlat -TargetUrl $playlistUrl -CookieArgs @("--cookies", $cookiesFile) -Label "playlist con cookies locales"))
}

$attempts.Add((Invoke-YtDlpFlat -TargetUrl $playlistUrl -CookieArgs @() -Label "playlist publica sin cookies"))

if ($playlistUrl -ne $Url) {
    if (Test-Path -LiteralPath $cookiesFile) {
        $attempts.Add((Invoke-YtDlpFlat -TargetUrl $Url -CookieArgs @("--cookies", $cookiesFile) -Label "video actual con cookies locales"))
    }
    $attempts.Add((Invoke-YtDlpFlat -TargetUrl $Url -CookieArgs @() -Label "video actual publico sin cookies"))
}

$playlistAttempts = @($attempts | Where-Object { $_.TargetUrl -eq $playlistUrl })
$watchAttempts = @($attempts | Where-Object { $_.TargetUrl -ne $playlistUrl })
$playlistRows = New-Object System.Collections.Generic.List[object]
$watchRows = New-Object System.Collections.Generic.List[object]

foreach ($attempt in $playlistAttempts) {
    $rows = Convert-YtDlpRows -Raw $attempt.Output
    foreach ($row in $rows) { $playlistRows.Add($row) }
}

foreach ($attempt in $watchAttempts) {
    $rows = Convert-YtDlpRows -Raw $attempt.Output
    foreach ($row in $rows) { $watchRows.Add($row) }
}

$lines = New-Object System.Collections.Generic.List[string]
$lines.Add("# ARKAIOS Playlist Link Export")
$lines.Add("# Fecha: $(Get-Date -Format o)")
$lines.Add("# URL original: $Url")
$lines.Add("# URL playlist: $playlistUrl")
$lines.Add("# No descarga multimedia. Solo exporta enlaces detectados.")
$lines.Add("# Columnas: index | titulo | canal | duracion | tipo_descarga | url")
$lines.Add("")

if ($playlistRows.Count -gt 0) {
    Add-UniqueRows -SourceRows $playlistRows -TargetLines $lines
} else {
    $lines.Add("# NO SE PUDO EXTRAER LA PLAYLIST COMPLETA")
    $lines.Add("# La URL de playlist fue probada directamente, pero YouTube no entrego los items al extractor.")
    Add-Diagnostics -TargetLines $lines -AttemptList $attempts
    if ($watchRows.Count -gt 0) {
        $lines.Add("")
        $lines.Add("# VIDEO ACTUAL DETECTADO EN LA URL ORIGINAL")
        Add-UniqueRows -SourceRows $watchRows -TargetLines $lines
    }
}

Set-Content -LiteralPath $OutputPath -Value $lines -Encoding UTF8
Write-Output $OutputPath
