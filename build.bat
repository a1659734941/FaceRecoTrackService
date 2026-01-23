@echo off
chcp 65001 >nul
setlocal enabledelayedexpansion

echo ========================================
echo    FaceRecoTrackService 打包脚本
echo ========================================
echo.

:: 设置变量
set PROJECT_DIR=FaceRecoTrackService
set PROJECT_FILE=%PROJECT_DIR%\FaceRecoTrackService.csproj
set OUTPUT_DIR=dist
set PUBLISH_DIR=%OUTPUT_DIR%\publish
set VERSION=%date:~0,4%%date:~5,2%%date:~8,2%_%time:~0,2%%time:~3,2%%time:~6,2%
set VERSION=%VERSION: =0%

:: 检查项目文件是否存在
if not exist "%PROJECT_FILE%" (
    echo [错误] 项目文件不存在: %PROJECT_FILE%
    pause
    exit /b 1
)

:: 清理旧的输出目录
echo [1/5] 清理旧的输出目录...
if exist "%OUTPUT_DIR%" (
    rmdir /s /q "%OUTPUT_DIR%"
)
mkdir "%OUTPUT_DIR%"
mkdir "%PUBLISH_DIR%"

:: 还原 NuGet 包
echo [2/5] 还原 NuGet 包...
dotnet restore "%PROJECT_FILE%"
if errorlevel 1 (
    echo [错误] NuGet 包还原失败
    pause
    exit /b 1
)

:: 发布项目（单文件）
echo [3/5] 发布项目（单文件模式）...
dotnet publish "%PROJECT_FILE%" ^
    --configuration Release ^
    --runtime win-x64 ^
    --self-contained true ^
    --output "%PUBLISH_DIR%" ^
    /p:PublishSingleFile=true ^
    /p:IncludeNativeLibrariesForSelfExtract=true ^
    /p:EnableCompressionInSingleFile=true ^
    /p:DebugType=None ^
    /p:DebugSymbols=false

if errorlevel 1 (
    echo [错误] 项目发布失败
    pause
    exit /b 1
)

:: 复制 res 文件夹
echo [4/5] 复制 res 文件夹...
if exist "%PROJECT_DIR%\res" (
    xcopy /E /I /Y "%PROJECT_DIR%\res" "%PUBLISH_DIR%\res"
    if errorlevel 1 (
        echo [警告] res 文件夹复制可能不完整
    ) else (
        echo [成功] res 文件夹已复制
    )
) else (
    echo [警告] res 文件夹不存在，跳过复制
)

:: 复制配置文件
echo [5/5] 复制配置文件...
if exist "%PROJECT_DIR%\appsettings.json" (
    copy /Y "%PROJECT_DIR%\appsettings.json" "%PUBLISH_DIR%\"
)
if exist "%PROJECT_DIR%\appsettings.Development.json" (
    copy /Y "%PROJECT_DIR%\appsettings.Development.json" "%PUBLISH_DIR%\"
)

:: 创建发布信息文件
echo 创建发布信息文件...
echo 构建时间: %date% %time% > "%PUBLISH_DIR%\build_info.txt"
echo 版本: %VERSION% >> "%PUBLISH_DIR%\build_info.txt"
echo 运行时: win-x64 >> "%PUBLISH_DIR%\build_info.txt"
echo 单文件模式: 是 >> "%PUBLISH_DIR%\build_info.txt"

:: 显示结果
echo.
echo ========================================
echo    打包完成！
echo ========================================
echo.
echo 发布目录: %CD%\%PUBLISH_DIR%
echo 主程序: FaceRecoTrackService.exe
echo.
echo 文件列表:
dir /B "%PUBLISH_DIR%"
echo.
echo 提示: 
echo   1. 确保 res 文件夹已正确复制
echo   2. 根据实际情况修改 appsettings.json 配置
echo   3. 运行 FaceRecoTrackService.exe 启动服务
echo.

pause
