@echo off
rem Double-click (or run from a terminal) to run all EditMode tests and print a summary. Options go to run-tests.ps1,
rem e.g.  run-tests.bat -Filter SetupChecks
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0run-tests.ps1" %*
set code=%errorlevel%
if not "%code%"=="0" pause
exit /b %code%
