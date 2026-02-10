type resourceScope = {
  name: string
  resourceGroupName: string
}

param tags object

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

@description('Container environment variables')
param environmentVariables {
  name: string
  value: string
}[]

@description('Container App environment name.')
param containerAppsEnvironmentName string

@description('Container registry server URL.')
param containerRegistryServer string

@description('User assigned identity ID for accessing the container registry.')
param userAssignedIdentityId string

@description('Docker image path for the Container App.')
param imageName string = 'mcr.microsoft.com/k8se/quickstart:latest'

@description('Number of CPU cores the container can use. Can be with a maximum of two decimals.')
@allowed([
  '0.25'
  '0.5'
  '0.75'
  '1'
  '1.25'
  '1.5'
  '1.75'
  '2'
])
param cpuCore string = '0.25'

@description('Amount of memory (in gibibytes, GiB) allocated to the container up to 4GiB. Can be with a maximum of two decimals. Ratio with CPU cores must be equal to 2.')
@allowed([
  '0.5'
  '1'
  '1.5'
  '2'
  '3'
  '3.5'
  '4'
])
param memorySize string = '0.5'

var location = resourceGroup().location
var volumeName = 'feedmind-volume'
var storageName = 'storage-share'

resource containerAppsEnvironment 'Microsoft.App/managedEnvironments@2025-01-01' existing = {
  name: containerAppsEnvironmentName
}

resource appStorage 'Microsoft.App/managedEnvironments/storages@2025-01-01' existing = {
  parent: containerAppsEnvironment
  name: storageName
}

resource containerApp 'Microsoft.App/containerApps@2025-10-02-preview' = {
  name: 'ca-${name}-${env}'
  location: location
  identity: {
    type: 'UserAssigned'
    userAssignedIdentities: {
      '${userAssignedIdentityId}': {}
    }
  }
  tags: tags
  properties: {
    environmentId: containerAppsEnvironment.id
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
          env: environmentVariables
          resources: {
            cpu: json(cpuCore)
            memory: '${memorySize}Gi'
          }
          volumeMounts: [
            {
              volumeName: volumeName
              mountPath: '/mnt'
            }
          ]
          probes: [
            {
              type: 'Liveness'
              httpGet: {
                path: '/health/live'
                port: 8080
              }
              initialDelaySeconds: 15
              periodSeconds: 60
            }
            {
              type: 'Readiness'
              httpGet: {
                path: '/health/ready'
                port: 8080
              }
              initialDelaySeconds: 15
              periodSeconds: 60
            }
          ]
        }
      ]
      volumes: [
        {
          name: volumeName
          storageName: appStorage.name
          storageType: 'AzureFile'
        }
      ]
    }
  }
}
