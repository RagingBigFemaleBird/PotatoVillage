# Deploy PotatoVillage Server to Azure App Service
# This script deploys the infrastructure and publishes the application

param(
    [string]$ResourceGroupName = "pv-resource-group",
    [string]$Location = "eastus",
    [string]$WebAppName = "potatovillage-server",
    [string]$AppServicePlanSku = "B1"
)

$ErrorActionPreference = "Stop"

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "PotatoVillage Server Azure Deployment" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

# Check if logged in to Azure
Write-Host "Checking Azure login status..." -ForegroundColor Yellow
$account = az account show 2>$null | ConvertFrom-Json
if (-not $account) {
    Write-Host "Not logged in to Azure. Please run 'az login' first." -ForegroundColor Red
    exit 1
}
Write-Host "Logged in as: $($account.user.name)" -ForegroundColor Green
Write-Host "Subscription: $($account.name)" -ForegroundColor Green
Write-Host ""

# Create resource group if it doesn't exist
Write-Host "Creating resource group '$ResourceGroupName' in '$Location'..." -ForegroundColor Yellow
az group create --name $ResourceGroupName --location $Location --output none
Write-Host "Resource group ready." -ForegroundColor Green
Write-Host ""

# Validate the Bicep deployment
Write-Host "Validating Bicep deployment..." -ForegroundColor Yellow
$validation = az deployment group validate `
    --resource-group $ResourceGroupName `
    --template-file infra/main.bicep `
    --parameters webAppName=$WebAppName appServicePlanSku=$AppServicePlanSku `
    2>&1

if ($LASTEXITCODE -ne 0) {
    Write-Host "Validation failed:" -ForegroundColor Red
    Write-Host $validation
    exit 1
}
Write-Host "Validation successful." -ForegroundColor Green
Write-Host ""

# Preview the deployment
Write-Host "Previewing deployment changes..." -ForegroundColor Yellow
az deployment group what-if `
    --resource-group $ResourceGroupName `
    --template-file infra/main.bicep `
    --parameters webAppName=$WebAppName appServicePlanSku=$AppServicePlanSku
Write-Host ""

# Confirm deployment
$confirm = Read-Host "Do you want to proceed with the deployment? (y/n)"
if ($confirm -ne "y" -and $confirm -ne "Y") {
    Write-Host "Deployment cancelled." -ForegroundColor Yellow
    exit 0
}

# Deploy infrastructure
Write-Host ""
Write-Host "Deploying Azure infrastructure..." -ForegroundColor Yellow
$deployment = az deployment group create `
    --resource-group $ResourceGroupName `
    --template-file infra/main.bicep `
    --parameters webAppName=$WebAppName appServicePlanSku=$AppServicePlanSku `
    --query "properties.outputs" `
    --output json | ConvertFrom-Json

$webAppUrl = $deployment.webAppUrl.value
$deployedWebAppName = $deployment.webAppName.value
$signalRHubUrl = $deployment.signalRHubUrl.value

Write-Host "Infrastructure deployed successfully!" -ForegroundColor Green
Write-Host ""

# Build and publish the application
Write-Host "Building Server project..." -ForegroundColor Yellow
dotnet publish Server/Server.csproj -c Release -o ./publish
if ($LASTEXITCODE -ne 0) {
    Write-Host "Build failed!" -ForegroundColor Red
    exit 1
}
Write-Host "Build successful." -ForegroundColor Green
Write-Host ""

# Create deployment package
Write-Host "Creating deployment package..." -ForegroundColor Yellow
$zipPath = "./publish.zip"
if (Test-Path $zipPath) {
    Remove-Item $zipPath
}
Compress-Archive -Path ./publish/* -DestinationPath $zipPath -Force
Write-Host "Package created." -ForegroundColor Green
Write-Host ""

# Deploy the application
Write-Host "Deploying application to Azure App Service..." -ForegroundColor Yellow
az webapp deployment source config-zip `
    --resource-group $ResourceGroupName `
    --name $deployedWebAppName `
    --src $zipPath
Write-Host "Application deployed!" -ForegroundColor Green
Write-Host ""

# Clean up
Write-Host "Cleaning up temporary files..." -ForegroundColor Yellow
Remove-Item -Path ./publish -Recurse -Force
Remove-Item -Path $zipPath -Force
Write-Host ""

# Output results
Write-Host "========================================" -ForegroundColor Green
Write-Host "Deployment Complete!" -ForegroundColor Green
Write-Host "========================================" -ForegroundColor Green
Write-Host ""
Write-Host "Web App URL: $webAppUrl" -ForegroundColor Cyan
Write-Host "SignalR Hub URL: $signalRHubUrl" -ForegroundColor Cyan
Write-Host ""
Write-Host "Update your PotatoVillage app's Hub URL to:" -ForegroundColor Yellow
Write-Host "$signalRHubUrl" -ForegroundColor White
Write-Host ""
Write-Host "Azure Portal Resource Group:" -ForegroundColor Yellow
Write-Host "https://portal.azure.com/#@/resource/subscriptions/$($account.id)/resourceGroups/$ResourceGroupName/overview" -ForegroundColor White
