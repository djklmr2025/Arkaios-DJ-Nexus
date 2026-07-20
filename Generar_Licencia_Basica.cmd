@echo off
cd /d "%~dp0"
node tools\dj-license-tool.mjs generate --type=BASIC --name="Arkaios User" --phone="" --register --validate-server
