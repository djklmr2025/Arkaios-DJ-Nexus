# ARKAIOS DJ Assistant - Release final local

Fecha de preparacion: 2026-07-20

## Estado

Build final para release instalable. Artefactos actuales:

- `ArkaiosDJ.exe`
- `dist\ArkaiosDJ_Nexus_Setup.exe`
- Sistema base: Windows / .NET Framework WinForms
- Integracion anfitriona: VirtualDJ 8 mediante `database.xml`, historial y drag & drop de archivos locales

## Modo simple por defecto

La interfaz principal queda reducida a dos pestanas operativas:

1. `Auto Help + Camelot`
2. `Buscar All Tracks`

La opcion `Settings -> Opciones -> Modo avanzado` muestra las pestanas tecnicas:

- `Buscar y descargar`
- `Hits / Plataformas`
- `Descargas / Hub local`
- `Organizador IA / Renombrador`

En `Settings -> Opciones` tambien queda el selector/reconocedor de salida de
audio para preview local. El motor liviano puede seguir usando la salida default
de Windows si MCI/WMP no permite ruteo directo, pero la preferencia queda
guardada en `config.txt` como `preview_audio_device`.

## Auto Help + Camelot

- Lee la metadata de VirtualDJ.
- Detecta pista actual.
- Recomienda tracks compatibles por BPM, tonalidad Camelot, historial y artista.
- Permite arrastrar archivos reales al plato de VirtualDJ.
- Muestra descargas recientes al final bajo separador propio.
- Si VirtualDJ tiene metadata de una recomendacion pero el archivo fisico falta, la fila aparece como `FALTA`.
- En una fila `FALTA`, doble clic permite descargar:
  - audio MP3 al Hub Music;
  - video MP4 al Hub Video.
- La seleccion usa botones claros `Audio MP3`, `Video MP4` y `Cancelar`.
- Si el formato elegido no se encuentra o no se descarga, el sistema ofrece
  intentar el formato alterno antes de dejar la pista como `FALTA`.
- Al completar descarga:
  - registra en `downloaded-hub-tracks.log`;
  - agrega a descargas recientes;
  - deja la pista lista para arrastrar a VirtualDJ.

## Buscar All Tracks

- No precarga archivos al abrir.
- Busca bajo demanda en carpetas permitidas del usuario y hubs:
  - Music
  - Video
  - Karaoke
- Usa busqueda normalizada:
  - ignora acentos;
  - tolera signos/guiones;
  - prioriza coincidencias exactas;
  - despues coincidencias por palabra;
  - despues coincidencias parciales.
- Muestra primero los resultados locales en bloque.
- Si esta activo `Internet`, despues consulta YouTube y agrega hasta 20 candidatos descargables en un bloque separado.
- Los resultados online muestran formato posible, duracion, calidad maxima detectada y tamano estimado cuando `yt-dlp` lo entrega.
- Por duracion etiqueta posibles `track corto`, `track normal`, `extended/remix probable` o `mix largo`.
- Permite:
  - prescuchar archivos locales;
  - descargar resultados online al Hub;
  - encontrar archivo en Explorer;
  - copiar ubicacion local o URL;
  - arrastrar archivos locales a VirtualDJ.

## Hub local y persistencia

- Las descargas reales se registran en `downloaded-hub-tracks.log`.
- El Hub separa descargas de internet y archivos locales.
- Las descargas de internet aparecen arriba, en verde, con fecha `Obtenido`.
- El registro local esta ignorado por Git para no subir historial del usuario.

## Descargas

- Motor real: `yt-dlp.exe`.
- Salidas soportadas:
  - MP3 320 kbps / 192 kbps;
  - M4A cuando se conserva el mejor audio disponible;
  - MP4 para video.
- Rutas usadas para Hub:
  - `C:\Users\djklm\Music`
  - `C:\Users\djklm\Videos`
  - `C:\Users\djklm\Videos\KARAOKES`

## Renombrador y metadata

- Renombrado automatico con validacion previa.
- Renombrado manual por archivo.
- Historial de renombrado para deshacer lote.
- Puente opcional a `tageditor-cli.exe`, solo si existe una version compatible con Windows x64/x86.
- MusicBrainz y YouTube actuan como fallback de metadata publica.
- Mp3tag no se usa como backend silencioso porque sus comandos oficiales cargan GUI y no ejecutan tagging/renombrado en segundo plano.

## Seguridad

No deben subirse al release ni al repositorio:

- `youtube-cookies.txt`
- `cookies.txt`
- `*.cookies.txt`
- `downloaded-hub-tracks.log`
- `yt-dlp-errors.log`
- claves/licencias locales

Estos archivos ya estan contemplados en `.gitignore`.

## Instalable oficial

- Instalador wizard creado con Inno Setup:
  - `dist\ArkaiosDJ_Nexus_Setup.exe`
- Incluye:
  - `ArkaiosDJ.exe`
  - `yt-dlp.exe`
  - `config.txt` base sanitizado
- No incluye cookies, logs, licencias locales ni historial del usuario.

## Pendiente posterior al release

- Integrar licencia por equipo/HWID.
- Documentar flujo de activacion.
- Evaluar instalacion limpia en un equipo sin configuracion previa.

Evaluar integracion avanzada con VirtualDJ mediante repositorios tipo `leadedge/SpoutVDJ` para determinar si ARKAIOS puede evolucionar de app externa a addon/plugin real.
