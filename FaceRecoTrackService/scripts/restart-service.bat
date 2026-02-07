@echo off
chcp 65001 >nul
setlocal

set SERVICE_NAME=FaceTrackService

echo ========================================
echo    FaceTrackService 重启服务
echo ========================================
echo 服务名称: %SERVICE_NAME%
echo.

echo [进行中] 正在停止服务...
sc stop "%SERVICE_NAME%" >nul
timeout /t 3 >nul

echo [进行中] 正在启动服务...
sc start "%SERVICE_NAME%"
if errorlevel 1 (
    echo [失败] 启动服务失败
    echo 可能原因:
    echo   1. 服务未注册
    echo   2. 需要管理员权限
    echo.
) else (
    echo [成功] 服务已启动
)
pause
