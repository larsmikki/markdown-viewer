@echo off
setlocal

echo [1/2] Building...
dotnet publish -c Release --self-contained false -o dist\MarkdownViewer
if errorlevel 1 ( echo FAILED & exit /b 1 )

echo [2/2] Building installer...
set ISCC=
if exist "C:\Program Files (x86)\Inno Setup 6\ISCC.exe" set "ISCC=C:\Program Files (x86)\Inno Setup 6\ISCC.exe"
if exist "C:\Program Files\Inno Setup 6\ISCC.exe"       set "ISCC=C:\Program Files\Inno Setup 6\ISCC.exe"
if "%ISCC%"=="" (
    echo SKIPPED: Inno Setup not found.
    echo App is ready in dist\MarkdownViewer\MarkdownViewer.exe
    exit /b 0
)
"%ISCC%" installer\MdView.iss
if errorlevel 1 ( echo FAILED & exit /b 1 )

echo.
echo Done! Installer is at dist\MarkdownViewerSetup.exe
