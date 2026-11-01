@echo off
rem Cift tikla: paneli baslatir ve tarayicida acar. Kapatmak icin bu pencereyi kapatin.
where node >nul 2>nul
if errorlevel 1 (
  echo Node.js bulunamadi. https://nodejs.org adresinden LTS surumunu kurun.
  pause
  exit /b 1
)
node "%~dp0server.mjs"
pause
