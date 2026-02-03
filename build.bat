@echo off
chcp 65001 >nul
setlocal enabledelayedexpansion

echo ========================================
echo    FaceTrackService Build Script
echo ========================================
echo.

set PROJECT_DIR=FaceRecoTrackService
set PROJECT_FILE=%PROJECT_DIR%\FaceRecoTrackService.csproj
set OUTPUT_DIR=dist
set PUBLISH_DIR=%OUTPUT_DIR%\FaceTrackService
for /f "delims=" %%i in ('powershell -NoProfile -Command "Get-Date -Format yyyyMMdd_HHmmss"') do set BUILD_VERSION=%%i

if not exist "%PROJECT_FILE%" (
    echo [ERROR] Project file not found: %PROJECT_FILE%
    pause
    exit /b 1
)

echo [1/7] Cleaning output...
if exist "%OUTPUT_DIR%" rmdir /s /q "%OUTPUT_DIR%"
mkdir "%OUTPUT_DIR%"
mkdir "%PUBLISH_DIR%"

echo [2/7] Restoring NuGet packages...
dotnet restore "%PROJECT_FILE%"
if errorlevel 1 (
    echo [ERROR] NuGet restore failed
    pause
    exit /b 1
)

echo [3/7] Publishing (single-file)...
dotnet publish "%PROJECT_FILE%" --configuration Release --runtime win-x64 --self-contained true --output "%PUBLISH_DIR%" /p:PublishSingleFile=true /p:IncludeNativeLibrariesForSelfExtract=true /p:EnableCompressionInSingleFile=true /p:DebugType=None /p:DebugSymbols=false
if errorlevel 1 (
    echo [ERROR] Publish failed
    pause
    exit /b 1
)

echo [4/7] Copying res folder...
if exist "%PROJECT_DIR%\res" (
    xcopy /E /I /Y "%PROJECT_DIR%\res" "%PUBLISH_DIR%\res"
    if errorlevel 1 (echo [WARN] res copy may be incomplete) else (echo [OK] res copied)
) else (
    echo [WARN] res folder not found, skipped
)

if exist "%PROJECT_DIR%\res\icon\FaceTrack.ico" (
    echo [OK] Icon found: res\icon\FaceTrack.ico
) else (
    echo [WARN] Icon not found: res\icon\FaceTrack.ico
)

echo [5/7] Copying qdrant folder (qdrant.exe)...
if exist "%PROJECT_DIR%\qdrant" (
    xcopy /E /I /Y "%PROJECT_DIR%\qdrant" "%PUBLISH_DIR%\qdrant"
    if errorlevel 1 (echo [WARN] qdrant copy may be incomplete) else (echo [OK] qdrant copied)
) else (
    echo [WARN] qdrant folder not found, skipped - put qdrant.exe in FaceRecoTrackService\qdrant for embedded Qdrant
)

echo [6/7] Copying config files...
if exist "%PROJECT_DIR%\appsettings.json" copy /Y "%PROJECT_DIR%\appsettings.json" "%PUBLISH_DIR%\"
if exist "%PROJECT_DIR%\appsettings.Development.json" copy /Y "%PROJECT_DIR%\appsettings.Development.json" "%PUBLISH_DIR%\"

echo [7/7] Copying scripts...
if exist "scripts" (
    xcopy /E /I /Y "scripts" "%PUBLISH_DIR%\scripts"
    if errorlevel 1 (echo [WARN] scripts copy may be incomplete) else (echo [OK] scripts copied)
) else (
    echo [WARN] scripts folder not found, skipped
)

echo Creating build_info.txt...
echo Build: %date% %time% > "%PUBLISH_DIR%\build_info.txt"
echo Version: %BUILD_VERSION% >> "%PUBLISH_DIR%\build_info.txt"
echo Runtime: win-x64 >> "%PUBLISH_DIR%\build_info.txt"
echo SingleFile: yes >> "%PUBLISH_DIR%\build_info.txt"

echo.
echo ========================================
echo    Build complete
echo ========================================
echo.
echo Output: %CD%\%PUBLISH_DIR%
echo Main: FaceTrackService.exe
echo.
dir /B "%PUBLISH_DIR%"
echo.
echo Next: edit appsettings.json if needed, then run FaceTrackService.exe
echo.
pause
