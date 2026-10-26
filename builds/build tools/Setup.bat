@echo off
setlocal EnableExtensions
title The Amazing Gorgonzola - Build

:: ==================================================
:: CONFIGURATION
:: ==================================================

set "EXPORT_PRESET=win-TEST"
set "OUTPUT_DIR=builds\win"
set "OUTPUT_NAME=TheAmazingGorgonzola.exe"

set "TOOLS_DIR=%~dp0"
set "TEMPLATE_ARCHIVE=%TOOLS_DIR%godot_build_templates_dotnet.tpz"

:: Extracted templates folder beside Setup.bat
set "LOCAL_TEMPLATE_DIR=%TOOLS_DIR%godot_export_templates"

set "TEMPLATE_VERSION=4.7.2.stable.mono"
set "TEMPLATE_DIR=%APPDATA%\Godot\export_templates\%TEMPLATE_VERSION%"

set "TEMP_TEMPLATE_ZIP=%TEMP%\GorgonzolaGodotTemplates_%RANDOM%_%RANDOM%.zip"
set "TEMPLATE_BACKUP_DIR=%TEMP%\GorgonzolaGodotTemplateBackup_%RANDOM%_%RANDOM%"

set "TEMPLATE_DIR_BACKED_UP=0"
set "TEMPLATE_STAGED=0"

:: ==================================================
:: LOCATE PROJECT
:: ==================================================

for %%I in ("%TOOLS_DIR%..\..") do set "PROJECT_DIR=%%~fI"

cd /d "%PROJECT_DIR%" || goto failed

echo.
echo ==================================================
echo   THE AMAZING GORGONZOLA - BUILD
echo ==================================================
echo.
echo Project: "%PROJECT_DIR%"
echo.

:: ==================================================
:: READ GODOT EXECUTABLE PATH
:: ==================================================

echo [1/6] Checking Godot configuration...

if not exist "%TOOLS_DIR%GODOT_PATH.txt" (
    echo ERROR: GODOT_PATH.txt was not found.
    goto failed
)

set "GODOT="
set /p "GODOT="<"%TOOLS_DIR%GODOT_PATH.txt"

if not defined GODOT (
    echo ERROR: GODOT_PATH.txt is empty.
    goto failed
)

if not exist "%GODOT%" (
    echo ERROR: Godot executable was not found:
    echo "%GODOT%"
    goto failed
)

if not exist "%PROJECT_DIR%\project.godot" (
    echo ERROR: project.godot was not found.
    goto failed
)

echo Godot executable: "%GODOT%"
"%GODOT%" --version
if errorlevel 1 goto failed

:: ==================================================
:: CHECK EXPORT PRESET
:: ==================================================

echo.
echo [2/6] Checking export configuration...

if not exist "%PROJECT_DIR%\export_presets.cfg" (
    echo ERROR: export_presets.cfg was not found.
    goto failed
)

findstr /L /C:"name=""%EXPORT_PRESET%""" "%PROJECT_DIR%\export_presets.cfg" >nul
if errorlevel 1 (
    echo ERROR: Export preset "%EXPORT_PRESET%" was not found.
    goto failed
)

:: ==================================================
:: EXTRACT BUNDLED TEMPLATES
:: ==================================================

echo.
echo [3/6] Preparing bundled export templates...

if not exist "%TEMPLATE_ARCHIVE%" (
    echo ERROR: Template archive was not found:
    echo "%TEMPLATE_ARCHIVE%"
    goto failed
)

:: Remove stale extracted templates from a previous run.
if exist "%LOCAL_TEMPLATE_DIR%\" (
    echo Removing old extracted templates...
    rmdir /s /q "%LOCAL_TEMPLATE_DIR%"
    if exist "%LOCAL_TEMPLATE_DIR%\" (
        echo ERROR: Could not remove old extracted templates.
        goto failed
    )
)

mkdir "%LOCAL_TEMPLATE_DIR%"
if errorlevel 1 goto failed

set "GORGONZOLA_ARCHIVE=%TEMPLATE_ARCHIVE%"
set "GORGONZOLA_TEMP=%LOCAL_TEMPLATE_DIR%"
set "GORGONZOLA_ZIP=%TEMP_TEMPLATE_ZIP%"

echo Extracting bundled archive...

powershell -NoProfile -ExecutionPolicy Bypass -Command ^
    "$ErrorActionPreference = 'Stop'; " ^
    "try { " ^
    "Copy-Item -LiteralPath $env:GORGONZOLA_ARCHIVE -Destination $env:GORGONZOLA_ZIP -Force; " ^
    "Expand-Archive -LiteralPath $env:GORGONZOLA_ZIP -DestinationPath $env:GORGONZOLA_TEMP -Force; " ^
    "} finally { " ^
    "if (Test-Path -LiteralPath $env:GORGONZOLA_ZIP) { Remove-Item -LiteralPath $env:GORGONZOLA_ZIP -Force }; " ^
    "}"

