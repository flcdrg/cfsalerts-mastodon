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
param runtime string = 'dotnet-isolated'

param mastadonToken string

var functionAppName = 'func-cfsalerts-prod-australiasoutheast'
var hostingPlanName = 'plan-cfsalerts-prod-australiasoutheast'
var applicationInsightsName = 'appi-cfsalerts-prod-australiasoutheast'
var storageAccountName = 'stcfsalertsprodause'
var functionWorkerRuntime = runtime

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
resource hostingPlan 'Microsoft.Web/serverfarms@2021-03-01' = {
  name: hostingPlanName
  location: location
  sku: {
    name: 'Y1'
  }
  kind: 'functionapp'
  properties: {
    reserved: true
  }
}

// https://learn.microsoft.com/azure/templates/microsoft.web/sites?WT.mc_id=DOP-MVP-5001655
resource functionApp 'Microsoft.Web/sites@2022-09-01' = {
  name: functionAppName
  location: location
  kind: 'functionapp,linux'
  identity: {
    type: 'SystemAssigned'
  }

  properties: {
    serverFarmId: hostingPlan.id
    reserved: true
    siteConfig: {
      appSettings: [
        {
          name: 'AzureWebJobsStorage'
          value: 'DefaultEndpointsProtocol=https;AccountName=${storageAccountName};EndpointSuffix=${environment().suffixes.storage};AccountKey=${storageAccount.listKeys().keys[0].value}'
        }
        {
          name: 'WEBSITE_CONTENTAZUREFILECONNECTIONSTRING'
          value: 'DefaultEndpointsProtocol=https;AccountName=${storageAccountName};EndpointSuffix=${environment().suffixes.storage};AccountKey=${storageAccount.listKeys().keys[0].value}'
        }
        {
          name: 'WEBSITE_CONTENTSHARE'
          value: toLower(functionAppName)
        }
        {
          name: 'FUNCTIONS_EXTENSION_VERSION'
          value: '~4'
        }
        {
          name: 'APPINSIGHTS_INSTRUMENTATIONKEY'
          value: applicationInsights.properties.InstrumentationKey
        }
        {
          name: 'FUNCTIONS_WORKER_RUNTIME'
          value: functionWorkerRuntime
        }
        {
          name: 'WEBSITE_USE_PLACEHOLDER_DOTNETISOLATED'
          value: '1'
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
      linuxFxVersion: 'DOTNET-ISOLATED|8.0'
      alwaysOn: false
      localMySqlEnabled: false
      netFrameworkVersion: 'v4.6'
      ftpsState: 'Disabled'
      http20Enabled: true
    }
    httpsOnly: true
  }
}

@description('The name of the function app.')
output functionAppName string = functionApp.name

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
