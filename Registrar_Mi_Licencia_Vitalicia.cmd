@echo off
cd /d "%~dp0"
node tools\dj-license-tool.mjs generate --type=LIFETIME --name="DJ KLMR / Arkaios Owner" --phone="" --register --validate-server --install-local
