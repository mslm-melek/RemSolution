param name string
param location string = resourceGroup().location
param tags object = {}

param serviceName string = 'web'
param applicationInsightsName string = ''
param keyVaultName string = ''

module appServicePlan '../core/host/appserviceplan.bicep' = {
  name: 'appServicePlan'
  params: {
    name: name
    location: location
    tags: tags
    sku: {
      name: 'B1'
    }
  }
}

module appService '../core/host/appservice.bicep' = {
  name: 'appService'
  params: {
    name: name
    location: location
    tags: union(tags, { 'azd-service-name': serviceName })
    appServicePlanId: appServicePlan.outputs.id
    applicationInsightsName: applicationInsightsName
    keyVaultName: keyVaultName
    runtimeName: 'dotnetcore'
    // Must match the SDK in global.json / the projects' TargetFramework.
    runtimeVersion: '10.0'
    healthCheckPath: '/health'
    appSettings: {
      // Production: Development would load appsettings.Development.json (a
      // published signing key, the demo seeder, self-migration). Secrets come
      // from Key Vault, and the host refuses to start without the signing key.
      ASPNETCORE_ENVIRONMENT: 'Production'
      // Uploads live on the persistent /home share, not under wwwroot, which a
      // zip deploy replaces wholesale.
      FileStorage__RootPath: '/home/data/uploads'
    }
  }
}

output name string = appService.outputs.name
output uri string = appService.outputs.uri
output identityPrincipalId string = appService.outputs.identityPrincipalId
