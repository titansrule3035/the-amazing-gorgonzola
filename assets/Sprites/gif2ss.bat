@echo off
setlocal enabledelayedexpansion

:: =========================================================
:: GIF -> Horizontal Sprite Sheet Converter
::
:: Features:
:: - Ask for a folder path
:: - Searches that folder for GIFs
:: - Preserves transparency
:: - Keeps original filename
:: - Deletes original GIF after success
::
:: Requires ImageMagick:
:: https://imagemagick.org
:: =========================================================

:: Check ImageMagick
where magick >nul 2>nul
if errorlevel 1 (
    echo ImageMagick not found in PATH.
    echo Install from:
    echo https://imagemagick.org
    pause
    exit /b
)

:: Ask user for folder
echo.
set /p TARGET=Enter folder path containing GIFs: 

:: Remove quotes if pasted
set TARGET=%TARGET:"=%

:: Validate folder
if not exist "%TARGET%" (
    echo.
    echo Folder does not exist.
    pause
    exit /b
)

echo.
echo Searching for GIFs in:
echo %TARGET%
echo.

pushd "%TARGET%"

for %%F in (*.gif) do (

    echo Processing %%F ...

    set "TMP=temp_%%~nF"

    mkdir "!TMP!" >nul 2>nul

    :: Extract frames while preserving transparency
    magick "%%F" -coalesce "!TMP!\frame_%%04d.png"

    :: Create horizontal sprite sheet
    magick montage "!TMP!\frame_*.png" ^
        -background none ^
        -tile x1 ^
        -geometry +0+0 ^
        "%%~nF.png"

    :: Delete original GIF if successful
    if exist "%%~nF.png" (
        del "%%F"
        echo Deleted original: %%F
    ) else (
        echo Failed to create sprite sheet for %%F
    )

    :: Cleanup temp files
    rmdir /s /q "!TMP!"

    echo Created: %%~nF.png
    echo.
)

popd

echo Done.
pause