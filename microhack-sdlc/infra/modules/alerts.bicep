// Azure Monitor metric alerts on the API container app.
// They fire into Azure Monitor; the SRE Agent picks up fired alerts/incidents.

@description('Name of the existing API container app.')
param apiAppName string

@description('Alert name prefix.')
param prefix string

@description('Tags applied to the alerts.')
param tags object = {}

resource apiApp 'Microsoft.App/containerApps@2024-03-01' existing = {
  name: apiAppName
}

resource http5xx 'Microsoft.Insights/metricAlerts@2018-03-01' = {
  name: '${prefix}-api-5xx'
  location: 'global'
  tags: tags
  properties: {
    description: 'API is returning HTTP 5xx responses.'
    severity: 2
    enabled: true
    scopes: [apiApp.id]
    evaluationFrequency: 'PT1M'
    windowSize: 'PT5M'
    criteria: {
      'odata.type': 'Microsoft.Azure.Monitor.SingleResourceMultipleMetricCriteria'
      allOf: [
        {
          name: 'Http5xx'
          criterionType: 'StaticThresholdCriterion'
          metricName: 'Requests'
          metricNamespace: 'microsoft.app/containerapps'
          operator: 'GreaterThan'
          threshold: 2
          timeAggregation: 'Total'
          dimensions: [
            {
              name: 'statusCodeCategory'
              operator: 'Include'
              values: ['5xx']
            }
          ]
        }
      ]
    }
  }
}

resource restarts 'Microsoft.Insights/metricAlerts@2018-03-01' = {
  name: '${prefix}-api-restarts'
  location: 'global'
  tags: tags
  properties: {
    description: 'API container is restarting (crash loop or failing probes).'
    severity: 2
    enabled: true
    scopes: [apiApp.id]
    evaluationFrequency: 'PT1M'
    windowSize: 'PT5M'
    criteria: {
      'odata.type': 'Microsoft.Azure.Monitor.SingleResourceMultipleMetricCriteria'
      allOf: [
        {
          name: 'Restarts'
          criterionType: 'StaticThresholdCriterion'
          metricName: 'RestartCount'
          metricNamespace: 'microsoft.app/containerapps'
          operator: 'GreaterThan'
          threshold: 0
          timeAggregation: 'Total'
        }
      ]
    }
  }
}
