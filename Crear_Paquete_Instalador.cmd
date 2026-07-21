@echo off
setlocal
cd /d "%~dp0"
set "OUT=%CD%\dist"
set "PKG=%OUT%\ArkaiosDJ_Nexus_Installer_Package.zip"
if not exist "%OUT%" mkdir "%OUT%"
if exist "%PKG%" del "%PKG%"
powershell -NoProfile -ExecutionPolicy Bypass -Command "Compress-Archive -Path 'ArkaiosDJ.exe','yt-dlp.exe','config.txt','installer\ArkaiosDJNexus.iss' -DestinationPath '%PKG%' -Force"
echo Paquete creado:
echo %PKG%
endlocal
