metadata description = 'Alert rules for the web app, the database and the background jobs, plus the action group they notify.'

param location string = resourceGroup().location
param tags object = {}

param actionGroupName string
param appServiceName string
param sqlServerName string
param sqlDatabaseName string
param applicationInsightsName string

@description('Where a fired alert is emailed. Left empty, the rules still fire and show in Azure Monitor — nobody is told.')
param alertEmailAddress string = ''

@description('5xx responses over 5 minutes before the app counts as failing. Not zero: a single bad request from a crawler is not an incident.')
param http5xxThreshold int = 10

@description('Average server response time, in seconds, over 15 minutes. The marketplace search is the slow path this watches.')
param responseTimeThresholdSeconds int = 3

@description('DTU consumption, in percent, averaged over 15 minutes. Below the ceiling on purpose — the point is to see it coming.')
param dtuThresholdPercent int = 80

@description('Database storage used, in percent. A full database refuses writes, so this must fire with room to act.')
param storageThresholdPercent int = 85

resource appService 'Microsoft.Web/sites@2022-09-01' existing = {
  name: appServiceName
}

resource sqlDatabase 'Microsoft.Sql/servers/databases@2023-08-01-preview' existing = {
  name: '${sqlServerName}/${sqlDatabaseName}'
}

resource applicationInsights 'Microsoft.Insights/components@2020-02-02' existing = {
  name: applicationInsightsName
}

// Created even with no address: the rules reference it, and an empty group is a
// deployment away from notifying somebody. Supply alertEmailAddress to be told.
resource actionGroup 'Microsoft.Insights/actionGroups@2023-01-01' = {
  name: actionGroupName
  location: 'global'
  tags: tags
  properties: {
    // Azure caps the short name at 12 characters; it prefixes every mail subject.
    groupShortName: 'RemSolution'
    enabled: true
    emailReceivers: empty(alertEmailAddress) ? [] : [
      {
        name: 'ops'
        emailAddress: alertEmailAddress
        useCommonAlertSchema: true
      }
    ]
  }
}

var alertActions = [
  {
    actionGroupId: actionGroup.id
  }
]

// Severity 0 — the health probe is the one signal that means customers are
// seeing nothing at all. Evaluated every minute, unlike the rest.
resource healthAlert 'Microsoft.Insights/metricAlerts@2018-03-01' = {
  name: '${appServiceName}-health'
  location: 'global'
  tags: tags
  properties: {
    description: 'The /health probe is failing on at least one instance.'
    severity: 0
    enabled: true
    scopes: [appService.id]
    evaluationFrequency: 'PT1M'
    windowSize: 'PT5M'
    autoMitigate: true
    criteria: {
      'odata.type': 'Microsoft.Azure.Monitor.SingleResourceMultipleMetricCriteria'
      allOf: [
        {
          name: 'HealthCheckStatus'
          metricNamespace: 'Microsoft.Web/sites'
          metricName: 'HealthCheckStatus'
          operator: 'LessThan'
          threshold: 100
          timeAggregation: 'Average'
          criterionType: 'StaticThresholdCriterion'
        }
      ]
    }
    actions: alertActions
  }
}

resource http5xxAlert 'Microsoft.Insights/metricAlerts@2018-03-01' = {
  name: '${appServiceName}-http5xx'
  location: 'global'
  tags: tags
  properties: {
    description: 'The web app is returning server errors.'
    severity: 1
    enabled: true
    scopes: [appService.id]
    evaluationFrequency: 'PT5M'
    windowSize: 'PT5M'
    autoMitigate: true
    criteria: {
      'odata.type': 'Microsoft.Azure.Monitor.SingleResourceMultipleMetricCriteria'
      allOf: [
        {
          name: 'Http5xx'
          metricNamespace: 'Microsoft.Web/sites'
          metricName: 'Http5xx'
          operator: 'GreaterThan'
          threshold: http5xxThreshold
          timeAggregation: 'Total'
          criterionType: 'StaticThresholdCriterion'
        }
      ]
    }
    actions: alertActions
  }
}

resource responseTimeAlert 'Microsoft.Insights/metricAlerts@2018-03-01' = {
  name: '${appServiceName}-response-time'
  location: 'global'
  tags: tags
  properties: {
    description: 'The web app is answering slowly.'
    severity: 2
    enabled: true
    scopes: [appService.id]
    evaluationFrequency: 'PT5M'
    windowSize: 'PT15M'
    autoMitigate: true
    criteria: {
      'odata.type': 'Microsoft.Azure.Monitor.SingleResourceMultipleMetricCriteria'
      allOf: [
        {
          name: 'HttpResponseTime'
          metricNamespace: 'Microsoft.Web/sites'
          metricName: 'HttpResponseTime'
          operator: 'GreaterThan'
          threshold: responseTimeThresholdSeconds
          timeAggregation: 'Average'
          criterionType: 'StaticThresholdCriterion'
        }
      ]
    }
    actions: alertActions
  }
}

