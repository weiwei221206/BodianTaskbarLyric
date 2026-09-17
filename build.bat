@echo off
set CSC=C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe
set REFS=System.Xaml.dll,System.Web.Extensions.dll,System.Windows.Forms.dll,System.Drawing.dll,C:\Windows\Microsoft.NET\Framework64\v4.0.30319\WPF\PresentationCore.dll,C:\Windows\Microsoft.NET\Framework64\v4.0.30319\WPF\PresentationFramework.dll,C:\Windows\Microsoft.NET\Framework64\v4.0.30319\WPF\WindowsBase.dll

echo Compiling BodianTaskbarLyric.exe with Settings UI and Icon...
if exist "%~dp0BodianTaskbarLyric.exe.tmp" del /f /q "%~dp0BodianTaskbarLyric.exe.tmp"
%CSC% /target:winexe /optimize+ /platform:x64 /win32icon:"%~dp0assets\app.ico" /resource:"%~dp0assets\ico.png",ico.png /resource:"%~dp0assets\bodian_client.png",bodian_client.png /r:%REFS% /out:"%~dp0BodianTaskbarLyric.exe.tmp" /recurse:"%~dp0src\*.cs"

if %ERRORLEVEL% equ 0 (
    if exist "%~dp0BodianTaskbarLyric.exe.old" del /f /q "%~dp0BodianTaskbarLyric.exe.old" 2>nul
    if exist "%~dp0BodianTaskbarLyric.exe" move /y "%~dp0BodianTaskbarLyric.exe" "%~dp0BodianTaskbarLyric.exe.old" >nul 2>nul
    move /y "%~dp0BodianTaskbarLyric.exe.tmp" "%~dp0BodianTaskbarLyric.exe" >nul
    echo [SUCCESS] BodianTaskbarLyric.exe built successfully!
) else (
    echo [ERROR] Build failed.
)
