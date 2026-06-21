@echo off
setlocal enabledelayedexpansion

set CONFIGURATION=Release
set PLATFORM=x64
set SELF_CONTAINED=true
set TARGET_OS=win

if not "%~1"=="" set CONFIGURATION=%~1
if not "%~2"=="" set PLATFORM=%~2
if not "%~3"=="" set SELF_CONTAINED=%~3
if not "%~4"=="" set TARGET_OS=%~4

if /I not "%TARGET_OS%"=="win" if /I not "%TARGET_OS%"=="linux" (
    echo Unsupported TARGET_OS "%TARGET_OS%".
    echo Supported values: win, linux
    exit /b 1
)

if /I "%TARGET_OS%"=="linux" (
    echo Linux publish is not supported for this project.
    echo MDReader.App targets net10.0-windows and uses WPF/WebView2, which are Windows-only.
    echo To support Linux, split core logic into a cross-platform library and add a Linux UI host.
    exit /b 1
)

set RUNTIME_PLATFORM=%PLATFORM%
if /I "%PLATFORM%"=="ARM64" set RUNTIME_PLATFORM=arm64
if /I "%PLATFORM%"=="x64" set RUNTIME_PLATFORM=x64

set ROOT=%~dp0
set APP_PROJECT=%ROOT%MDReader.App\MDReader.App.csproj
set PUBLISH_DIR=%ROOT%MDReader.App\bin\%PLATFORM%\%CONFIGURATION%\net10.0-windows10.0.22621.0\%TARGET_OS%-%RUNTIME_PLATFORM%\publish
set OUTPUT_DIR=%ROOT%ZipOutput
set ZIP_PATH=%OUTPUT_DIR%\MDReader-%CONFIGURATION%-%TARGET_OS%-%PLATFORM%.zip

if not exist "%OUTPUT_DIR%" mkdir "%OUTPUT_DIR%"

echo.
echo == Publishing app ==
dotnet publish "%APP_PROJECT%" -c %CONFIGURATION% -r %TARGET_OS%-%RUNTIME_PLATFORM% --self-contained %SELF_CONTAINED%
if errorlevel 1 exit /b 1

if not exist "%PUBLISH_DIR%" (
    echo Publish output not found at %PUBLISH_DIR%
    echo Ensure the runtime identifier %TARGET_OS%-%RUNTIME_PLATFORM% is supported and the publish step succeeded.
    exit /b 1
)

echo.
echo == Creating zip ==
powershell -NoProfile -Command "Compress-Archive -Path '%PUBLISH_DIR%\*' -DestinationPath '%ZIP_PATH%' -Force"
if errorlevel 1 exit /b 1

echo Zip generated at %ZIP_PATH%
endlocal
