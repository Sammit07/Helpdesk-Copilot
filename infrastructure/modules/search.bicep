param prefix string
param environment string
param location string

var skuName = environment == 'dev' ? 'free' : 'basic'

resource searchService 'Microsoft.Search/searchServices@2023-11-01' = {
  name: '${prefix}-search-${environment}'
  location: location
  sku: {
    name: skuName
  }
  properties: {
    replicaCount: 1
    partitionCount: 1
    hostingMode: 'default'
    publicNetworkAccess: 'enabled'
    semanticSearch: environment == 'dev' ? 'disabled' : 'standard'
  }
}

// Note: Index creation is handled by the application on startup via IRagService.
// The Bicep creates the service; the app creates the index schema.

output endpoint string = 'https://${searchService.name}.search.windows.net'
output key string = searchService.listAdminKeys().primaryKey
output serviceName string = searchService.name
