# FIX: Arkaios DJ Nexus no lee la base de datos / pistas reales de VirtualDJ en otras PCs

> Documento para un **agente local** (Claude Code, Antigravity, etc.).
> Repo: https://github.com/djklmr2025/Arkaios-DJ-Nexus (público)
> Proyecto: `ArkaiosDJAssistant.csproj`, `net8.0-windows`, WinForms (.NET 8 Desktop Runtime).
> Archivos implicados: `AppSettings.cs`, `Program.cs` (`LoadDatabase`, `CheckHistory`, `UpdateRecommendations`), `HistoryEngine.cs`, `SetupForm.cs`, `config.txt`.

**Regla para el agente:** NO escribas claves, tokens ni contraseñas en ningún archivo del repo ni en logs. Si aparece alguna en el entorno, ignórala.

---

## 1. Síntoma

- En la PC del desarrollador (usuario `djklm`) todo funciona: carga `database.xml`, ve las pistas, recomienda con Camelot y detecta los "Ghost Tracks".
- En otras PCs la app abre, pero no detecta las pistas ni la base de datos real del usuario (lista vacía, "No se encontró metadata en DB para: ...", o usa una BD equivocada/vacía).

## 2. Diagnóstico (análisis estático del código; confirmar con la sección 5)

Ordenado por probabilidad.

### Causa 1 (la más probable): el filtro `AllowedFolders` descarta casi todas las pistas

En `Program.cs > LoadDatabase`, cada `<Song FilePath="...">` solo se agrega si:

```csharp
bool allowed = AppSettings.AllowedFolders.Count == 0;
if (!allowed) { foreach (var f in AppSettings.AllowedFolders)
    if (currentTrack.FilePath.StartsWith(f, OrdinalIgnoreCase)) { allowed = true; break; } }
```

Pero `AppSettings.EnsureVirtualDjIntegration()` (se llama en `Main`, y otra vez en `GetDownloadFolder`) SIEMPRE agrega `C:\ARKAIOS\Biblioteca_DJ` y sus subcarpetas a `AllowedFolders`, y `config.txt` trae `%USERPROFILE%\Music` y `%USERPROFILE%\Videos`. Por lo tanto `Count` nunca es 0 y el filtro siempre está activo.

