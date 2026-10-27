// Application Insights (workspace-based) for API telemetry.

@description('Azure region.')
param location string

@description('Application Insights component name.')
param appInsightsName string

@description('Log Analytics workspace that stores the telemetry.')
param logAnalyticsWorkspaceId string

@description('Tags applied to the resources.')
param tags object = {}

resource appInsights 'Microsoft.Insights/components@2020-02-02' = {
  name: appInsightsName
  location: location
  kind: 'web'
  tags: tags
  properties: {
    Application_Type: 'web'
    WorkspaceResourceId: logAnalyticsWorkspaceId
  }
}

@description('Connection string injected into the API as APPLICATIONINSIGHTS_CONNECTION_STRING.')
output connectionString string = appInsights.properties.ConnectionString
