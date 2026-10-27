// Generic Container App module reused for both the api and the frontend.
// Pulls from ACR with a user-assigned managed identity (no registry secrets).

@description('Azure region for the app.')
param location string

@description('Name of the container app.')
param appName string

@description('Resource ID of the Container Apps managed environment.')
param environmentId string

@description('Fully-qualified image reference, e.g. myregistry.azurecr.io/api:abc123.')
param image string

@description('The port the container listens on (api=3000, frontend=80).')
param targetPort int

@description('Whether ingress is exposed publicly.')
param externalIngress bool = true

@description('Login server of the ACR the image is pulled from.')
param registryLoginServer string

@description('Resource ID of the user-assigned managed identity with AcrPull on the registry.')
param userAssignedIdentityId string

@description('Environment variables. Use { name, secretRef } to reference an app secret.')
param env array = []

@description('Name of an optional app secret (referenced from env via secretRef).')
param secretName string = ''

@description('Value of the optional app secret. Skipped when empty.')
@secure()
param secretValue string = ''

@description('Optional KEDA/HTTP scale rules.')
param scaleRules array = []

@minValue(0)
param minReplicas int = 1

@minValue(1)
param maxReplicas int = 1

param cpu string = '0.5'
param memory string = '1.0Gi'

@description('Path used by startup/liveness/readiness probes.')
param probePath string = '/'

@description('Tags applied to the app.')
param tags object = {}

resource containerApp 'Microsoft.App/containerApps@2024-03-01' = {
  name: appName
  location: location
  tags: tags
  identity: {
    type: 'UserAssigned'
    userAssignedIdentities: {
      '${userAssignedIdentityId}': {}
    }
  }
  properties: {
    managedEnvironmentId: environmentId
    configuration: {
      ingress: {
        external: externalIngress
        targetPort: targetPort
        transport: 'auto'
        allowInsecure: false
        traffic: [
          {
            weight: 100
            latestRevision: true
          }
        ]
      }
      registries: [
        {
          server: registryLoginServer
          identity: userAssignedIdentityId
        }
      ]
      secrets: empty(secretValue) ? [] : [
        {
          name: secretName
          value: secretValue
        }
      ]
    }
    template: {
      containers: [
        {
          name: appName
          image: image
          resources: {
            cpu: json(cpu)
            memory: memory
          }
          env: env
          probes: [
            {
              type: 'Startup'
              httpGet: {
                path: probePath
                port: targetPort
              }
              periodSeconds: 5
              failureThreshold: 24
            }
            {
              type: 'Liveness'
              httpGet: {
                path: probePath
                port: targetPort
              }
              periodSeconds: 30
              failureThreshold: 3
            }
            {
              type: 'Readiness'
              httpGet: {
                path: probePath
                port: targetPort
              }
              periodSeconds: 10
              failureThreshold: 3
            }
          ]
        }
      ]
      scale: {
        minReplicas: minReplicas
        maxReplicas: maxReplicas
        rules: scaleRules
      }
    }
  }
}

@description('The fully-qualified domain name of the app.')
output fqdn string = containerApp.properties.configuration.ingress.fqdn

@description('The name of the container app.')
output appName string = containerApp.name
