# Deploy to Google Play Store Script
# ===================================
# This script builds and optionally uploads your .NET MAUI Android app to Google Play

param(
    [Parameter(Mandatory=$false)]
    [string]$KeystorePath = "potatovillage.keystore",
    
    [Parameter(Mandatory=$false)]
    [string]$KeystorePassword,
    
    [Parameter(Mandatory=$false)]
    [string]$KeyPassword,
    
    [Parameter(Mandatory=$false)]
    [switch]$CreateKeystore,
    
    [Parameter(Mandatory=$false)]
    [switch]$BuildOnly,
    
    [Parameter(Mandatory=$false)]
    [string]$VersionCode,
    
    [Parameter(Mandatory=$false)]
    [string]$VersionName
)

$ErrorActionPreference = "Stop"
$ProjectPath = "PotatoVillage/PotatoVillage.csproj"

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "  Google Play Deployment Script" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan

# Step 1: Create keystore if requested
if ($CreateKeystore) {
    Write-Host "`n[1/4] Creating new keystore..." -ForegroundColor Yellow
    
    if (Test-Path $KeystorePath) {
        Write-Host "Keystore already exists at $KeystorePath. Skipping creation." -ForegroundColor Yellow
    } else {
        Write-Host "Creating keystore at $KeystorePath..." -ForegroundColor Green
        Write-Host "You will be prompted for keystore details." -ForegroundColor White
        
        & 'C:\Program Files\Java\jdk-25.0.2\bin\keytool.exe' -genkey -v -keystore $KeystorePath -alias potatovillage -keyalg RSA -keysize 2048 -validity 10000
        
        if ($LASTEXITCODE -ne 0) {
            Write-Host "Failed to create keystore!" -ForegroundColor Red
            exit 1
        }
        Write-Host "Keystore created successfully!" -ForegroundColor Green
        Write-Host "IMPORTANT: Store this keystore and password securely. You'll need it for all future updates!" -ForegroundColor Red
    }
}

# Step 2: Validate keystore exists
Write-Host "`n[2/4] Validating keystore..." -ForegroundColor Yellow
if (-not (Test-Path $KeystorePath)) {
    Write-Host "Keystore not found at $KeystorePath" -ForegroundColor Red
    Write-Host "Run with -CreateKeystore to create one, or specify path with -KeystorePath" -ForegroundColor Yellow
    exit 1
}
Write-Host "Keystore found: $KeystorePath" -ForegroundColor Green

# Step 3: Get passwords if not provided
if (-not $KeystorePassword) {
    $securePassword = Read-Host -Prompt "Enter keystore password" -AsSecureString
    $KeystorePassword = [Runtime.InteropServices.Marshal]::PtrToStringAuto([Runtime.InteropServices.Marshal]::SecureStringToBSTR($securePassword))
}

if (-not $KeyPassword) {
    $KeyPassword = $KeystorePassword  # Often the same
}

# Step 4: Build the AAB
Write-Host "`n[3/4] Building Android App Bundle (AAB)..." -ForegroundColor Yellow

$buildArgs = @(
    "publish",
    $ProjectPath,
    "-f", "net10.0-android",
    "-c", "Release",
    "-p:AndroidKeyStore=true",
    "-p:AndroidSigningKeyStore=$((Resolve-Path $KeystorePath).Path)",
    "-p:AndroidSigningKeyAlias=potatovillage",
    "-p:AndroidSigningStorePass=$KeystorePassword",
    "-p:AndroidSigningKeyPass=$KeyPassword",
    "-p:AndroidPackageFormat=aab"
)

# Add version if specified
if ($VersionCode) {
    $buildArgs += "-p:ApplicationVersion=$VersionCode"
}
if ($VersionName) {
    $buildArgs += "-p:ApplicationDisplayVersion=$VersionName"
}

Write-Host "Running: dotnet $($buildArgs -join ' ')" -ForegroundColor Gray
& dotnet @buildArgs

if ($LASTEXITCODE -ne 0) {
    Write-Host "Build failed!" -ForegroundColor Red
    exit 1
}

Write-Host "Build successful!" -ForegroundColor Green

# Find the output AAB
$outputDir = "PotatoVillage/bin/Release/net10.0-android/publish"
$aabFile = Get-ChildItem -Path $outputDir -Filter "*.aab" -Recurse | Select-Object -First 1

if ($aabFile) {
    Write-Host "`nAAB file created: $($aabFile.FullName)" -ForegroundColor Green
    Write-Host "File size: $([math]::Round($aabFile.Length / 1MB, 2)) MB" -ForegroundColor White
} else {
    Write-Host "Warning: Could not locate AAB file in $outputDir" -ForegroundColor Yellow
}

# Step 5: Upload instructions
Write-Host "`n[4/4] Upload to Google Play..." -ForegroundColor Yellow

if ($BuildOnly) {
    Write-Host "Build-only mode. Skipping upload." -ForegroundColor Yellow
} else {
    Write-Host @"

========================================
  MANUAL UPLOAD INSTRUCTIONS
========================================

1. Go to Google Play Console: https://play.google.com/console

2. Select your app or create a new one:
   - Package name: com.biwuenterprise.potatovillage
   - App name: 土豆天天村

3. Navigate to: Release > Production (or Testing track)

4. Click "Create new release"

5. Upload your AAB file:
   $($aabFile.FullName)

6. Add release notes and submit for review

========================================
  AUTOMATED UPLOAD (Optional)
========================================

To automate uploads, install Google Play CLI:

  pip install google-play-cli

Then upload with:
  
  google-play-cli upload `
    --package-name com.biwuenterprise.potatovillage `
    --aab-file "$($aabFile.FullName)" `
    --track internal `
    --service-account-json path/to/service-account.json

"@ -ForegroundColor Cyan
}

Write-Host "`n========================================" -ForegroundColor Green
Write-Host "  Deployment preparation complete!" -ForegroundColor Green
Write-Host "========================================" -ForegroundColor Green
