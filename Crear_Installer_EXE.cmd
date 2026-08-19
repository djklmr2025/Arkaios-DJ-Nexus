@echo off
setlocal
cd /d "%~dp0"
set "ISCC=%ProgramFiles(x86)%\Inno Setup 6\ISCC.exe"
if not exist "%ISCC%" set "ISCC=%ProgramFiles%\Inno Setup 6\ISCC.exe"
if not exist "%ISCC%" set "ISCC=%LocalAppData%\Programs\Inno Setup 6\ISCC.exe"
if not exist "%ISCC%" (
  echo ERROR: Inno Setup 6 no esta instalado.
  exit /b 3
)
if not exist "installer\payload" mkdir "installer\payload"
if exist "ArkaiosDJ.exe" copy /y "ArkaiosDJ.exe" "installer\payload\ArkaiosDJ.exe"
if exist "yt-dlp.exe" copy /y "yt-dlp.exe" "installer\payload\yt-dlp.exe"
if exist "config.txt" copy /y "config.txt" "installer\payload\config.txt"

if not exist "dist" mkdir "dist"
"%ISCC%" "installer\ArkaiosDJNexus.iss"
if errorlevel 1 exit /b %errorlevel%
if not exist "dist\ArkaiosDJ_Nexus_Setup.exe" exit /b 2
echo Instalador EXE creado:
echo %CD%\dist\ArkaiosDJ_Nexus_Setup.exe
endlocal
