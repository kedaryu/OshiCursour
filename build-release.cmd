@echo off
setlocal EnableExtensions

pushd "%~dp0"

where dotnet.exe >nul 2>nul
if errorlevel 1 (
    echo [ERROR] .NET SDK was not found.
    echo Install the .NET 8 SDK or Visual Studio 2022 with the .NET desktop development workload.
    pause
    popd
    exit /b 1
)

set "SDK_FOUND="
for /f "delims=" %%S in ('dotnet --list-sdks 2^>nul') do set "SDK_FOUND=1"
if not defined SDK_FOUND (
    echo [ERROR] dotnet.exe exists, but no .NET SDK is installed.
    echo Install the .NET 8 SDK or Visual Studio 2022 with the .NET desktop development workload.
    echo https://dotnet.microsoft.com/download/dotnet/8.0
    pause
    popd
    exit /b 1
)

if exist "%~dp0publish\win-x64" rmdir /s /q "%~dp0publish\win-x64"
if exist "%~dp0publish\release-assets" rmdir /s /q "%~dp0publish\release-assets"

dotnet publish "%~dp0src\CursorCycle\CursorCycle.csproj" ^
    --configuration Release ^
    --runtime win-x64 ^
    --self-contained true ^
    --output "%~dp0publish\win-x64" ^
    -p:PublishSingleFile=true ^
    -p:IncludeNativeLibrariesForSelfExtract=true ^
    -p:DebugSymbols=false ^
    -p:DebugType=None

set "BUILD_EXIT=%ERRORLEVEL%"
if not "%BUILD_EXIT%"=="0" (
    echo.
    echo [ERROR] Build failed. Exit code: %BUILD_EXIT%
    pause
    popd
    exit /b %BUILD_EXIT%
)

if not exist "%~dp0publish\win-x64\OshiCursour.exe" (
    echo.
    echo [ERROR] Build command finished, but OshiCursour.exe was not created.
    pause
    popd
    exit /b 1
)

mkdir "%~dp0publish\release-assets"
powershell.exe -NoProfile -ExecutionPolicy Bypass -Command ^
    "Compress-Archive -LiteralPath '%~dp0publish\win-x64\OshiCursour.exe' -DestinationPath '%~dp0publish\release-assets\OshiCursour-win-x64.zip' -Force; $hash = (Get-FileHash -Algorithm SHA256 -LiteralPath '%~dp0publish\release-assets\OshiCursour-win-x64.zip').Hash.ToLowerInvariant(); Set-Content -LiteralPath '%~dp0publish\release-assets\checksums.txt' -Value ($hash + '  OshiCursour-win-x64.zip') -Encoding ascii"

if errorlevel 1 (
    echo.
    echo [ERROR] Release assets could not be created.
    pause
    popd
    exit /b 1
)

echo.
echo Build completed:
echo %~dp0publish\win-x64\OshiCursour.exe
echo.
echo GitHub Release assets:
echo %~dp0publish\release-assets\OshiCursour-win-x64.zip
echo %~dp0publish\release-assets\checksums.txt
start "" "%~dp0publish\release-assets"
pause

popd
exit /b 0
