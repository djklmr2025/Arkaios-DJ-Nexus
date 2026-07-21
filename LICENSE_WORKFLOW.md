# Arkaios DJ Nexus License Workflow

The desktop app currently uses a hybrid license flow:

1. Validate the key locally with the built-in signature format.
2. Try `http://localhost:3000/api/licenses/validate`.
3. Fall back to `https://servidor-arkaios-api.vercel.app/api/licenses/validate`.
4. If both servers are unavailable, local validation still allows offline use.

## License Types

- `BASIC`: tied to the machine HWID.
- `LIFETIME`: universal, no local expiration and eligible for update checks.

## Local Server

Start the local license API:

```bat
Iniciar_Servidor_Licencias_Local.cmd
```

The server stores local registrations in:

```text
C:\ARKAIOS\Servidor_Arkaios_API\licenses.json
```

## Generate Keys

Generate and register a basic key:

```bat
Generar_Licencia_Basica.cmd
```

Generate, register, validate, and install the owner lifetime key:

```bat
Registrar_Mi_Licencia_Vitalicia.cmd
```

The installed app license is written to:

```text
%APPDATA%\ArkaiosDJNexus\license.key
```

## Installer Package

Create the distributable installer package:

```bat
Crear_Paquete_Instalador.cmd
```

The package includes:

- `installer/Install-ArkaiosDJ.ps1`
- `ArkaiosDJ.exe`
- `yt-dlp.exe`
- `config.txt`
- `LICENSE_WORKFLOW.md`

Run the installer from the extracted package:

```powershell
powershell -ExecutionPolicy Bypass -File installer\Install-ArkaiosDJ.ps1
```

The installer asks for a serial key first. If the key is invalid, revoked, or
does not match the HWID for a `BASIC` license, installation stops before copying
the application.

If the user does not have a serial key yet, the installer shows the official
Firebase registration site:

```text
https://arkaios-world.web.app/
```

That link is the intended public path for free personal registration and serial
generation. It can be overridden for staging with:

```powershell
$env:ARKAIOS_DJ_REGISTRATION_URL = "https://arkaios-world.web.app/"
```

## Direct CLI

```powershell
node tools\dj-license-tool.mjs hwid
node tools\dj-license-tool.mjs generate --type=BASIC --name="Human User" --phone="" --register --validate-server
node tools\dj-license-tool.mjs generate --type=LIFETIME --name="DJ KLMR / Arkaios Owner" --register --validate-server --install-local
```

## Expo-Arkaios Registration

The current desktop license does not require Expo-Arkaios identity yet. Expo can become the public account layer later:

- User signs in to Expo-Arkaios.
- Expo calls the license API to generate a `BASIC` key for that account.
- The desktop app validates the key using the same server endpoint.