resource dtuAlert 'Microsoft.Insights/metricAlerts@2018-03-01' = {
  name: '${sqlDatabaseName}-dtu'
  location: 'global'
  tags: tags
  properties: {
    description: 'The database is running out of headroom.'
    severity: 2
    enabled: true
    scopes: [sqlDatabase.id]
    evaluationFrequency: 'PT5M'
    windowSize: 'PT15M'
    autoMitigate: true
    criteria: {
      'odata.type': 'Microsoft.Azure.Monitor.SingleResourceMultipleMetricCriteria'
      allOf: [
        {
          name: 'dtu_consumption_percent'
          metricNamespace: 'Microsoft.Sql/servers/databases'
          metricName: 'dtu_consumption_percent'
          operator: 'GreaterThan'
          threshold: dtuThresholdPercent
          timeAggregation: 'Average'
          criterionType: 'StaticThresholdCriterion'
        }
      ]
    }
    actions: alertActions
  }
}

// Hourly: storage creeps, it does not spike, and a 5-minute window would only
// re-send the same news twelve times.
resource storageAlert 'Microsoft.Insights/metricAlerts@2018-03-01' = {
  name: '${sqlDatabaseName}-storage'
  location: 'global'
  tags: tags
  properties: {
    description: 'The database is filling up. A full database refuses writes.'
    severity: 2
    enabled: true
    scopes: [sqlDatabase.id]
    evaluationFrequency: 'PT15M'
    windowSize: 'PT1H'
    autoMitigate: true
    criteria: {
      'odata.type': 'Microsoft.Azure.Monitor.SingleResourceMultipleMetricCriteria'
      allOf: [
        {
          name: 'storage_percent'
          metricNamespace: 'Microsoft.Sql/servers/databases'
          metricName: 'storage_percent'
          operator: 'GreaterThan'
          threshold: storageThresholdPercent
          timeAggregation: 'Maximum'
          criterionType: 'StaticThresholdCriterion'
        }
      ]
    }
    actions: alertActions
  }
}

// Serilog writes through the App Insights sink, so an ILogger error lands in
// traces and one carrying an exception lands in exceptions — both are needed or
// half the failures are invisible. severityLevel >= 3 is Error and Fatal.
resource appErrorsAlert 'Microsoft.Insights/scheduledQueryRules@2021-08-01' = {
  name: '${applicationInsightsName}-errors'
  location: location
  tags: tags
  kind: 'LogAlert'
  properties: {
    displayName: 'Application errors'
    description: 'The application is logging errors.'
    severity: 2
    enabled: true
    scopes: [applicationInsights.id]
    evaluationFrequency: 'PT15M'
    windowSize: 'PT15M'
    autoMitigate: true
    criteria: {
      allOf: [
        {
          query: '''
union traces, exceptions
| where severityLevel >= 3
'''
          timeAggregation: 'Count'
          operator: 'GreaterThan'
          threshold: 10
          failingPeriods: {
            numberOfEvaluationPeriods: 1
            minFailingPeriodsToAlert: 1
          }
        }
      ]
    }
    actions: {
      actionGroups: [actionGroup.id]
    }
  }
}

// The sweeps are the failure nobody notices: no user is waiting on
// reservation-expiry or personal-data-purge, so a job that throws every night is
// silent until a hold that should have expired blocks a car. Threshold 0 — one
// is already worth reading. SourceContext is where Serilog puts the logger
// category; Hangfire's own logger reports the retries and the final give-up.
resource jobFailuresAlert 'Microsoft.Insights/scheduledQueryRules@2021-08-01' = {
  name: '${applicationInsightsName}-job-failures'
  location: location
  tags: tags
  kind: 'LogAlert'
  properties: {
    displayName: 'Background job failures'
    description: 'A recurring Hangfire job is failing.'
    severity: 2
    enabled: true
    scopes: [applicationInsights.id]
    evaluationFrequency: 'PT1H'
    windowSize: 'PT1H'
    autoMitigate: true
    criteria: {
      allOf: [
        {
          query: '''
union traces, exceptions
| where severityLevel >= 3
| extend source = tostring(customDimensions.SourceContext)
| where source startswith "RemSolution.Infrastructure.Jobs" or source startswith "Hangfire"
'''
          timeAggregation: 'Count'
          operator: 'GreaterThan'
          threshold: 0
          failingPeriods: {
            numberOfEvaluationPeriods: 1
            minFailingPeriodsToAlert: 1
          }
        }
      ]
    }
    actions: {
      actionGroups: [actionGroup.id]
    }
  }
}

output actionGroupId string = actionGroup.id
output actionGroupName string = actionGroup.name
