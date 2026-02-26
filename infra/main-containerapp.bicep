// Azure Container Apps deployment for PotatoVillage Game Server
// This Bicep file deploys a Container App for the SignalR-based game server

@description('The name of the container app')
param containerAppName string = 'potatovillage-${uniqueString(resourceGroup().id)}'

@description('The location for all resources')
param location string = resourceGroup().location

@description('Container image to deploy (leave empty for initial setup)')
param containerImage string = 'mcr.microsoft.com/dotnet/samples:aspnetapp'

// Log Analytics Workspace for Container App Environment
resource logAnalytics 'Microsoft.OperationalInsights/workspaces@2023-09-01' = {
  name: '${containerAppName}-logs'
  location: location
  properties: {
    sku: {
      name: 'PerGB2018'
    }
    retentionInDays: 30
  }
}

// Container App Environment
resource containerAppEnv 'Microsoft.App/managedEnvironments@2024-03-01' = {
  name: '${containerAppName}-env'
  location: location
  properties: {
    appLogsConfiguration: {
      destination: 'log-analytics'
      logAnalyticsConfiguration: {
        customerId: logAnalytics.properties.customerId
        sharedKey: logAnalytics.listKeys().primarySharedKey
      }
    }
  }
}

// Container App
resource containerApp 'Microsoft.App/containerApps@2024-03-01' = {
  name: containerAppName
  location: location
  properties: {
    managedEnvironmentId: containerAppEnv.id
    configuration: {
      ingress: {
        external: true
        targetPort: 8080
        transport: 'http'
        allowInsecure: false
      }
    }
    template: {
      containers: [
        {
          name: containerAppName
          image: containerImage
          resources: {
            cpu: json('0.5')
            memory: '1Gi'
          }
          env: [
            {
              name: 'ASPNETCORE_ENVIRONMENT'
              value: 'Production'
            }
            {
              name: 'ASPNETCORE_URLS'
              value: 'http://+:8080'
            }
          ]
        }
      ]
      scale: {
        minReplicas: 0
        maxReplicas: 1
      }
    }
  }
}

// Output the container app URL
output containerAppUrl string = 'https://${containerApp.properties.configuration.ingress.fqdn}'
output containerAppName string = containerApp.name
output signalRHubUrl string = 'https://${containerApp.properties.configuration.ingress.fqdn}/gamehub'
