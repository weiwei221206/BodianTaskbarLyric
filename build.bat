@echo off
set CSC=C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe
set REFS=System.Xaml.dll,System.Web.Extensions.dll,System.Windows.Forms.dll,System.Drawing.dll,C:\Windows\Microsoft.NET\Framework64\v4.0.30319\WPF\PresentationCore.dll,C:\Windows\Microsoft.NET\Framework64\v4.0.30319\WPF\PresentationFramework.dll,C:\Windows\Microsoft.NET\Framework64\v4.0.30319\WPF\WindowsBase.dll

echo Compiling BodianTaskbarLyric.exe with Settings UI and Icon...
%CSC% /target:winexe /optimize+ /platform:x64 /win32icon:"%~dp0assets\app.ico" /resource:"%~dp0assets\ico.png",ico.png /resource:"%~dp0assets\bodian_client.png",bodian_client.png /r:%REFS% /out:"%~dp0BodianTaskbarLyric.exe" "%~dp0BodianTaskbarLyric.cs"

if %ERRORLEVEL% equ 0 (
    echo [SUCCESS] BodianTaskbarLyric.exe built successfully!
) else (
    echo [ERROR] Build failed.
)
