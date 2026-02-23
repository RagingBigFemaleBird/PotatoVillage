# Launch8Players.ps1
# Script to launch 8 instances of PotatoVillage app for testing multiplayer functionality

param(
    [int]$PlayerCount = 8,
    [switch]$Build,
    [string]$Configuration = "Debug",
    [string]$Framework = "net9.0-windows10.0.19041.0"
)

$ErrorActionPreference = "Stop"

# Get the script directory
$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$ProjectDir = Join-Path $ScriptDir "PotatoVillage"
$ProjectFile = Join-Path $ProjectDir "PotatoVillage.csproj"

# Build the project if requested
if ($Build) {
    Write-Host "Building PotatoVillage..." -ForegroundColor Cyan
    dotnet build $ProjectFile -c $Configuration -f $Framework
    if ($LASTEXITCODE -ne 0) {
        Write-Host "Build failed!" -ForegroundColor Red
        exit 1
    }
    Write-Host "Build successful!" -ForegroundColor Green
}

# Find the executable
$ExePath = Join-Path $ProjectDir "bin\$Configuration\$Framework\win10-x64\PotatoVillage.exe"

# Alternative paths to check
$AltPaths = @(
    (Join-Path $ProjectDir "bin\$Configuration\$Framework\PotatoVillage.exe"),
    (Join-Path $ProjectDir "bin\$Configuration\net9.0-windows10.0.19041.0\win10-x64\PotatoVillage.exe"),
    (Join-Path $ProjectDir "bin\$Configuration\net8.0-windows10.0.19041.0\win10-x64\PotatoVillage.exe"),
    (Join-Path $ProjectDir "bin\$Configuration\net8.0-windows10.0.19041.0\PotatoVillage.exe")
)

if (-not (Test-Path $ExePath)) {
    foreach ($path in $AltPaths) {
        if (Test-Path $path) {
            $ExePath = $path
            break
        }
    }
}

if (-not (Test-Path $ExePath)) {
    Write-Host "Could not find PotatoVillage.exe. Attempting to run via dotnet..." -ForegroundColor Yellow
    
    # Use dotnet run instead
    Write-Host "Launching $PlayerCount instances of PotatoVillage using dotnet run..." -ForegroundColor Cyan
    
    for ($i = 1; $i -le $PlayerCount; $i++) {
        Write-Host "Starting Player $i..." -ForegroundColor Green
        Start-Process -FilePath "dotnet" -ArgumentList "run", "--project", $ProjectFile, "-c", $Configuration, "-f", $Framework -WindowStyle Normal
        Start-Sleep -Milliseconds 500
    }
}
else {
    Write-Host "Found executable at: $ExePath" -ForegroundColor Cyan
    Write-Host "Launching $PlayerCount instances of PotatoVillage..." -ForegroundColor Cyan

    for ($i = 1; $i -le $PlayerCount; $i++) {
        Write-Host "Starting Player $i..." -ForegroundColor Green
        Start-Process -FilePath $ExePath -WindowStyle Normal
        Start-Sleep -Milliseconds 500
    }
}

Write-Host ""
Write-Host "All $PlayerCount instances launched!" -ForegroundColor Green
Write-Host ""
Write-Host "Testing Instructions:" -ForegroundColor Yellow
Write-Host "1. In the first instance, create a new game room" -ForegroundColor White
Write-Host "2. Note the Room # displayed" -ForegroundColor White
Write-Host "3. In other instances, join using that Room #" -ForegroundColor White
Write-Host "4. Once all players have joined, click 'Start Game' in the owner's instance" -ForegroundColor White