if errorlevel 1 (
    echo ERROR: Template extraction failed.
    goto failed
)

:: Find the extracted template directory.
if exist "%LOCAL_TEMPLATE_DIR%\templates\windows_release_x86_64.exe" (
    set "EXTRACTED_TEMPLATE_DIR=%LOCAL_TEMPLATE_DIR%\templates"
) else if exist "%LOCAL_TEMPLATE_DIR%\windows_release_x86_64.exe" (
    set "EXTRACTED_TEMPLATE_DIR=%LOCAL_TEMPLATE_DIR%"
) else (
    echo ERROR: Windows release template was not found in the archive.
    goto failed
)

:: ==================================================
:: TEMPORARILY STAGE TEMPLATES FOR GODOT
:: ==================================================

echo Temporarily preparing Godot's template directory...

if exist "%TEMPLATE_DIR%\" (
    move "%TEMPLATE_DIR%" "%TEMPLATE_BACKUP_DIR%" >nul
    if errorlevel 1 (
        echo ERROR: Could not preserve existing templates.
        goto failed
    )
    set "TEMPLATE_DIR_BACKED_UP=1"
)

mkdir "%TEMPLATE_DIR%"
if errorlevel 1 goto failed

set "TEMPLATE_STAGED=1"

copy /Y "%EXTRACTED_TEMPLATE_DIR%\*" "%TEMPLATE_DIR%\" >nul
if errorlevel 1 (
    echo ERROR: Could not stage export templates.
    goto failed
)

if not exist "%TEMPLATE_DIR%\windows_release_x86_64.exe" (
    echo ERROR: Windows release template is missing.
    goto failed
)

echo Export templates are ready.

:: ==================================================
:: IMPORT ASSETS
:: ==================================================

echo.
echo [4/6] Importing project assets...

"%GODOT%" --headless --path "%PROJECT_DIR%" --editor --import --quit
if errorlevel 1 (
    echo ERROR: Asset import failed.
    goto failed
)

:: ==================================================
:: BUILD C# SOLUTION
:: ==================================================

echo.
echo [5/6] Building C# solution...

"%GODOT%" --headless --path "%PROJECT_DIR%" --build-solutions --quit
if errorlevel 1 (
    echo ERROR: C# build failed.
    goto failed
)

:: ==================================================
:: CREATE OUTPUT DIRECTORY
:: ==================================================

if not exist "%PROJECT_DIR%\%OUTPUT_DIR%\" (
    mkdir "%PROJECT_DIR%\%OUTPUT_DIR%"
    if errorlevel 1 goto failed
)

:: ==================================================
:: EXPORT RELEASE BUILD
:: ==================================================

echo.
echo [6/6] Exporting release build...
echo.

"%GODOT%" --headless --path "%PROJECT_DIR%" --export-release "%EXPORT_PRESET%" "%PROJECT_DIR%\%OUTPUT_DIR%\%OUTPUT_NAME%"
if errorlevel 1 (
    echo ERROR: Export failed.
    goto failed
)

:: ==================================================
:: CLEANUP
:: ==================================================

echo.
echo Cleaning up extracted templates...

call :cleanup

echo.
echo ==================================================
echo   BUILD COMPLETED SUCCESSFULLY!
echo ==================================================
echo.
echo Your exported game:
echo "%PROJECT_DIR%\%OUTPUT_DIR%\%OUTPUT_NAME%"
echo.
pause
exit /b 0

:: ==================================================
:: CLEANUP ROUTINE
:: ==================================================

:cleanup

:: Restore the original templates, if they existed.
if "%TEMPLATE_STAGED%"=="1" (
    if exist "%TEMPLATE_DIR%\" (
        rmdir /s /q "%TEMPLATE_DIR%"
    )
)

if "%TEMPLATE_DIR_BACKED_UP%"=="1" (
    if exist "%TEMPLATE_BACKUP_DIR%\" (
        move "%TEMPLATE_BACKUP_DIR%" "%TEMPLATE_DIR%" >nul
        if errorlevel 1 (
            echo WARNING: Could not restore original templates.
            echo Backup remains at "%TEMPLATE_BACKUP_DIR%"
        )
    )
)

:: Delete extracted template files beside Setup.bat.
if exist "%LOCAL_TEMPLATE_DIR%\" (
    rmdir /s /q "%LOCAL_TEMPLATE_DIR%"
)

if exist "%LOCAL_TEMPLATE_DIR%\" (
    echo WARNING: Could not delete "%LOCAL_TEMPLATE_DIR%".
) else (
    echo Extracted template folder removed.
)

:: Delete any leftover temporary ZIP.
if exist "%TEMP_TEMPLATE_ZIP%" del /q "%TEMP_TEMPLATE_ZIP%"

exit /b 0

:: ==================================================
:: ERROR HANDLING
:: ==================================================

:failed
echo.
echo BUILD FAILED - cleaning up...

call :cleanup

echo.
echo Review the error messages above.
echo.
pause
exit /b 1