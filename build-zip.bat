@echo off
setlocal enabledelayedexpansion

set CONFIGURATION=Release
set PLATFORM=x64
set SELF_CONTAINED=true

if not "%~1"=="" set CONFIGURATION=%~1
if not "%~2"=="" set PLATFORM=%~2
if not "%~3"=="" set SELF_CONTAINED=%~3
set RUNTIME_PLATFORM=%PLATFORM%
if /I "%PLATFORM%"=="ARM64" set RUNTIME_PLATFORM=arm64
if /I "%PLATFORM%"=="x64" set RUNTIME_PLATFORM=x64

set ROOT=%~dp0
set APP_PROJECT=%ROOT%MDReader.App\MDReader.App.csproj
set PUBLISH_DIR=%ROOT%MDReader.App\bin\%PLATFORM%\%CONFIGURATION%\net10.0-windows10.0.22621.0\win-%RUNTIME_PLATFORM%\publish
set OUTPUT_DIR=%ROOT%ZipOutput
set ZIP_PATH=%OUTPUT_DIR%\MDReader-%CONFIGURATION%-win-%PLATFORM%.zip

if not exist "%OUTPUT_DIR%" mkdir "%OUTPUT_DIR%"

echo.
echo == Publishing app ==
dotnet publish "%APP_PROJECT%" -c %CONFIGURATION% -r win-%RUNTIME_PLATFORM% --self-contained %SELF_CONTAINED%
if errorlevel 1 exit /b 1

if not exist "%PUBLISH_DIR%" (
    echo Publish output not found at %PUBLISH_DIR%
    echo Ensure the runtime identifier win-%RUNTIME_PLATFORM% is supported and the publish step succeeded.
    exit /b 1
)

echo.
echo == Creating zip ==
powershell -NoProfile -Command "Compress-Archive -Path '%PUBLISH_DIR%\*' -DestinationPath '%ZIP_PATH%' -Force"
if errorlevel 1 exit /b 1

echo Zip generated at %ZIP_PATH%
endlocal