- En la PC del desarrollador la música vive en `C:\Users\djklm\Music` → pasa el filtro.
- En otra PC la música suele estar en `D:\`, `E:\`, una carpeta propia, un NAS o un disco externo → **0 pistas cargadas**, aunque `database.xml` sea correcto.
- Consecuencia en cadena: `UpdateRecommendations` no encuentra la pista que suena en `allTracks` y muestra "No se encontró metadata en DB".

### Causa 2: la autodetección de `database.xml` toma el primer candidato, no el correcto

`AutoDetectVirtualDjPaths` recorre `GetVirtualDjBaseCandidates()` y se queda con el **primero** que tenga `database.xml`, sin comparar tamaño, fecha ni número de canciones. Además:

- Si `config.txt` ya apunta a un `database.xml` que existe (aunque sea uno viejo, vacío o de otra instalación), `Load()` no fuerza redetección (solo fuerza si falta el archivo/carpeta o si la ruta contiene `\Users\djklm`).
- No se consulta dónde guarda realmente VirtualDJ su carpeta de usuario (Home Folder configurable; hay que **verificar** en una PC real si queda en el registro, p. ej. `HKCU\Software\VirtualDJ`) ni las bases por unidad que VirtualDJ crea en otros discos (**verificar** nombre exacto en una PC real, p. ej. `X:\VirtualDJ\VirtualDJ Local Database v6.xml`).
- Si el usuario movió su carpeta VirtualDJ fuera de Documentos/OneDrive, no hay candidato.

### Causa 3: `config.txt` junto al `.exe` (`AppDomain.CurrentDomain.BaseDirectory`)

- `Save()` escribe en la carpeta de instalación. Si se instala en `Program Files` o la carpeta es de solo lectura, el guardado falla (no hay `try/catch`) y la configuración nunca persiste.
- El `.csproj` marca `config.txt` como `CopyToOutputDirectory=Always`: una actualización/instalador puede sobrescribir la configuración del usuario con la del desarrollador.

### Causa 4: historial (`HistoryEngine`) y errores silenciosos

- `HistoryEngine.ScanHistory` solo lee `*.m3u`. En `Playlists\` VirtualDJ guarda normalmente `.vdjfolder`/`.m3u8` y otros formatos, así que en otras PCs el motor de transiciones históricas queda vacío (no rompe la lectura de la BD, pero reduce las "pistas" sugeridas). **Verificar** con archivos reales.
- Hay muchos `catch { }` vacíos (`CheckHistory`, `ScanDirectory`, `ParseM3U`, `EnsureVirtualDjIntegration`, `WriteVdjFolderXml`). Cuando falla algo en otra PC no queda ninguna pista de por qué.

### Causa 5 (descartar): requisitos de entorno

- El proyecto es `net8.0-windows` y necesita **.NET 8 Desktop Runtime** si no se publicó autocontenido. Si faltara, la app ni abriría, así que no explica el síntoma, pero conviene confirmarlo.
- Rutas fijas `C:\ARKAIOS\Biblioteca_DJ` en `GetDownloadFolder`/`EnsureVirtualDjIntegration`. Si no se puede crear, el `catch {}` lo oculta.

---

## 3. Plan de arreglo (en este orden)

### 3.1 Logging de diagnóstico (primero, para poder ver qué pasa en la PC ajena)

Crear `DiagLog.cs` que escriba en `%LOCALAPPDATA%\ArkaiosDJ\diagnostico.log` (con try/catch propio) y reemplazar los `catch { }` relevantes por `catch (Exception ex) { DiagLog.Write(ex); }`.

Registrar al arrancar:

- `Environment.UserName`, `Environment.OSVersion`, versión de runtime.
- `Environment.GetFolderPath(MyDocuments)`, `UserProfile`, `ApplicationData`.
- Por cada candidato de `GetVirtualDjBaseCandidates()`: existe carpeta, existe `database.xml`, tamaño, `LastWriteTime`, número de `<Song`, existe `History\`, cuántos `.m3u`.
- Ruta final elegida y **por qué**.
- En `LoadDatabase`: canciones leídas, canciones descartadas por filtro, canciones cuyo archivo existe en disco, y las 5 primeras letras de unidad de los `FilePath` (`C:`, `D:`...).
- En `CheckHistory`: archivo `.m3u` más reciente y la última ruta leída.

### 3.2 Quitar el filtro `AllowedFolders` de la carga de la BD (arreglo de la Causa 1)

- `LoadDatabase` debe cargar **todas** las canciones con `FilePath` no vacío.
- `AllowedFolders` pasa a ser solo el **alcance de búsqueda local** (`Buscar All Tracks`, Hub), no un filtro de la base.
- `EnsureVirtualDjIntegration` NO debe agregar carpetas a `AllowedFolders` si el usuario no las eligió; y las comparaciones de prefijo deben normalizar con separador final para evitar falsos positivos (`C:\Music2` vs `C:\Music`):

```csharp
static bool IsUnder(string path, string folder)
{
    if (string.IsNullOrWhiteSpace(path) || string.IsNullOrWhiteSpace(folder)) return false;
    string f = folder.TrimEnd('\\', '/') + Path.DirectorySeparatorChar;
    return path.StartsWith(f, StringComparison.OrdinalIgnoreCase);
}
```

- Opcional: agregar a `Track` una propiedad `InAllowedFolders` solo informativa.

### 3.3 Autodetección robusta de `database.xml` (Causa 2)

Reescribir `AutoDetectVirtualDjPaths` así:

1. Reunir TODOS los candidatos con `database.xml` (Documentos, OneDrive en/es, `%USERPROFILE%\VirtualDJ`, LocalAppData, AppData, y el Home Folder de VirtualDJ si se confirma en registro/`settings.xml`).
2. Elegir el mejor por: más canciones (contar `<Song`) y, como desempate, `LastWriteTime` más reciente. Evitar archivos de menos de ~1 KB (BD vacía).
3. Aplicar redetección también cuando la BD configurada exista pero tenga 0 canciones.
4. Si hay varios candidatos válidos y distintos, mostrar en `SetupForm` un selector con tamaño/fecha/nº de canciones para que el usuario decida.
5. Dejar siempre un botón "Elegir database.xml manualmente" y recordar la elección.
6. Valorar escanear la raíz de cada unidad fija (`DriveInfo.GetDrives()`, `DriveType.Fixed`) buscando la base local de VirtualDJ y fusionar canciones (verificar formato real antes de implementarlo).

### 3.4 Configuración por usuario, no junto al `.exe` (Causa 3)

- Mover `config.txt` a `%APPDATA%\ArkaiosDJ\config.txt`.
- Migración: si existe el antiguo junto al `.exe` y el nuevo no, copiarlo una vez.
- `Save()` con `try/catch` y mensaje visible si falla.
- Quitar `CopyToOutputDirectory=Always` de `config.txt` (dejar `PreserveNewest` solo como plantilla `config.default.txt`).
- Mantener la limpieza de rutas ajenas, pero generalizada: si una ruta apunta a `\Users\<otro usuario>\` distinto de `Environment.UserName`, forzar redetección (hoy solo está hardcodeado `djklm`).

### 3.5 Historial (Causa 4)

- Hacer que `ScanHistory` tolere `.m3u`, `.m3u8` y, tras verificar con muestras reales, `.vdjfolder` (XML con rutas de canciones).
- Normalizar rutas al comparar (`Path.GetFullPath` con try/catch) y mantener el respaldo por nombre de archivo ya existente.
- En `CheckHistory`, ignorar líneas que no parezcan rutas y registrar en el log cuando la ruta leída no esté en `allTracks`.

### 3.6 Mensajes claros en la UI

- Si `allTracks.Count == 0`: mostrar la ruta de la BD, cuántas canciones tenía el XML y cuántas se descartaron (con el motivo), y un botón para abrir `diagnostico.log`.
- En `Error: database.xml no encontrado en ...` ofrecer abrir `SetupForm`.

---

## 4. Criterios de aceptación

- [ ] En una PC con otro usuario, música en otra unidad (`D:\`) y VirtualDJ en `Documents\VirtualDJ`: carga todas las pistas y las recomendaciones Camelot funcionan.
- [ ] Con Documentos redirigido a OneDrive: detecta la BD correcta.
- [ ] Con la carpeta de VirtualDJ en una ruta personalizada: el selector manual la guarda y persiste tras reiniciar.
- [ ] Instalada en `Program Files` (sin admin): la configuración se guarda y persiste.
- [ ] Con BD vacía o antigua en un candidato y la buena en otro: elige la buena.
- [ ] `diagnostico.log` se genera y explica la ruta elegida, nº de canciones y descartes.
- [ ] Ningún secreto en repo ni logs.

## 5. Script para confirmar la causa en la PC con el problema

Ejecutar en PowerShell en el equipo donde falla y pegar la salida (no contiene claves):

```powershell
$docs = [Environment]::GetFolderPath('MyDocuments')
"Usuario: $env:USERNAME | Documentos: $docs"
$cands = @("$docs\VirtualDJ","$env:USERPROFILE\Documents\VirtualDJ","$env:USERPROFILE\OneDrive\Documents\VirtualDJ","$env:USERPROFILE\OneDrive\Documentos\VirtualDJ","$env:USERPROFILE\VirtualDJ","$env:LOCALAPPDATA\VirtualDJ","$env:APPDATA\VirtualDJ") | Select-Object -Unique
foreach ($c in $cands) {
  $db = Join-Path $c 'database.xml'
  if (Test-Path $db) {
    $n = (Select-String -Path $db -Pattern '<Song ' -SimpleMatch | Measure-Object).Count
    $i = Get-Item $db
    "{0} | {1} KB | {2} | canciones: {3}" -f $db, [int]($i.Length/1KB), $i.LastWriteTime, $n
    "  History m3u: " + (Get-ChildItem (Join-Path $c 'History') -Filter *.m3u -ErrorAction SilentlyContinue | Measure-Object).Count
    "  Letras de unidad en FilePath: " + ((Select-String -Path $db -Pattern 'FilePath="([A-Za-z]):' | ForEach-Object { $_.Matches[0].Groups[1].Value.ToUpper() } | Sort-Object -Unique) -join ',')
  } else { "$db -> NO existe" }
}
Get-Content "$env:APPDATA\..\Local\ArkaiosDJ\diagnostico.log" -ErrorAction SilentlyContinue
```

Lectura del resultado:

- Si `database.xml` tiene canciones con unidades distintas a las de `config.txt` (`%USERPROFILE%\Music`, `Videos`, `C:\ARKAIOS\...`) → confirma la **Causa 1**.
- Si hay varios `database.xml` con tamaños/fechas muy distintos → confirma la **Causa 2**.
- Si no existe ninguno → VirtualDJ guarda en otra ruta; hay que localizar el Home Folder (Causa 2, punto de registro).

## 6. Nota de seguridad (para quien mantiene el repo)

- Este repo es **público** e incluye `lifetime key.txt`, `config.txt`, binarios y artefactos de licencia. Revisar que no expongan claves válidas o lógica de licencias reutilizable.
- No subir `.env`, tokens, ni archivos de claves. Usar variables de entorno y rotar cualquier credencial que haya estado en texto plano.

## 7. Limitaciones de este análisis

- Se revisó el código fuente (`AppSettings.cs`, `Program.cs`, `HistoryEngine.cs`, `config.txt`, `.csproj`) de la rama `main`; no se ejecutó la app ni se vio una `database.xml` real de otra PC.
- Los binarios del repo (`ArkaiosDJ.exe/.dll`) podrían no coincidir exactamente con el código fuente.
- Los puntos marcados "verificar" (registro de VirtualDJ, bases por unidad, formato de `.vdjfolder`) requieren confirmarse en una instalación real de VirtualDJ.
