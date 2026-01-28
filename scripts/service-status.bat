@echo off
chcp 65001 >nul
setlocal

set SERVICE_NAME=FaceTrackService
sc query "%SERVICE_NAME%"
pause
