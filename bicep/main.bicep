@description('Storage Account type')
@allowed([
  'Standard_LRS'
  'Standard_GRS'
  'Standard_RAGRS'
])
param storageAccountType string = 'Standard_LRS'

@description('Location for all resources.')
param location string = resourceGroup().location

@description('Location for Application Insights')
param appInsightsLocation string = resourceGroup().location

@description('The language worker runtime to load in the function app.')
@allowed([
  'dotnet-isolated'
])
param runtime string = 'dotnet-isolated'

@secure()
param mastadonToken string

@description('Enable only after the original Consumption app has been stopped. Both apps share Durable state.')
param monitorEnabled bool = false

@description('Existing Durable task hub name. Verify against the source app before cutover.')
var durableTaskHubName = 'funccfsalertsprodaustraliasoutheast'

var functionAppName = 'func-cfsalerts-prod-australiasoutheast-flex'
var hostingPlanName = 'plan-cfsalerts-prod-australiasoutheast-flex'
var applicationInsightsName = 'appi-cfsalerts-prod-australiasoutheast'
var storageAccountName = 'stcfsalertsprodause'
var deploymentContainerName = 'app-packages-cfsalerts-flex'

// https://learn.microsoft.com/azure/templates/microsoft.storage/storageaccounts?WT.mc_id=DOP-MVP-5001655
resource storageAccount 'Microsoft.Storage/storageAccounts@2022-05-01' = {
  name: storageAccountName
  location: location
  sku: {
    name: storageAccountType
  }
  kind: 'StorageV2'
  properties: {
    supportsHttpsTrafficOnly: true
    defaultToOAuthAuthentication: true
    allowBlobPublicAccess: false
    minimumTlsVersion: 'TLS1_2'
  }
}

// https://learn.microsoft.com/azure/templates/microsoft.web/serverfarms?WT.mc_id=DOP-MVP-5001655
resource hostingPlan 'Microsoft.Web/serverfarms@2024-04-01' = {
  name: hostingPlanName
  location: location
  sku: {
    name: 'FC1'
    tier: 'FlexConsumption'
  }
  kind: 'functionapp'
  properties: {
    reserved: true
  }
}

resource deploymentBlobService 'Microsoft.Storage/storageAccounts/blobServices@2022-05-01' existing = {
  parent: storageAccount
  name: 'default'
}

resource deploymentContainer 'Microsoft.Storage/storageAccounts/blobServices/containers@2022-05-01' = {
  parent: deploymentBlobService
  name: deploymentContainerName
  properties: {
    publicAccess: 'None'
  }
}

// https://learn.microsoft.com/azure/templates/microsoft.web/sites?WT.mc_id=DOP-MVP-5001655
resource functionApp 'Microsoft.Web/sites@2024-04-01' = {
  name: functionAppName
  location: location
  kind: 'functionapp,linux'
  identity: {
    type: 'SystemAssigned'
  }

  properties: {
    serverFarmId: hostingPlan.id
    reserved: true
    functionAppConfig: {
      deployment: {
        storage: {
          type: 'blobContainer'
          value: '${storageAccount.properties.primaryEndpoints.blob}${deploymentContainer.name}'
          authentication: {
            type: 'StorageAccountConnectionString'
            storageAccountConnectionStringName: 'AzureWebJobsStorage'
          }
        }
      }
      runtime: {
        name: runtime
        version: '8.0'
      }
      scaleAndConcurrency: {
        instanceMemoryMB: 512
        maximumInstanceCount: 40
      }
    }
    siteConfig: {
      appSettings: [
        {
          name: 'AzureWebJobsStorage'
          value: 'DefaultEndpointsProtocol=https;AccountName=${storageAccountName};EndpointSuffix=${environment().suffixes.storage};AccountKey=${storageAccount.listKeys().keys[0].value}'
        }
        {
          name: 'APPLICATIONINSIGHTS_CONNECTION_STRING'
          value: applicationInsights.properties.ConnectionString
        }
        {
          name: 'AzureFunctionsJobHost__extensions__durableTask__hubName'
          value: durableTaskHubName
        }
        {
          name: 'AzureFunctionsWebHost__hostid'
          value: 'cfsalerts-prod-flex'
        }
        {
          name: 'AzureWebJobs.MonitorJobStatus.Disabled'
          value: string(!monitorEnabled)
        }
        {
          name: 'AzureWebJobs.CheckAlerts.Disabled'
          value: string(!monitorEnabled)
        }
        {
          name: 'AzureWebJobs.StartOrchestration.Disabled'
          value: string(!monitorEnabled)
        }
        {
          name: 'AzureWebJobs.StopOrchestrations.Disabled'
          value: string(!monitorEnabled)
        }
        {
          name: 'MastodonSettings__token'
          value: mastadonToken
        }
        {
          name: 'MastodonSettings__instance'
          value: 'mastodon.online'
        }
      ]

      minTlsVersion: '1.2'
      http20Enabled: true
    }
    httpsOnly: true
  }
}

@description('The name of the function app.')
output functionAppName string = functionApp.name

@description('Whether deployment may invoke StartOrchestration.')
output monitorEnabled bool = monitorEnabled

// https://learn.microsoft.com/azure/templates/microsoft.insights/components?WT.mc_id=DOP-MVP-5001655
resource applicationInsights 'Microsoft.Insights/components@2020-02-02' = {
  name: applicationInsightsName
  location: appInsightsLocation
  kind: 'web'
  properties: {
    Application_Type: 'web'
    Flow_Type: 'Bluefield'
    Request_Source: 'rest'
    WorkspaceResourceId: logAnalyticsWorkspace.id
  }
}

// https://learn.microsoft.com/azure/templates/microsoft.operationalinsights/workspaces?WT.mc_id=DOP-MVP-5001655
resource logAnalyticsWorkspace 'Microsoft.OperationalInsights/workspaces@2022-10-01' = {
  name: 'law-cfsalerts-prod-australiasoutheast'
  location: location
  properties: {
    sku: {
      name: 'PerGB2018'
    }
    retentionInDays: 30
  }
}
