@echo off
setlocal
cd /d "%~dp0"
set "BUILD=%WINDIR%\Microsoft.NET\Framework\v4.0.30319\MSBuild.exe"
if exist "%ProgramFiles(x86)%\Microsoft Visual Studio\Installer\vswhere.exe" (
    for /f "usebackq delims=" %%i in (`"%ProgramFiles(x86)%\Microsoft Visual Studio\Installer\vswhere.exe" -latest -products * -requires Microsoft.Component.MSBuild -find MSBuild\Current\Bin\MSBuild.exe`) do set "BUILD=%%i"
)
"%BUILD%" TovarSales.csproj /nologo /p:Configuration=Release /verbosity:minimal
if errorlevel 1 exit /b 1
copy /y bin\Release\TovarSales.exe bin\TovarSales.exe >nul
if errorlevel 1 exit /b 1
copy /y bin\Release\TovarSales.exe.config bin\TovarSales.exe.config >nul
