@echo off
set CSC_PATH=C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe
if not exist "%CSC_PATH%" (
    echo Error: C# Compiler csc.exe not found at %CSC_PATH%
    pause
    exit /b 1
)

echo Compiling Sagittarius Explorer...
:: Compiles Program.cs into a standalone EXE at the parent directory (workspace root)
"%CSC_PATH%" /target:winexe /out:..\SagittariusA.exe /lib:C:\Windows\Microsoft.NET\Framework64\v4.0.30319\WPF /r:WindowsBase.dll,PresentationCore.dll,PresentationFramework.dll,System.Xaml.dll,System.dll,System.Drawing.dll,System.Core.dll,Microsoft.VisualBasic.dll Program.cs

if %ERRORLEVEL% equ 0 (
    echo Compilation successful! SagittariusA.exe generated at the root of the workspace.
) else (
    echo Compilation failed with error code %ERRORLEVEL%.
)
