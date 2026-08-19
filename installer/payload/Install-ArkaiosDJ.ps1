param(
    [string]$InstallDir = "$env:LOCALAPPDATA\Programs\Arkaios DJ Nexus",
    [string]$ApiBase = $(if ($env:ARKAIOS_DJ_LICENSE_API) { $env:ARKAIOS_DJ_LICENSE_API } else { "http://127.0.0.1:3000" }),
    [string]$RegistrationUrl = $(if ($env:ARKAIOS_DJ_REGISTRATION_URL) { $env:ARKAIOS_DJ_REGISTRATION_URL } else { "https://arkaios-world.web.app/" }),
    [switch]$SkipServerValidation
)

Add-Type -AssemblyName System.Windows.Forms
Add-Type -AssemblyName System.Drawing

$ErrorActionPreference = "Stop"
$SourceDir = if (Test-Path (Join-Path $PSScriptRoot "ArkaiosDJ.exe")) {
    $PSScriptRoot
} else {
    Split-Path -Parent $PSScriptRoot
}
$LicenseDir = Join-Path $env:APPDATA "ArkaiosDJNexus"
$LicensePath = Join-Path $LicenseDir "license.key"
$Salt = "ARKAIOS_SECRET_KEY_2026_NEXUS"

function Get-ArkaiosHardwareId {
    try {
        $nic = Get-CimInstance Win32_NetworkAdapter |
            Where-Object { $_.MACAddress -and $_.NetEnabled -eq $true } |
            Select-Object -First 1
        if ($nic) {
            return ($nic.MACAddress -replace ":", "").ToUpperInvariant()
        }
    } catch {}
    return "HWID_NOT_FOUND_$env:COMPUTERNAME"
}

function Get-Sha256Hex([string]$InputText) {
    $sha = [System.Security.Cryptography.SHA256]::Create()
    try {
        $bytes = [System.Text.Encoding]::UTF8.GetBytes($InputText)
        $hash = $sha.ComputeHash($bytes)
        return -join ($hash | ForEach-Object { $_.ToString("x2") })
    } finally {
        $sha.Dispose()
    }
}

function Test-LicenseLocal([string]$Key) {
    if ([string]::IsNullOrWhiteSpace($Key)) {
        throw "La licencia esta vacia."
    }

    try {
        $decoded = [System.Text.Encoding]::UTF8.GetString([Convert]::FromBase64String($Key.Trim()))
    } catch {
        throw "La licencia no es Base64 valido."
    }

    $parts = $decoded.Split("|")
    if ($parts.Count -ne 6) {
        throw "La licencia no tiene el formato esperado."
    }

    $hwid = $parts[0]
    $type = $parts[1]
    $name = $parts[2]
    $phone = $parts[3]
    $timestamp = $parts[4]
    $signature = $parts[5]
    $expected = Get-Sha256Hex "$hwid|$type|$name|$phone|$timestamp|$Salt"

    if ($signature -ne $expected) {
        throw "La firma criptografica de la licencia no coincide."
    }

    $localHwid = Get-ArkaiosHardwareId
    if ($type -eq "BASIC" -and $hwid -ne $localHwid) {
        throw "La licencia BASIC pertenece a otro equipo. HWID local: $localHwid"
    }

    if ($type -ne "BASIC" -and $type -ne "LIFETIME") {
        throw "Tipo de licencia no reconocido: $type"
    }

    return [PSCustomObject]@{
        Hwid = $hwid
        Type = $type
        Name = $name
        Phone = $phone
        Timestamp = $timestamp
    }
}

function Test-LicenseServer([string]$Key, [string]$Hwid) {
    $payload = @{ key = $Key; hwid = $Hwid } | ConvertTo-Json -Compress
    $response = Invoke-RestMethod -Uri "$($ApiBase.TrimEnd('/'))/api/licenses/validate" -Method Post -ContentType "application/json" -Body $payload -TimeoutSec 8
    if ($response.success -eq $false) {
        throw $response.message
    }
    return $response
}

