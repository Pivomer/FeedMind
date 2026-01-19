@description('Deployment environment (e.g., dev, test, prod).')
param env string

@description('Resource name prefix for the Container App.')
param name string

param ingress {
  @description('Bool indicating if app exposes an external endpoint.')
  external: bool
  targetPort: int
  transport: 'auto' | 'http' | 'http2' | 'tcp'
}?

param containerAppsEnvironmentId string

@description('Container registry server URL.')
param containerRegistryServer string

@description('User assigned identity ID for accessing the container registry.')
param userAssignedIdentityId string

@description('Docker image path for the Container App.')
param imageName string = 'mcr.microsoft.com/k8se/quickstart:latest'

var location = resourceGroup().location

resource containerApp 'Microsoft.App/containerApps@2025-10-02-preview' = {
  name: 'ca-${name}-${env}'
  location: location
  identity: {
    type: 'UserAssigned'
    userAssignedIdentities: {
      '${userAssignedIdentityId}': {}
    }
  }
  properties: {
    environmentId: containerAppsEnvironmentId
    configuration: {
      activeRevisionsMode: 'Single'
      ingress: ingress
      registries: [
        {
          server: containerRegistryServer
          identity: userAssignedIdentityId
        }
      ]
    }
    template: {
      containers: [
        {
          name: name
          image: imageName
          probes: [
            {
              type: 'Liveness'
              httpGet: {
                path: '/health/live'
                port: 8080
              }
              initialDelaySeconds: 5
              periodSeconds: 10
            }
            {
              type: 'Readiness'
              httpGet: {
                path: '/health/ready'
                port: 8080
              }
              initialDelaySeconds: 5
              periodSeconds: 10
            }
          ]
        }
      ]
    }
  }
}
