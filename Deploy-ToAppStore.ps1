# Deploy to iOS App Store Script
# ================================
# This script builds and optionally uploads your .NET MAUI iOS app to App Store Connect
# NOTE: This script must be run on macOS with Xcode installed

param(
    [Parameter(Mandatory=$false)]
    [string]$Configuration = "Release",
    
    [Parameter(Mandatory=$false)]
    [string]$CodesignKey = "Apple Distribution",
    
    [Parameter(Mandatory=$false)]
    [string]$CodesignProvision = "",
    
    [Parameter(Mandatory=$false)]
    [switch]$BuildOnly,
    
    [Parameter(Mandatory=$false)]
    [string]$VersionCode,
    
    [Parameter(Mandatory=$false)]
    [string]$VersionName,
    
    [Parameter(Mandatory=$false)]
    [string]$AppleId = "",
    
    [Parameter(Mandatory=$false)]
    [string]$AppSpecificPassword = "",
    
    [Parameter(Mandatory=$false)]
    [string]$TeamId = ""
)

$ErrorActionPreference = "Stop"
$ProjectPath = "PotatoVillage/PotatoVillage.csproj"

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "  iOS App Store Deployment Script" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan

# Check if running on macOS
if (-not $IsMacOS) {
    Write-Host "ERROR: This script must be run on macOS with Xcode installed." -ForegroundColor Red
    Write-Host "iOS builds require a Mac with Xcode and valid Apple Developer certificates." -ForegroundColor Yellow
    exit 1
}

# Step 1: Verify Xcode is installed
Write-Host "`n[1/5] Verifying Xcode installation..." -ForegroundColor Yellow
try {
    $xcodeVersion = xcodebuild -version 2>&1
    Write-Host "Xcode found: $($xcodeVersion[0])" -ForegroundColor Green
} catch {
    Write-Host "ERROR: Xcode is not installed or not properly configured." -ForegroundColor Red
    Write-Host "Please install Xcode from the Mac App Store." -ForegroundColor Yellow
    exit 1
}

# Step 2: Verify .NET MAUI workload
Write-Host "`n[2/5] Verifying .NET MAUI iOS workload..." -ForegroundColor Yellow
$workloads = dotnet workload list 2>&1
if ($workloads -notmatch "maui-ios|ios") {
    Write-Host "WARNING: iOS workload may not be installed. Installing..." -ForegroundColor Yellow
    dotnet workload install maui-ios
}
Write-Host ".NET MAUI iOS workload verified." -ForegroundColor Green

# Step 3: Build the IPA
Write-Host "`n[3/5] Building iOS App (.ipa)..." -ForegroundColor Yellow

$buildArgs = @(
    "publish",
    $ProjectPath,
    "-f", "net9.0-ios",
    "-c", $Configuration
)

# Add code signing if provided
if ($CodesignKey) {
    $buildArgs += "-p:CodesignKey=`"$CodesignKey`""
}

if ($CodesignProvision) {
    $buildArgs += "-p:CodesignProvision=`"$CodesignProvision`""
}

# Add version if specified
if ($VersionCode) {
    $buildArgs += "-p:ApplicationVersion=$VersionCode"
}
if ($VersionName) {
    $buildArgs += "-p:ApplicationDisplayVersion=$VersionName"
}

# Required for App Store builds
$buildArgs += "-p:ArchiveOnBuild=true"
$buildArgs += "-p:RuntimeIdentifier=ios-arm64"

Write-Host "Running: dotnet $($buildArgs -join ' ')" -ForegroundColor Gray
& dotnet @buildArgs

if ($LASTEXITCODE -ne 0) {
    Write-Host "Build failed!" -ForegroundColor Red
    exit 1
}

Write-Host "Build successful!" -ForegroundColor Green

# Find the output IPA
$outputDir = "PotatoVillage/bin/$Configuration/net9.0-ios/ios-arm64/publish"
$ipaFile = Get-ChildItem -Path $outputDir -Filter "*.ipa" -Recurse -ErrorAction SilentlyContinue | Select-Object -First 1

