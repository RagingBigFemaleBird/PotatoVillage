# Deploy PotatoVillage Server to Azure Container Apps
# This script deploys the infrastructure and publishes the application
# Use -UpdateOnly to skip infrastructure deployment and just rebuild/push the container

param(
    [string]$ResourceGroupName = "pv-resource-group-eastus",
    [string]$Location = "eastus",
 [string]$ContainerAppName = "potatovillage-server",
    [switch]$UpdateOnly = $false,
    [switch]$SkipConfirmation = $false
)

$ErrorActionPreference = "Stop"

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "PotatoVillage Server Azure Deployment" -ForegroundColor Cyan
Write-Host "(Using Azure Container Apps)" -ForegroundColor Cyan
if ($UpdateOnly) {
    Write-Host "Mode: UPDATE ONLY (skip infrastructure)" -ForegroundColor Yellow
}
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

# Derive ACR name consistently
$acrName = ($ContainerAppName -replace '[^a-zA-Z0-9]', '') + "acr"
$acrName = $acrName.Substring(0, [Math]::Min($acrName.Length, 50))

if (-not $UpdateOnly) {
    # Create resource group if it doesn't exist
    Write-Host "Creating resource group '$ResourceGroupName' in '$Location'..." -ForegroundColor Yellow
    az group create --name $ResourceGroupName --location $Location --output none
    Write-Host "Resource group ready." -ForegroundColor Green
    Write-Host ""

  # Ensure Microsoft.App provider is registered
    Write-Host "Ensuring Microsoft.App provider is registered..." -ForegroundColor Yellow
    az provider register --namespace Microsoft.App --wait 2>$null
    Write-Host "Provider registered." -ForegroundColor Green
 Write-Host ""

    # Validate the Bicep deployment
    Write-Host "Validating Bicep deployment..." -ForegroundColor Yellow
$validation = az deployment group validate `
        --resource-group $ResourceGroupName `
        --template-file infra/main-containerapp.bicep `
 --parameters containerAppName=$ContainerAppName `
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
        --template-file infra/main-containerapp.bicep `
        --parameters containerAppName=$ContainerAppName
    Write-Host ""

    # Confirm deployment
    if (-not $SkipConfirmation) {
        $confirm = Read-Host "Do you want to proceed with the deployment? (y/n)"
        if ($confirm -ne "y" -and $confirm -ne "Y") {
 Write-Host "Deployment cancelled." -ForegroundColor Yellow
     exit 0
        }
    }

    # Deploy infrastructure with sample image first
    Write-Host ""
    Write-Host "Deploying Azure Container Apps infrastructure..." -ForegroundColor Yellow
    $deployment = az deployment group create `
        --resource-group $ResourceGroupName `
      --template-file infra/main-containerapp.bicep `
   --parameters containerAppName=$ContainerAppName `
        --query "properties.outputs" `
        --output json | ConvertFrom-Json

    $containerAppUrl = $deployment.containerAppUrl.value
    $deployedContainerAppName = $deployment.containerAppName.value
    $signalRHubUrl = $deployment.signalRHubUrl.value

    Write-Host "Infrastructure deployed successfully!" -ForegroundColor Green
 Write-Host ""

    # Create Azure Container Registry if it doesn't exist
    Write-Host "Checking Azure Container Registry '$acrName'..." -ForegroundColor Yellow
    $acrExists = az acr show --name $acrName --resource-group $ResourceGroupName 2>$null
    if (-not $acrExists) {
        Write-Host "Creating Azure Container Registry '$acrName'..." -ForegroundColor Yellow
        az acr create --resource-group $ResourceGroupName --name $acrName --sku Basic --admin-enabled true --output none
        Write-Host "ACR created." -ForegroundColor Green
    } else {
        Write-Host "ACR already exists." -ForegroundColor Green
    }
    Write-Host ""
} else {
    # Update-only mode: verify resources exist
    Write-Host "Verifying existing resources..." -ForegroundColor Yellow

    # Check if resource group exists
  $rgExists = az group exists --name $ResourceGroupName 2>$null
    if ($rgExists -ne "true") {
        Write-Host "Resource group '$ResourceGroupName' does not exist. Run without -UpdateOnly first." -ForegroundColor Red
        exit 1
}

    # Check if Container App exists and get its name
    $containerApp = az containerapp show --name $ContainerAppName --resource-group $ResourceGroupName 2>$null | ConvertFrom-Json
    if (-not $containerApp) {
        Write-Host "Container App '$ContainerAppName' does not exist. Run without -UpdateOnly first." -ForegroundColor Red
        exit 1
    }
    $deployedContainerAppName = $containerApp.name
    $containerAppUrl = "https://$($containerApp.properties.configuration.ingress.fqdn)"
    $signalRHubUrl = "$containerAppUrl/gamehub"

    # Check if ACR exists
    $acrExists = az acr show --name $acrName --resource-group $ResourceGroupName 2>$null
    if (-not $acrExists) {
 Write-Host "ACR '$acrName' does not exist. Run without -UpdateOnly first." -ForegroundColor Red
        exit 1
    }

    Write-Host "All resources verified." -ForegroundColor Green
    Write-Host ""
}

# Get ACR credentials
Write-Host "Getting ACR credentials..." -ForegroundColor Yellow
$acrLoginServer = az acr show --name $acrName --query loginServer --output tsv
$acrPassword = az acr credential show --name $acrName --query "passwords[0].value" --output tsv
Write-Host "ACR: $acrLoginServer" -ForegroundColor Green
Write-Host ""

# Build and push the container image
Write-Host "Building and pushing container image..." -ForegroundColor Yellow
az containerapp up --name $deployedContainerAppName --resource-group $ResourceGroupName --source .
if ($LASTEXITCODE -ne 0) {
    Write-Host "Failed to update Container App." -ForegroundColor Red
    exit 1
}
Write-Host "Container App updated!" -ForegroundColor Green
Write-Host ""

# Output results
Write-Host "========================================" -ForegroundColor Green
Write-Host "Deployment Complete!" -ForegroundColor Green
Write-Host "========================================" -ForegroundColor Green
Write-Host ""
Write-Host "Container App URL: $containerAppUrl" -ForegroundColor Cyan
Write-Host "SignalR Hub URL: $signalRHubUrl" -ForegroundColor Cyan
Write-Host ""

# Update the client's ServerDiscoveryService with the deployed URL
$discoveryServicePath = "PotatoVillage/Services/ServerDiscoveryService.cs"
if (Test-Path $discoveryServicePath) {
    Write-Host "Updating client's default server URL..." -ForegroundColor Yellow
    $content = Get-Content $discoveryServicePath -Raw
    $content = $content -replace 'public const string DefaultServerUrl = "[^"]+";', "public const string DefaultServerUrl = `"$signalRHubUrl`";"
    Set-Content $discoveryServicePath $content
    Write-Host "Client updated to use: $signalRHubUrl" -ForegroundColor Green
}
Write-Host ""
Write-Host "The client will automatically connect to:" -ForegroundColor Yellow
Write-Host "$signalRHubUrl" -ForegroundColor White
Write-Host ""
Write-Host "Azure Portal Resource Group:" -ForegroundColor Yellow
Write-Host "https://portal.azure.com/#@/resource/subscriptions/$($account.id)/resourceGroups/$ResourceGroupName/overview" -ForegroundColor White
Write-Host ""
Write-Host "To update the app in the future, run:" -ForegroundColor Yellow
Write-Host "  .\Deploy-ToAzure-ContainerApp.ps1 -UpdateOnly" -ForegroundColor White
