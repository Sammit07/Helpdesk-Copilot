param prefix string
param environment string
param location string
param openAiEndpoint string
param openAiKey string
param searchEndpoint string
param searchKey string
param sqlConnectionString string
param appInsightsConnectionString string

var isProd = environment == 'prod'
var planSku = isProd ? 'P1v3' : 'B1'
var planTier = isProd ? 'PremiumV3' : 'Basic'

// ── App Service Plan ──────────────────────────────────────────────────────

resource appServicePlan 'Microsoft.Web/serverfarms@2023-01-01' = {
  name: '${prefix}-plan-${environment}'
  location: location
  sku: {
    name: planSku
    tier: planTier
  }
  kind: 'linux'
  properties: {
    reserved: true
  }
}

// ── API App ───────────────────────────────────────────────────────────────

resource apiApp 'Microsoft.Web/sites@2023-01-01' = {
  name: '${prefix}-api-${environment}'
  location: location
  identity: {
    type: 'SystemAssigned'
  }
  properties: {
    serverFarmId: appServicePlan.id
    siteConfig: {
      linuxFxVersion: 'DOTNETCORE|8.0'
      alwaysOn: isProd
      appSettings: [
        { name: 'ASPNETCORE_ENVIRONMENT', value: isProd ? 'Production' : 'Development' }
        { name: 'AzureOpenAI__Endpoint', value: openAiEndpoint }
        { name: 'AzureOpenAI__ApiKey', value: openAiKey }
        { name: 'AzureOpenAI__DeploymentName', value: 'gpt-4o' }
        { name: 'AzureOpenAI__EmbeddingDeploymentName', value: 'text-embedding-3-small' }
        { name: 'AzureSearch__Endpoint', value: searchEndpoint }
        { name: 'AzureSearch__ApiKey', value: searchKey }
        { name: 'AzureSearch__IndexName', value: 'helpdesk-knowledge' }
        { name: 'ApplicationInsights__ConnectionString', value: appInsightsConnectionString }
        { name: 'ConnectionStrings__DefaultConnection', value: sqlConnectionString }
        { name: 'Cors__AllowedOrigins__0', value: 'https://${prefix}-web-${environment}.azurewebsites.net' }
      ]
    }
    httpsOnly: true
  }
}

// ── Web (Blazor) App ──────────────────────────────────────────────────────

resource webApp 'Microsoft.Web/sites@2023-01-01' = {
  name: '${prefix}-web-${environment}'
  location: location
  identity: {
    type: 'SystemAssigned'
  }
  properties: {
    serverFarmId: appServicePlan.id
    siteConfig: {
      linuxFxVersion: 'DOTNETCORE|8.0'
      alwaysOn: isProd
      appSettings: [
        { name: 'ASPNETCORE_ENVIRONMENT', value: isProd ? 'Production' : 'Development' }
        { name: 'ApiBaseUrl', value: 'https://${prefix}-api-${environment}.azurewebsites.net/' }
        { name: 'ApplicationInsights__ConnectionString', value: appInsightsConnectionString }
      ]
    }
    httpsOnly: true
  }
}

output apiUrl string = 'https://${apiApp.properties.defaultHostName}'
output webUrl string = 'https://${webApp.properties.defaultHostName}'
output apiAppName string = apiApp.name
output webAppName string = webApp.name
