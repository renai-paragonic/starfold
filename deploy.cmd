@echo off
rem Test, build, push and deploy Starfold: see deploy.ps1. "deploy.cmd -Yes" skips the questions.
rem It runs deploy.ps1 with the execution policy bypassed for this run only.
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0deploy.ps1" %*
set code=%ERRORLEVEL%
rem Run with no arguments (as from a double-click), keep the window open to read the result.
if "%~1"=="" pause
exit /b %code%
