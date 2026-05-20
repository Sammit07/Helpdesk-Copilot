@description('Deployment environment')
@allowed(['dev', 'staging', 'prod'])
param environment string = 'dev'

@description('Azure region for most resources')
param location string = resourceGroup().location

@description('Resource name prefix')
param prefix string = 'helpdesk'

@description('Azure region for Azure OpenAI (limited availability)')
param openAiLocation string = 'eastus'

@description('SQL administrator login')
param sqlAdminLogin string = 'helpdeskadmin'

@secure()
@description('SQL administrator password')
param sqlAdminPassword string

// ── Modules ────────────────────────────────────────────────────────────────

module logAnalytics 'modules/appinsights.bicep' = {
  name: 'logAnalytics-${environment}'
  params: {
    prefix: prefix
    environment: environment
    location: location
  }
}

module openAi 'modules/openai.bicep' = {
  name: 'openai-${environment}'
  params: {
    prefix: prefix
    environment: environment
    location: openAiLocation
  }
}

module search 'modules/search.bicep' = {
  name: 'search-${environment}'
  params: {
    prefix: prefix
    environment: environment
    location: location
  }
}

module sql 'modules/sql.bicep' = {
  name: 'sql-${environment}'
  params: {
    prefix: prefix
    environment: environment
    location: location
    adminLogin: sqlAdminLogin
    adminPassword: sqlAdminPassword
  }
}

module appService 'modules/appservice.bicep' = {
  name: 'appservice-${environment}'
  params: {
    prefix: prefix
    environment: environment
    location: location
    openAiEndpoint: openAi.outputs.endpoint
    openAiKey: openAi.outputs.key
    searchEndpoint: search.outputs.endpoint
    searchKey: search.outputs.key
    sqlConnectionString: sql.outputs.connectionString
    appInsightsConnectionString: logAnalytics.outputs.appInsightsConnectionString
  }
}

// ── Outputs ────────────────────────────────────────────────────────────────

output apiUrl string = appService.outputs.apiUrl
output webUrl string = appService.outputs.webUrl
output openAiEndpoint string = openAi.outputs.endpoint
output searchEndpoint string = search.outputs.endpoint
output logAnalyticsWorkspaceId string = logAnalytics.outputs.workspaceId
