@echo off
rem Builds the cross-platform (Avalonia) LiveSplit for Windows.
rem
rem Usage: build.bat [single^|aot] [runtime]
rem   single  self-contained single-file executable (default)
rem   aot     Native AOT executable: no JIT, faster startup, smaller and lower memory use.
rem           Needs Visual Studio (or Build Tools) with the "Desktop development with C++"
rem           workload. The native graphics/hotkey DLLs are placed next to the exe.
rem   runtime defaults to win-x64; also win-arm64, win-x86 (single only)
rem
rem Output: artifacts\publish\<runtime>-<mode>\LiveSplit.Avalonia.exe

setlocal

rem Capture the script folder before parsing: shift also shifts %0.
set "ROOT=%~dp0"
set "SCRIPT=%~f0"
set "MODE=single"
set "RUNTIME=win-x64"

:parse
if "%~1"=="" goto parsed
if /i "%~1"=="single" (set "MODE=single") else if /i "%~1"=="aot" (set "MODE=aot") else if /i "%~1"=="-h" (goto usage) else if /i "%~1"=="--help" (goto usage) else (set "RUNTIME=%~1")
shift
goto parse

:usage
findstr /b "rem" "%SCRIPT%"
exit /b 0

:parsed

rem pushd maps a drive letter when the repository is on a network/WSL path, which cmd needs.
pushd "%ROOT%" || exit /b 1

where dotnet >nul 2>nul
if errorlevel 1 (
    echo The .NET SDK was not found. Install the .NET 10 SDK from https://dot.net and try again.
    popd
    exit /b 1
)

set "OUTPUT=%CD%\artifacts\publish\%RUNTIME%-%MODE%"

rem Build in a dedicated artifacts folder (bin, obj and build output) so this neither overwrites
rem nor shares intermediate files with regular builds or with builds for other OSes/runtimes.
set "WORK=%CD%\artifacts\publish-work\%RUNTIME%-%MODE%"

rem Native AOT finds the MSVC linker with vswhere.exe, which the Visual Studio installer does not
rem put on PATH; without it the linker path ends up containing an error message.
if /i "%MODE%"=="aot" set "PATH=%PATH%;%ProgramFiles(x86)%\Microsoft Visual Studio\Installer"

if /i "%MODE%"=="aot" (
    set "MODE_ARGS=-p:PublishAot=true"
) else (
    set "MODE_ARGS=-p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:IncludeAllContentForSelfExtract=true -p:EnableCompressionInSingleFile=true"
)

echo Building LiveSplit.Avalonia (%MODE%) for %RUNTIME%...

if exist "%OUTPUT%" rmdir /s /q "%OUTPUT%"

dotnet publish "src\LiveSplit.Avalonia\LiveSplit.Avalonia.csproj" ^
    --configuration Release ^
    --runtime %RUNTIME% ^
    --self-contained true ^
    --output "%OUTPUT%" ^
    -p:ArtifactsPath="%WORK%" ^
    -p:DebugType=none ^
    -p:DebugSymbols=false ^
    %MODE_ARGS%

if errorlevel 1 (
    echo.
    echo Build failed.
    if /i "%MODE%"=="aot" echo Note: Native AOT also needs the Visual Studio "Desktop development with C++" workload.
    popd
    exit /b 1
)

rem Native libraries from NuGet bring their own debug symbols (~100 MB); they aren't needed to run.
del /q "%OUTPUT%\*.pdb" 2>nul

echo.
echo Done: %OUTPUT%\LiveSplit.Avalonia.exe

popd
endlocal
