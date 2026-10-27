// Octocat Supply on Azure Container Apps.
//
//   ACR ──images──> ┌──────── Container Apps Environment ────────┐
//                   │  frontend (nginx :80, public)               │
//                   │  api (.NET :3000, public, 1 replica)        │
//                   └─────────────────────────────────────────────┘
//                                    │ logs
//                            Log Analytics workspace
//
// The browser calls the API directly (the frontend writes runtime-config.js
// with API_URL at container start), so the api needs public ingress. CORS on
// the api is locked to the frontend origin via API_CORS_ORIGINS.
//
// Deployed in two phases by the workflow (images live in the ACR created here):
//   1. deployApps=false  -> identity, ACR (+AcrPull), logs, environment
//   2. deployApps=true   -> api + frontend apps pointing at the pushed images

targetScope = 'resourceGroup'

@description('Azure region for all resources. Defaults to the resource group location.')
param location string = resourceGroup().location

@description('Short prefix used to derive resource names (lowercase letters/numbers).')
@minLength(2)
@maxLength(12)
param namePrefix string = 'octocat'

@description('Environment name / suffix (e.g. dev, test). Keeps names unique per env.')
param environmentName string = 'dev'

@description('Create the container apps. False on the first (bootstrap) phase.')
param deployApps bool = true

@description('Container image for the API (e.g. <acr>.azurecr.io/api:<tag>).')
param apiImage string = ''

@description('Container image for the frontend (e.g. <acr>.azurecr.io/frontend:<tag>).')
param frontendImage string = ''

@description('Frontend replica bounds. The api is pinned to one replica (SQLite).')
@minValue(0)
param frontendMinReplicas int = 1

@minValue(1)
param frontendMaxReplicas int = 3

@description('Payment provider: demo (no secrets) or stripe.')
@allowed([
  'demo'
  'stripe'
])
param paymentProvider string = 'demo'

@description('Stripe secret key. Stored as a Container App secret. Required when paymentProvider is stripe.')
@secure()
param paymentApiKey string = ''

@description('Stripe publishable key (public by design).')
param paymentPublishableKey string = ''

var baseName = '${namePrefix}-${environmentName}'
var registryName = toLower(replace('${namePrefix}${environmentName}acr${take(uniqueString(resourceGroup().id), 6)}', '-', ''))
var logAnalyticsName = '${baseName}-logs'
var environmentResourceName = '${baseName}-env'
var identityName = '${baseName}-pull-id'
var apiAppName = '${baseName}-api'
var frontendAppName = '${baseName}-frontend'

var commonTags = {
  project: 'octocat-supply'
  managedBy: 'bicep'
  environment: environmentName
}

resource pullIdentity 'Microsoft.ManagedIdentity/userAssignedIdentities@2023-01-31' = {
  name: identityName
  location: location
  tags: commonTags
}

module registry 'modules/registry.bicep' = {
  name: 'registry'
  params: {
    location: location
    registryName: registryName
    pullPrincipalId: pullIdentity.properties.principalId
    tags: commonTags
  }
}

module logAnalytics 'modules/loganalytics.bicep' = {
  name: 'loganalytics'
  params: {
    location: location
    workspaceName: logAnalyticsName
    tags: commonTags
  }
}

module environment 'modules/containerapp-env.bicep' = {
  name: 'containerapp-env'
  params: {
    location: location
    environmentName: environmentResourceName
    logAnalyticsWorkspaceId: logAnalytics.outputs.workspaceId
    tags: commonTags
  }
}

// Predictable hostnames avoid a circular reference between the two apps.
var apiHost = '${apiAppName}.${environment.outputs.defaultDomain}'
var frontendHost = '${frontendAppName}.${environment.outputs.defaultDomain}'

var apiEnv = concat([
  {
    name: 'PORT'
    value: '3000'
  }
  {
    name: 'DB_FILE'
    value: '/app/data/app.db'
  }
  {
    name: 'API_CORS_ORIGINS'
    value: 'https://${frontendHost}'
  }
  {
    name: 'PAYMENT_PROVIDER'
    value: paymentProvider
  }
  {
    name: 'PAYMENT_PUBLISHABLE_KEY'
    value: paymentPublishableKey
  }
], empty(paymentApiKey) ? [] : [
  {
    name: 'PAYMENT_API_KEY'
    secretRef: 'payment-api-key'
  }
])

// API: public ingress (browser calls it directly). SQLite is local to the
// container, so it is pinned to exactly one replica and its data is ephemeral.
module apiApp 'modules/containerapp.bicep' = if (deployApps) {
  name: 'api-app'
  params: {
    location: location
    appName: apiAppName
    environmentId: environment.outputs.environmentId
    image: apiImage
    targetPort: 3000
    externalIngress: true
    registryLoginServer: registry.outputs.loginServer
    userAssignedIdentityId: pullIdentity.id
    minReplicas: 1
    maxReplicas: 1
    secretName: 'payment-api-key'
    secretValue: paymentApiKey
    env: apiEnv
    tags: commonTags
  }
}

module frontendApp 'modules/containerapp.bicep' = if (deployApps) {
  name: 'frontend-app'
  params: {
    location: location
    appName: frontendAppName
    environmentId: environment.outputs.environmentId
    image: frontendImage
    targetPort: 80
    externalIngress: true
    registryLoginServer: registry.outputs.loginServer
    userAssignedIdentityId: pullIdentity.id
    minReplicas: frontendMinReplicas
    maxReplicas: frontendMaxReplicas
    cpu: '0.25'
    memory: '0.5Gi'
    scaleRules: [
      {
        name: 'http-concurrency'
        http: {
          metadata: {
            concurrentRequests: '50'
          }
        }
      }
    ]
    env: [
      {
        name: 'API_HOST'
        value: apiHost
      }
      {
        name: 'API_PORT'
        value: '443'
      }
      {
        name: 'API_PROTOCOL'
        value: 'https'
      }
    ]
    tags: commonTags
  }
}

@description('ACR name — the workflow pushes images here between the two phases.')
output acrName string = registry.outputs.registryName

@description('ACR login server.')
output acrLoginServer string = registry.outputs.loginServer

@description('Public URL of the frontend app.')
output frontendUrl string = 'https://${frontendHost}'

@description('Public URL of the api app.')
output apiUrl string = 'https://${apiHost}'
