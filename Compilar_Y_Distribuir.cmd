@echo off
setlocal EnableDelayedExpansion
cd /d "%~dp0"
title Arkaios DJ - Compilar y Distribuir v1.3

echo.
echo ===================================================
echo   ARKAIOS DJ NEXUS - Compilar y Distribuir
echo ===================================================
echo.

:: ---- Buscar dotnet con SDK ----
set "DOTNET="
for %%p in (
    "%USERPROFILE%\dotnet\dotnet.exe"
    "%ProgramFiles%\dotnet\dotnet.exe"
    "%ProgramFiles(x86)%\dotnet\dotnet.exe"
    "%LOCALAPPDATA%\Microsoft\dotnet\dotnet.exe"
) do (
    if exist "%%~p" (
        "%%~p" build --version >nul 2>&1
        if !ERRORLEVEL! EQU 0 (
            set "DOTNET=%%~p"
            goto :found_dotnet
        )
    )
)

:: Buscar en PATH
for /f "tokens=*" %%i in ('where dotnet 2^>nul') do (
    "%%i" build --version >nul 2>&1
    if !ERRORLEVEL! EQU 0 (
        set "DOTNET=%%i"
        goto :found_dotnet
    )
)

echo [ERROR] No se encontro dotnet SDK con capacidad de compilacion.
echo.
echo Para compilar este proyecto necesitas el SDK de .NET 8:
echo   https://dotnet.microsoft.com/download/dotnet/8.0
echo.
echo ALTERNATIVA: El instalador actual ya incluye yt-dlp.exe
echo Si no necesitas recompilar, ejecuta: Crear_Installer_EXE.cmd
echo.
pause
exit /b 1

:found_dotnet
echo [OK] SDK de .NET encontrado: %DOTNET%
"%DOTNET%" --version
echo.

:: ---- Compilar ----
echo Compilando ArkaiosDJAssistant...
"%DOTNET%" build "%~dp0ArkaiosDJAssistant.csproj" -c Release -o "%~dp0bin\Release" 2>&1
if %ERRORLEVEL% NEQ 0 (
    echo.
    echo [ERROR] La compilacion fallo. Revisa los mensajes de arriba.
    pause
    exit /b %ERRORLEVEL%
)

echo.
echo [OK] Compilacion exitosa.

:: ---- Copiar binarios al raiz del proyecto ----
if exist "%~dp0bin\Release\ArkaiosDJ.exe" (
    xcopy /y /e /i /q "%~dp0bin\Release\*" "%~dp0"
    echo [OK] Binarios y dependencias actualizadas en raiz del proyecto.
)

:: ---- Sincronizar payload del instalador ----
if not exist "%~dp0installer\payload" mkdir "%~dp0installer\payload"
if exist "%~dp0bin\Release\ArkaiosDJ.exe" (
    xcopy /y /e /i /q "%~dp0bin\Release\*" "%~dp0installer\payload\"
)
if exist "%~dp0yt-dlp.exe" copy /y "%~dp0yt-dlp.exe" "%~dp0installer\payload\yt-dlp.exe"
if exist "%~dp0config.txt" copy /y "%~dp0config.txt" "%~dp0installer\payload\config.txt"
echo [OK] Payload completo del instalador sincronizado con todas las DLLs y dependencias.

:: ---- Crear instalador ----
set "ISCC=%ProgramFiles(x86)%\Inno Setup 6\ISCC.exe"
if not exist "%ISCC%" set "ISCC=%ProgramFiles%\Inno Setup 6\ISCC.exe"
if not exist "%ISCC%" set "ISCC=%LocalAppData%\Programs\Inno Setup 6\ISCC.exe"

if not exist "%ISCC%" (
    echo.
    echo [AVISO] Inno Setup 6 no encontrado. Saltando generacion de instalador .exe.
    echo Descarga Inno Setup en: https://jrsoftware.org/isinfo.php
    echo Los archivos compilados estan listos en: %~dp0installer\payload\
    goto :done
)

if not exist "%~dp0dist" mkdir "%~dp0dist"
echo.
echo Generando instalador con Inno Setup...
"%ISCC%" "%~dp0installer\ArkaiosDJNexus.iss"
if %ERRORLEVEL% NEQ 0 (
    echo [ERROR] El instalador no se pudo crear.
    pause
    exit /b %ERRORLEVEL%
)

echo.
echo [OK] Instalador creado:
echo %~dp0dist\ArkaiosDJ_Nexus_Setup.exe

:done
echo.
echo ===================================================
echo   PROCESO COMPLETADO
echo ===================================================
echo.
pause
endlocal
