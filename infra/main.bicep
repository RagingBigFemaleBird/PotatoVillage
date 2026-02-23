// Azure App Service deployment for PotatoVillage Game Server
// This Bicep file deploys an App Service Plan and Web App for the SignalR-based game server

@description('The name of the web app')
param webAppName string = 'potatovillage-${uniqueString(resourceGroup().id)}'

@description('The location for all resources')
param location string = resourceGroup().location

@description('The SKU of the App Service Plan')
@allowed([
  'F1'   // Free tier
  'B1'   // Basic tier
  'S1'   // Standard tier
  'P1v3' // Premium tier
])
param appServicePlanSku string = 'B1'

@description('The .NET version to use')
param dotnetVersion string = 'v8.0'

// App Service Plan
resource appServicePlan 'Microsoft.Web/serverfarms@2023-12-01' = {
  name: '${webAppName}-plan'
  location: location
  sku: {
    name: appServicePlanSku
  }
  kind: 'app'
  properties: {
    reserved: false // Windows App Service
  }
}

// Web App
resource webApp 'Microsoft.Web/sites@2023-12-01' = {
  name: webAppName
  location: location
  properties: {
    serverFarmId: appServicePlan.id
    httpsOnly: true
    siteConfig: {
      netFrameworkVersion: dotnetVersion
      alwaysOn: appServicePlanSku != 'F1' // AlwaysOn not available on Free tier
      webSocketsEnabled: true // Required for SignalR
      http20Enabled: true
      minTlsVersion: '1.2'
      ftpsState: 'Disabled'
      appSettings: [
        {
          name: 'ASPNETCORE_ENVIRONMENT'
          value: 'Production'
        }
        {
          name: 'WEBSITE_RUN_FROM_PACKAGE'
          value: '1'
        }
      ]
    }
  }
}

// Output the web app URL
output webAppUrl string = 'https://${webApp.properties.defaultHostName}'
output webAppName string = webApp.name
output signalRHubUrl string = 'https://${webApp.properties.defaultHostName}/gamehub'
