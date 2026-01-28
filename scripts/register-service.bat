@echo off
chcp 65001 >nul
setlocal

set SERVICE_NAME=FaceTrackService
set SERVICE_DISPLAY_NAME=FaceTrackService
set SCRIPT_DIR=%~dp0
set SERVICE_EXE=%SCRIPT_DIR%..\FaceTrackService.exe
for %%I in ("%SERVICE_EXE%") do set SERVICE_EXE=%%~fI

echo ========================================
echo    FaceTrackService 服务注册脚本
echo ========================================
echo 服务名称: %SERVICE_NAME%
echo 显示名称: %SERVICE_DISPLAY_NAME%
echo 可执行文件: %SERVICE_EXE%
echo.

if not exist "%SERVICE_EXE%" (
    echo [失败] 未找到可执行文件
    echo 请确认 FaceTrackService.exe 与 scripts 同级
    pause
    exit /b 1
)

sc query "%SERVICE_NAME%" >nul 2>&1
if %errorlevel%==0 (
    echo [提示] 服务已存在: %SERVICE_NAME%
    echo 如需重新注册，请先删除服务:
    echo   sc delete "%SERVICE_NAME%"
    pause
    exit /b 0
)

echo [进行中] 正在注册服务...
sc create "%SERVICE_NAME%" binPath= "\"%SERVICE_EXE%\"" start= auto DisplayName= "%SERVICE_DISPLAY_NAME%"
if errorlevel 1 (
    echo [失败] 服务注册失败
    pause
    exit /b 1
)

sc description "%SERVICE_NAME%" "FaceTrackService Windows Service"
echo [成功] 服务注册完成
echo.
echo 你可以运行以下命令查看状态:
echo   service-status.bat
pause
