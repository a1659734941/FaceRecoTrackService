@echo off
chcp 65001 >nul
setlocal

set SERVICE_NAME=FaceTrackService

echo ========================================
echo    FaceTrackService 服务状态查询
echo ========================================
echo 服务名称: %SERVICE_NAME%
echo.

sc query "%SERVICE_NAME%"
if errorlevel 1 (
    echo.
    echo [失败] 未找到服务或查询失败
    echo 可能原因:
    echo   1. 服务未注册
    echo   2. 需要管理员权限
    echo.
)
pause
