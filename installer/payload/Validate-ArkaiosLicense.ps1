param(
    [Parameter(Mandatory=$true)][string]$KeyFile,
    [Parameter(Mandatory=$true)][string]$ResultFile
)

$ErrorActionPreference = "Stop"
$salt = "ARKAIOS_SECRET_KEY_2026_NEXUS"
$apiUrl = "https://servidor-arkaios-api.vercel.app/api/licenses/validate"

function Write-Result {
    param([string]$Status, [string]$Message)
    Set-Content -LiteralPath $ResultFile -Value "$Status|$Message" -Encoding UTF8
}

function Get-ArkaiosHardwareId {
    try {
        $nics = [System.Net.NetworkInformation.NetworkInterface]::GetAllNetworkInterfaces()
        foreach ($nic in $nics) {
            if ($nic.OperationalStatus -eq [System.Net.NetworkInformation.OperationalStatus]::Up -and
                $nic.NetworkInterfaceType -ne [System.Net.NetworkInformation.NetworkInterfaceType]::Loopback) {
                return ($nic.GetPhysicalAddress().ToString())
            }
        }
    } catch {}

    return "HWID_NOT_FOUND_$env:COMPUTERNAME"
}

function Get-Sha256Hex {
    param([string]$InputText)
    $sha = [System.Security.Cryptography.SHA256]::Create()
    try {
        $bytes = [System.Text.Encoding]::UTF8.GetBytes($InputText)
        $hash = $sha.ComputeHash($bytes)
        return (($hash | ForEach-Object { $_.ToString("x2") }) -join "")
    } finally {
        $sha.Dispose()
    }
}

try {
    if (-not (Test-Path -LiteralPath $KeyFile)) {
        Write-Result "ERROR" "No se recibió licencia."
        exit 2
    }

    $key = (Get-Content -LiteralPath $KeyFile -Raw).Trim()
    if ([string]::IsNullOrWhiteSpace($key)) {
        Write-Result "ERROR" "Pega tu serial/licencia para continuar."
        exit 2
    }

    try {
        $decoded = [System.Text.Encoding]::UTF8.GetString([System.Convert]::FromBase64String($key))
    } catch {
        Write-Result "ERROR" "Formato de licencia inválido."
        exit 3
    }

    $parts = $decoded.Split('|')
    if ($parts.Length -ne 6) {
        Write-Result "ERROR" "Licencia incompleta."
        exit 3
    }

    $licenseHwid = $parts[0]
    $type = $parts[1]
    $name = $parts[2]
    $phone = $parts[3]
    $timestamp = $parts[4]
    $signature = $parts[5]
    $currentHwid = Get-ArkaiosHardwareId

    $expectedSignature = Get-Sha256Hex "$licenseHwid|$type|$name|$phone|$timestamp|$salt"
    if ($signature -ne $expectedSignature) {
        Write-Result "ERROR" "Firma de licencia inválida."
        exit 4
    }

    if ($type -eq "BASIC" -and $licenseHwid -ne $currentHwid) {
        Write-Result "ERROR" "La licencia BASIC no corresponde a este equipo. HWID: $currentHwid"
        exit 5
    }

    try {
        [Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12
        $payload = @{ key = $key; hwid = $currentHwid } | ConvertTo-Json -Compress
        $response = Invoke-RestMethod -Uri $apiUrl -Method Post -Body $payload -ContentType "application/json" -TimeoutSec 20
        if (-not $response.success) {
            Write-Result "ERROR" "Servidor rechazó la licencia."
            exit 6
        }
    } catch {
        Write-Result "ERROR" "No se pudo validar con Arkaios API. Revisa internet o genera una licencia activa en arkaios-world.web.app"
        exit 7
    }

    $licenseDir = Join-Path $env:APPDATA "ArkaiosDJNexus"
    if (-not (Test-Path -LiteralPath $licenseDir)) {
        New-Item -ItemType Directory -Path $licenseDir -Force | Out-Null
    }
    Set-Content -LiteralPath (Join-Path $licenseDir "license.key") -Value $key -Encoding UTF8
    Write-Result "OK" "Licencia validada para $name."
    exit 0
} catch {
    Write-Result "ERROR" $_.Exception.Message
    exit 99
}
