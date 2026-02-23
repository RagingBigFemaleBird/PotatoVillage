@echo off
REM Launch8Players.bat
REM Batch script to launch 8 instances of PotatoVillage app for testing

echo ========================================
echo PotatoVillage Multi-Instance Launcher
echo ========================================
echo.

REM Check if we should build first
if "%1"=="build" (
    echo Building PotatoVillage...
    dotnet build PotatoVillage\PotatoVillage.csproj -c Debug -f net9.0-windows10.0.19041.0
    if errorlevel 1 (
        echo Build failed!
        pause
        exit /b 1
    )
    echo Build successful!
    echo.
)

REM Set the number of players (default 8)
set PLAYER_COUNT=8
if not "%2"=="" set PLAYER_COUNT=%2

echo Launching %PLAYER_COUNT% instances of PotatoVillage...
echo.

REM Try to find the executable
set EXE_PATH=PotatoVillage\bin\Debug\net9.0-windows10.0.19041.0\PotatoVillage.exe

if not exist "%EXE_PATH%" (
    set EXE_PATH=PotatoVillage\bin\Debug\net8.0-windows10.0.19041.0\PotatoVillage.exe
)

if not exist "%EXE_PATH%" (
    echo Could not find PotatoVillage.exe
    echo Using dotnet run instead...
    echo.
    
    for /L %%i in (1,1,%PLAYER_COUNT%) do (
        echo Starting Player %%i...
        start "Player %%i" dotnet run --project PotatoVillage\PotatoVillage.csproj -c Debug -f net9.0-windows10.0.19041.0
        timeout /t 1 /nobreak >nul
    )
) else (
    echo Found executable: %EXE_PATH%
    echo.
    
    for /L %%i in (1,1,%PLAYER_COUNT%) do (
        echo Starting Player %%i...
        start "Player %%i" "%EXE_PATH%"
        timeout /t 1 /nobreak >nul
    )
)

echo.
echo ========================================
echo All %PLAYER_COUNT% instances launched!
echo ========================================
echo.
echo Testing Instructions:
echo 1. In the first instance, create a new game room
echo 2. Note the Room # displayed
echo 3. In other instances, join using that Room #
echo 4. Once all players have joined, click 'Start Game'
echo.
pause
