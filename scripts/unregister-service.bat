@echo off
chcp 65001 >nul
setlocal

set SERVICE_NAME=FaceTrackService

echo ========================================
echo    FaceTrackService 服务删除脚本
echo ========================================
echo 服务名称: %SERVICE_NAME%
echo.

sc query "%SERVICE_NAME%" >nul 2>&1
if errorlevel 1 (
    echo [提示] 服务不存在: %SERVICE_NAME%
    pause
    exit /b 0
)

echo [进行中] 正在停止服务...
sc stop "%SERVICE_NAME%" >nul 2>&1
timeout /t 2 /nobreak >nul

echo [进行中] 正在删除服务...
sc delete "%SERVICE_NAME%"
if errorlevel 1 (
    echo [失败] 服务删除失败
    echo 请确保以管理员权限运行此脚本
    pause
    exit /b 1
)

echo [成功] 服务删除完成
echo.
pause
