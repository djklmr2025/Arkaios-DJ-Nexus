@echo off
setlocal
cd /d "%~dp0"
title Arkaios DJ - Parche yt-dlp

echo.
echo ===================================================
echo   PARCHE yt-dlp para Arkaios DJ Nexus
echo   Solucion rapida para equipos sin yt-dlp
echo ===================================================
echo.

:: Archivo fuente de yt-dlp (buscar en la carpeta del script primero)
set "SRC="
if exist "%~dp0yt-dlp.exe" set "SRC=%~dp0yt-dlp.exe"
if not defined SRC (
    echo [ERROR] No se encontro yt-dlp.exe en: %~dp0
    echo.
    echo Coloca yt-dlp.exe en la misma carpeta que este script y vuelve a ejecutarlo.
    pause
    exit /b 1
)

echo [OK] yt-dlp encontrado: %SRC%
echo.

:: Destinos donde puede estar instalada la app
set /a count=0

:: 1. Instalacion estandar (LocalAppData)
set "DEST1=%LOCALAPPDATA%\Programs\Arkaios DJ Nexus"
if exist "%DEST1%\ArkaiosDJ.exe" (
    echo Copiando a: %DEST1%
    copy /y "%SRC%" "%DEST1%\yt-dlp.exe" && (
        echo   [OK] Copiado exitosamente.
        set /a count+=1
    ) || echo   [ERROR] No se pudo copiar.
)

:: 2. Carpeta del ejecutable actual (si se ejecuta desde otra ruta)
set "DEST2=%~dp0"
if exist "%DEST2%ArkaiosDJ.exe" (
    if not "%DEST2%" == "%DEST1%\" (
        echo Copiando a: %DEST2%
        copy /y "%SRC%" "%DEST2%yt-dlp.exe" && (
            echo   [OK] Copiado exitosamente.
            set /a count+=1
        ) || echo   [ERROR] No se pudo copiar.
    )
)

:: 3. Buscar en carpetas comunes del sistema
for %%d in (
    "C:\ARKAIOS\DJ_Assistant"
    "C:\Program Files\Arkaios DJ Nexus"
    "C:\Program Files (x86)\Arkaios DJ Nexus"
    "%APPDATA%\ArkaiosDJNexus"
) do (
    if exist "%%~d\ArkaiosDJ.exe" (
        echo Copiando a: %%~d
        copy /y "%SRC%" "%%~d\yt-dlp.exe" && (
            echo   [OK] Copiado exitosamente.
            set /a count+=1
        ) || echo   [ERROR] No se pudo copiar.
    )
)

echo.
if %count% GTR 0 (
    echo ===================================================
    echo   PARCHE APLICADO EN %count% UBICACION(ES)
    echo   Ya puedes abrir Arkaios DJ Nexus y descargar musica.
    echo ===================================================
) else (
    echo ===================================================
    echo   [AVISO] No se encontro ninguna instalacion de
    echo   Arkaios DJ Nexus en el sistema.
    echo.
    echo   Opciones:
    echo   1. Reinstala la app usando el instalador oficial.
    echo   2. Copia yt-dlp.exe manualmente a la carpeta
    echo      donde este ArkaiosDJ.exe.
    echo ===================================================
)

echo.
pause
endlocal
