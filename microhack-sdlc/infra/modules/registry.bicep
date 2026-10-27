// Azure Container Registry holding the api + frontend images.
// Admin user is disabled; the apps pull with a user-assigned managed identity
// that is granted AcrPull here.

@description('Azure region for the registry.')
param location string

@description('Globally-unique name for the container registry (alphanumeric, 5-50 chars).')
param registryName string

@description('Principal ID of the managed identity that pulls images (granted AcrPull).')
param pullPrincipalId string

@description('Tags applied to the registry.')
param tags object = {}

// Basic is enough for two small images; no geo-replication/private link needed.
@allowed([
  'Basic'
  'Standard'
  'Premium'
])
param sku string = 'Basic'

var acrPullRoleId = '7f951dda-4ed3-4680-a7ca-43fe172d538d'

resource registry 'Microsoft.ContainerRegistry/registries@2023-11-01-preview' = {
  name: registryName
  location: location
  tags: tags
  sku: {
    name: sku
  }
  properties: {
    adminUserEnabled: false
    anonymousPullEnabled: false
  }
}

resource acrPull 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(registry.id, pullPrincipalId, acrPullRoleId)
  scope: registry
  properties: {
    principalId: pullPrincipalId
    principalType: 'ServicePrincipal'
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', acrPullRoleId)
  }
}

@description('The login server host name of the registry (e.g. myregistry.azurecr.io).')
output loginServer string = registry.properties.loginServer

@description('The resource ID of the registry.')
output registryId string = registry.id

@description('The name of the registry.')
output registryName string = registry.name
