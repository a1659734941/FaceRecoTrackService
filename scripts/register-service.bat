@echo off
chcp 65001 >nul
setlocal

set SERVICE_NAME=FaceTrackService
set SERVICE_DISPLAY_NAME=FaceTrackService
set SCRIPT_DIR=%~dp0
set SERVICE_EXE=%SCRIPT_DIR%..\FaceTrackService.exe
for %%I in ("%SERVICE_EXE%") do set SERVICE_EXE=%%~fI

if not exist "%SERVICE_EXE%" (
    echo [错误] 未找到可执行文件: %SERVICE_EXE%
    pause
    exit /b 1
)

sc query "%SERVICE_NAME%" >nul 2>&1
if %errorlevel%==0 (
    echo [提示] 服务已存在: %SERVICE_NAME%
    pause
    exit /b 0
)

sc create "%SERVICE_NAME%" binPath= "\"%SERVICE_EXE%\"" start= auto DisplayName= "%SERVICE_DISPLAY_NAME%"
if errorlevel 1 (
    echo [错误] 服务注册失败
    pause
    exit /b 1
)

sc description "%SERVICE_NAME%" "FaceTrackService Windows Service"
echo [成功] 服务注册完成: %SERVICE_NAME%
pause
