@echo off
setlocal EnableExtensions EnableDelayedExpansion

pushd "%~dp0"

set "PROJECT_FILE=%~dp0src\CursorCycle\CursorCycle.csproj"
set "EXPECTED_REMOTE=https://github.com/kedaryu/OshiCursour.git"
set "VERSION="

where git.exe >nul 2>nul
if errorlevel 1 (
    echo [ERROR] Git was not found.
    echo Install Git for Windows, then run this file again.
    goto :failed
)

git rev-parse --is-inside-work-tree >nul 2>nul
if errorlevel 1 (
    echo [ERROR] This folder is not a Git repository.
    goto :failed
)

if not exist "%PROJECT_FILE%" (
    echo [ERROR] Project file was not found:
    echo %PROJECT_FILE%
    goto :failed
)

for /f "usebackq delims=" %%V in (`powershell.exe -NoProfile -ExecutionPolicy Bypass -Command "$p = [xml](Get-Content -LiteralPath '%PROJECT_FILE%' -Raw); $node = $p.SelectSingleNode('/Project/PropertyGroup/Version'); if ($null -eq $node) { exit 1 }; $value = $node.InnerText.Trim(); if ($value -notmatch '^\d+\.\d+\.\d+$') { exit 2 }; Write-Output $value"`) do set "VERSION=%%V"
if not defined VERSION (
    echo [ERROR] A valid x.x.x version could not be read from CursorCycle.csproj.
    echo Check the Version element in:
    echo %PROJECT_FILE%
    goto :failed
)

set "TAG=v!VERSION!"

for /f "delims=" %%B in ('git branch --show-current') do set "CURRENT_BRANCH=%%B"
if /i not "%CURRENT_BRANCH%"=="main" (
    echo [ERROR] Current branch is "%CURRENT_BRANCH%". Switch to main first.
    goto :failed
)

for /f "delims=" %%R in ('git remote get-url origin 2^>nul') do set "REMOTE_URL=%%R"
if not defined REMOTE_URL (
    echo [ERROR] Git remote "origin" is not configured.
    goto :failed
)

if /i not "%REMOTE_URL%"=="%EXPECTED_REMOTE%" if /i not "%REMOTE_URL%"=="git@github.com:kedaryu/OshiCursour.git" (
    echo [ERROR] Unexpected origin URL:
    echo %REMOTE_URL%
    echo Expected: %EXPECTED_REMOTE%
    goto :failed
)

echo Checking GitHub and tags...
git fetch origin main --tags
if errorlevel 1 (
    echo [ERROR] Could not fetch the latest GitHub state.
    goto :failed
)

for /f "tokens=1,2" %%A in ('git rev-list --left-right --count HEAD...origin/main') do (
    set "LOCAL_AHEAD=%%A"
    set "LOCAL_BEHIND=%%B"
)

if not "%LOCAL_BEHIND%"=="0" (
    echo [ERROR] Local main is behind GitHub by %LOCAL_BEHIND% commit^(s^).
    echo Run git pull before publishing.
    goto :failed
)

git show-ref --verify --quiet "refs/tags/%TAG%"
if not errorlevel 1 (
    for /f "delims=" %%S in ('git status --porcelain') do set "HAS_CHANGES=1"
    if defined HAS_CHANGES (
        echo [ERROR] Local tag %TAG% already exists, but the working tree has new changes.
        echo Increase the project version before publishing those changes.
        goto :failed
    )
    for /f "delims=" %%T in ('git rev-list -n 1 "%TAG%"') do set "TAG_COMMIT=%%T"
    for /f "delims=" %%H in ('git rev-parse HEAD') do set "HEAD_COMMIT=%%H"
    if /i not "!TAG_COMMIT!"=="!HEAD_COMMIT!" (
        echo [ERROR] Local tag %TAG% already points to another commit.
        goto :failed
    )
)

git ls-remote --exit-code --tags origin "refs/tags/%TAG%" >nul 2>nul
if not errorlevel 1 (
    echo [ERROR] GitHub already contains tag %TAG%.
    echo Increase the project version before publishing another release.
    goto :failed
)

echo.
echo ============================================================
echo Release version : !VERSION!
echo Release tag     : !TAG!
echo Branch          : %CURRENT_BRANCH%
echo GitHub          : %REMOTE_URL%
echo ============================================================
echo.
echo Changes to commit:
git status --short
echo.

git diff --check
if errorlevel 1 (
    echo [ERROR] Whitespace errors were detected. Fix them before publishing.
    goto :failed
)

choice /c YN /n /m "Commit these changes and publish !TAG!? [Y/N]: "
if errorlevel 2 goto :cancelled

git add -A
if errorlevel 1 goto :git_failed

git diff --cached --quiet
if errorlevel 1 (
    git commit -m "OshiCursour %TAG%"
    if errorlevel 1 goto :git_failed
) else (
    echo No new changes to commit. The current HEAD will be released.
)

git push origin main
if errorlevel 1 goto :git_failed

git show-ref --verify --quiet "refs/tags/%TAG%"
if errorlevel 1 (
    git tag -a "%TAG%" -m "OshiCursour %TAG%"
    if errorlevel 1 goto :git_failed
)

for /f "delims=" %%T in ('git rev-list -n 1 "%TAG%"') do set "TAG_COMMIT=%%T"
for /f "delims=" %%H in ('git rev-parse HEAD') do set "HEAD_COMMIT=%%H"
if /i not "!TAG_COMMIT!"=="!HEAD_COMMIT!" (
    echo [ERROR] Tag %TAG% does not point to the current release commit.
    goto :failed
)

git push origin "%TAG%"
if errorlevel 1 goto :git_failed

echo.
echo Release request completed successfully.
echo GitHub Actions:
echo https://github.com/kedaryu/OshiCursour/actions
echo.
echo Wait for "Create Windows release" to finish, then verify the Release assets.
pause
popd
exit /b 0

:git_failed
echo.
echo [ERROR] A Git command failed. Review the message above.
echo The completed Git operations were not undone automatically.
goto :failed

:cancelled
echo.
echo Publishing was cancelled. No commit, push, or tag was created.
popd
exit /b 0

:failed
echo.
pause
popd
exit /b 1
