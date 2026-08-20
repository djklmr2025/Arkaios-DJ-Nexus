@echo off
title Configurar Ruta Predeterminada de aTube Catcher - ARKAIOS DJ NEXUS
color 0A
echo =========================================================================
echo  CONFIGURANDO RUTA DE DESCARGA DE aTube Catcher PARA ARKAIOS DJ NEXUS
echo =========================================================================
echo.
set "TARGET_DIR=C:\ARKAIOS\Biblioteca_DJ\Musica"
if not exist "%TARGET_DIR%" mkdir "%TARGET_DIR%"

reg add "HKCU\Software\DsNET Corp\aTube Catcher" /v "SaveDirectory" /t REG_SZ /d "%TARGET_DIR%" /f >nul 2>&1
reg add "HKCU\Software\aTube Catcher" /v "SaveDirectory" /t REG_SZ /d "%TARGET_DIR%" /f >nul 2>&1

echo [OK] Ruta predeterminada configurada exitosamente en:
echo      %TARGET_DIR%
echo.
echo Presione una tecla para cerrar...
pause >nul
