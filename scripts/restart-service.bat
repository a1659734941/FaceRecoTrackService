@echo off
chcp 65001 >nul
setlocal

set SERVICE_NAME=FaceTrackService
sc stop "%SERVICE_NAME%"
timeout /t 3 >nul
sc start "%SERVICE_NAME%"
pause