function Show-LicenseDialog {
    $form = New-Object Windows.Forms.Form
    $form.Text = "Arkaios DJ Nexus - Instalador"
    $form.Size = New-Object Drawing.Size(620, 500)
    $form.StartPosition = "CenterScreen"
    $form.BackColor = [Drawing.Color]::FromArgb(18, 18, 24)
    $form.ForeColor = [Drawing.Color]::White
    $form.FormBorderStyle = "FixedDialog"
    $form.MaximizeBox = $false

    $title = New-Object Windows.Forms.Label
    $title.Text = "Activacion e instalacion de Arkaios DJ Nexus"
    $title.Font = New-Object Drawing.Font("Segoe UI", 14, [Drawing.FontStyle]::Bold)
    $title.AutoSize = $true
    $title.Location = New-Object Drawing.Point(24, 22)
    $form.Controls.Add($title)

    $hwid = New-Object Windows.Forms.Label
    $hwid.Text = "HWID de este equipo: $(Get-ArkaiosHardwareId)"
    $hwid.Font = New-Object Drawing.Font("Consolas", 9)
    $hwid.ForeColor = [Drawing.Color]::LightCyan
    $hwid.AutoSize = $true
    $hwid.Location = New-Object Drawing.Point(24, 62)
    $form.Controls.Add($hwid)

    $label = New-Object Windows.Forms.Label
    $label.Text = "Pega tu serial key:"
    $label.AutoSize = $true
    $label.Location = New-Object Drawing.Point(24, 98)
    $form.Controls.Add($label)

    $text = New-Object Windows.Forms.TextBox
    $text.Multiline = $true
    $text.ScrollBars = "Vertical"
    $text.Font = New-Object Drawing.Font("Consolas", 9)
    $text.BackColor = [Drawing.Color]::FromArgb(32, 35, 45)
    $text.ForeColor = [Drawing.Color]::LightGreen
    $text.Location = New-Object Drawing.Point(24, 124)
    $text.Size = New-Object Drawing.Size(556, 115)
    $form.Controls.Add($text)

    $registerInfo = New-Object Windows.Forms.Label
    $registerInfo.Text = "Si aun no tienes serial key, registrate en el sitio oficial de Arkaios Expo y obten tu licencia personal para uso libre del software, sin pagos de membresia."
    $registerInfo.AutoSize = $false
    $registerInfo.Size = New-Object Drawing.Size(556, 42)
    $registerInfo.Location = New-Object Drawing.Point(24, 252)
    $registerInfo.ForeColor = [Drawing.Color]::Gainsboro
    $form.Controls.Add($registerInfo)

    $register = New-Object Windows.Forms.LinkLabel
    $register.Text = "Registrate aqui y obten tu licencia personal gratuita"
    $register.AutoSize = $true
    $register.Location = New-Object Drawing.Point(24, 302)
    $register.LinkColor = [Drawing.Color]::DeepSkyBlue
    $register.ActiveLinkColor = [Drawing.Color]::LightSkyBlue
    $register.Add_Click({
        try {
            Start-Process $RegistrationUrl
        } catch {
            [Windows.Forms.MessageBox]::Show("No pude abrir el enlace automaticamente.`n`n$RegistrationUrl", "Abrir registro", "OK", "Information") | Out-Null
        }
    })
    $form.Controls.Add($register)

    $install = New-Object Windows.Forms.Button
    $install.Text = "Validar e instalar"
    $install.Location = New-Object Drawing.Point(220, 350)
    $install.Size = New-Object Drawing.Size(170, 38)
    $install.BackColor = [Drawing.Color]::FromArgb(0, 150, 100)
    $install.ForeColor = [Drawing.Color]::White
    $install.FlatStyle = "Flat"
    $form.Controls.Add($install)

    $status = New-Object Windows.Forms.Label
    $status.Text = "La validacion local siempre se ejecuta. La validacion de servidor usa $ApiBase."
    $status.AutoSize = $false
    $status.Size = New-Object Drawing.Size(556, 46)
    $status.Location = New-Object Drawing.Point(24, 406)
    $status.ForeColor = [Drawing.Color]::Silver
    $form.Controls.Add($status)

    $form.Tag = $null
    $install.Add_Click({
        try {
            $key = $text.Text.Trim()
            $license = Test-LicenseLocal $key
            if (-not $SkipServerValidation) {
                Test-LicenseServer $key $license.Hwid | Out-Null
            }
            $form.Tag = @{ Key = $key; License = $license }
            $form.DialogResult = [Windows.Forms.DialogResult]::OK
            $form.Close()
        } catch {
            [Windows.Forms.MessageBox]::Show($_.Exception.Message, "Licencia invalida", "OK", "Error") | Out-Null
        }
    })

    if ($form.ShowDialog() -ne [Windows.Forms.DialogResult]::OK) {
        throw "Instalacion cancelada."
    }
    return $form.Tag
}

function Install-ArkaiosDJ([string]$Key) {
    New-Item -ItemType Directory -Force -Path $InstallDir | Out-Null
    New-Item -ItemType Directory -Force -Path $LicenseDir | Out-Null

    foreach ($file in @("ArkaiosDJ.exe", "yt-dlp.exe", "config.txt")) {
        $source = Join-Path $SourceDir $file
        if (Test-Path $source) {
            Copy-Item -LiteralPath $source -Destination (Join-Path $InstallDir $file) -Force
        }
    }

    Set-Content -LiteralPath $LicensePath -Value $Key -Encoding UTF8

    $shortcutPath = Join-Path ([Environment]::GetFolderPath("Desktop")) "Arkaios DJ Nexus.lnk"
    $shell = New-Object -ComObject WScript.Shell
    $shortcut = $shell.CreateShortcut($shortcutPath)
    $shortcut.TargetPath = Join-Path $InstallDir "ArkaiosDJ.exe"
    $shortcut.WorkingDirectory = $InstallDir
    $shortcut.IconLocation = Join-Path $InstallDir "ArkaiosDJ.exe"
    $shortcut.Save()

    return [PSCustomObject]@{
        InstallDir = $InstallDir
        LicensePath = $LicensePath
        Shortcut = $shortcutPath
    }
}

$result = Show-LicenseDialog
$installResult = Install-ArkaiosDJ $result.Key

[Windows.Forms.MessageBox]::Show(
    "Arkaios DJ Nexus fue instalado correctamente.`n`nCarpeta: $($installResult.InstallDir)`nLicencia: $($installResult.LicensePath)",
    "Instalacion completa",
    "OK",
    "Information"
) | Out-Null
