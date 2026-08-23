@description('Deployment environment (e.g., dev, test, prod).')
param env string

@description('Deployment tags.')
param tags object

@description('Resource name. Azure prefix and env will be added automatically.')
param name string

@description('Container environment variables.')
param environmentVariables {
  name: string
  value: string
}[]

@description('User assigned identity ID for accessing the container registry.')
param userAssignedIdentityId string

@description('Container registry server URL.')
param containerRegistryServer string

@description('Container App environment name.')
param containerAppsEnvironmentName string

@description('Docker image path for the Container App job.')
param imageName string = 'mcr.microsoft.com/k8se/quickstart:latest'

@description('Maximum number of seconds a replica is allowed to run.')
param replicaTimeout int = 1800

@description('Maximum number of retries before failing the job.')
param replicaRetryLimit int = 1

@description('Cron formatted repeating schedule ("* * * * *") of a Cron Job.')
param cronExpression string

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

@description('Amount of memory (in gibibytes, GiB) allocated to the container. Ratio with CPU cores must be equal to 2.')
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

resource containerJob 'Microsoft.App/jobs@2025-01-01' = {
  name: 'caj-${name}-${env}'
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
      triggerType: 'Schedule'
      replicaTimeout: replicaTimeout
      replicaRetryLimit: replicaRetryLimit
      scheduleTriggerConfig: {
        replicaCompletionCount: 1
        cronExpression: cronExpression
        parallelism: 1
      }
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
