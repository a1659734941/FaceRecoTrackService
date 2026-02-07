@echo off
chcp 65001 >nul
setlocal

set SERVICE_NAME=FaceTrackService

echo ========================================
echo    FaceTrackService 停止服务
echo ========================================
echo 服务名称: %SERVICE_NAME%
echo.

echo [进行中] 正在停止服务...
sc stop "%SERVICE_NAME%"
if errorlevel 1 (
    echo [失败] 停止服务失败
    echo 可能原因:
    echo   1. 服务未注册
    echo   2. 服务未启动
    echo   3. 需要管理员权限
    echo.
) else (
    echo [成功] 已发送停止命令
)
pause
