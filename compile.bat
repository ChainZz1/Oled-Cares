@echo off
setlocal
echo Compiling OLED Cares...
set CSC_PATH=C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe

if not exist "%CSC_PATH%" (
    echo Error: .NET Framework compiler not found at %CSC_PATH%
    pause
    exit /b 1
)

"%CSC_PATH%" /target:winexe /out:OledCares.exe /optimize /platform:anycpu /reference:System.dll,System.Drawing.dll,System.Windows.Forms.dll OledCares.cs

if %ERRORLEVEL% equ 0 (
    echo Compilation successful! Created OledCares.exe
) else (
    echo Compilation failed!
    pause
)
