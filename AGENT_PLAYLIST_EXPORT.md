# ARKAIOS - Rutina de agente para exportar enlaces de playlist

Objetivo: validar una playlist y generar un `.txt` informativo con enlaces reales detectables, sin descargar audio ni video.

Comando base:

```powershell
powershell -ExecutionPolicy Bypass -File C:\ARKAIOS\DJ_Assistant\tools\ExportPlaylistLinks.ps1 -Url "URL_DE_PLAYLIST_O_VIDEO_CON_LIST"
```

Salida:

- Se crea un archivo en `C:\ARKAIOS\DJ_Assistant\exports\playlist-links-YYYYMMDD-HHMMSS.txt`.
- Columnas: `index`, `titulo`, `canal`, `duracion`, `tipo_descarga`, `url`.
- `tipo_descarga` queda como `audio/video` porque el archivo solo informa enlaces; el usuario decide luego si baja MP3 o MP4 desde ARKAIOS.

Reglas de seguridad:

- No leer ni imprimir `youtube-cookies.txt`.
- Si existe `youtube-cookies.txt`, el script lo usa por ruta para validar contenido privado.
- Si YouTube responde `403` o no entrega items, reportar el diagnóstico y pedir playlist pública/no listada o cookies válidas exportadas desde la cuenta con permiso.
- No descargar medios desde esta rutina.

Interpretación:

- Si aparecen muchas filas, la playlist fue extraída correctamente.
- Si aparece `NO SE PUDO EXTRAER LA PLAYLIST COMPLETA` y solo un `VIDEO ACTUAL DETECTADO`, el enlace `watch?v=...&list=...` permitió ver el video actual, pero no la lista completa.
- Si se mejora `yt-dlp.exe` o cambian las cookies, no hay que recompilar ARKAIOS para esta rutina; basta con volver a correr el script.