if ($ipaFile) {
    Write-Host "`nIPA file created: $($ipaFile.FullName)" -ForegroundColor Green
    Write-Host "File size: $([math]::Round($ipaFile.Length / 1MB, 2)) MB" -ForegroundColor White
} else {
    # Check for .app bundle
    $appBundle = Get-ChildItem -Path $outputDir -Filter "*.app" -Recurse -ErrorAction SilentlyContinue | Select-Object -First 1
    if ($appBundle) {
        Write-Host "`nApp bundle created: $($appBundle.FullName)" -ForegroundColor Green
        Write-Host "Note: For App Store submission, you need to create an IPA or use Xcode to archive." -ForegroundColor Yellow
    } else {
        Write-Host "Warning: Could not locate IPA or app bundle in $outputDir" -ForegroundColor Yellow
    }
}

# Step 4: Upload to App Store Connect (if credentials provided)
Write-Host "`n[4/5] Upload to App Store Connect..." -ForegroundColor Yellow

if ($BuildOnly) {
    Write-Host "Build-only mode. Skipping upload." -ForegroundColor Yellow
} elseif ($AppleId -and $AppSpecificPassword -and $ipaFile) {
    Write-Host "Uploading to App Store Connect..." -ForegroundColor Cyan
    
    $uploadArgs = @(
        "altool",
        "--upload-app",
        "-f", $ipaFile.FullName,
        "-t", "ios",
        "-u", $AppleId,
        "-p", $AppSpecificPassword
    )
    
    if ($TeamId) {
        $uploadArgs += "--asc-provider"
        $uploadArgs += $TeamId
    }
    
    Write-Host "Running: xcrun $($uploadArgs -join ' ')" -ForegroundColor Gray
    & xcrun @uploadArgs
    
    if ($LASTEXITCODE -eq 0) {
        Write-Host "Upload successful!" -ForegroundColor Green
    } else {
        Write-Host "Upload failed. Please check your credentials and try again." -ForegroundColor Red
    }
} else {
    Write-Host "Skipping upload - credentials not provided or IPA not found." -ForegroundColor Yellow
}

# Step 5: Instructions
Write-Host "`n[5/5] Next Steps..." -ForegroundColor Yellow

Write-Host @"

========================================
  MANUAL UPLOAD INSTRUCTIONS
========================================

1. Open Xcode and go to: Window > Organizer

2. Or use Transporter app from the Mac App Store

3. Go to App Store Connect: https://appstoreconnect.apple.com

4. Select your app or create a new one:
   - Bundle ID: com.biwuenterprise.potatovillage
   - App name: 土豆天天村

5. Upload your IPA file:

"@ -ForegroundColor Cyan

if ($ipaFile) {
    Write-Host "   $($ipaFile.FullName)" -ForegroundColor White
}

Write-Host @"

6. Complete app metadata, screenshots, and submit for review

========================================
  AUTOMATED UPLOAD (Optional)
========================================

To automate uploads, use this command:

  xcrun altool --upload-app `
    -f "path/to/your.ipa" `
    -t ios `
    -u "your-apple-id@email.com" `
    -p "app-specific-password" `
    --asc-provider "TEAM_ID"

Generate an app-specific password at:
  https://appleid.apple.com/account/manage

========================================
  REQUIREMENTS CHECKLIST
========================================

[ ] Apple Developer Program membership ($99/year)
[ ] Distribution certificate in Keychain
[ ] App Store provisioning profile
[ ] App created in App Store Connect
[ ] App icons (1024x1024) and screenshots
[ ] Privacy policy URL
[ ] App description and keywords

"@ -ForegroundColor Cyan

Write-Host "`n========================================" -ForegroundColor Green
Write-Host "  iOS deployment preparation complete!" -ForegroundColor Green
Write-Host "========================================" -ForegroundColor Green
