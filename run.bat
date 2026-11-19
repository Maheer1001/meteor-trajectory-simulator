@echo off
rem Double-click to build (first time) and play the Meteor Trajectory Simulator. Options are passed to run.ps1,
rem e.g.  run.bat -Rebuild   or   run.bat -Editor   or   run.bat -Windowed
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0run.ps1" %*
if errorlevel 1 pause
