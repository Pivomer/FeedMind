import * as infraScopes from './../core/infraScopes.bicep'
import { azureRoles } from '../core/azureRoles.bicep'

@description('Deployment environment (e.g., dev, test, prod).')
param env infraScopes.envType

@description('Optional. Cannot be var, should be known during compile time')
param deploymentDate string = sys.utcNow('yyyy-MM-dd')

var location = resourceGroup().location
var containerName = 'feedmind-api'
var jobName = 'feedmind'
var filterRequestsQueue = 'sbq-feedmind-ai-telegram-requests'
var filterResultsQueue = 'sbq-feedmind-ai-telegram-results'

var acrScope = infraScopes.acrScope()
var serviceBusNamespaceScope = infraScopes.serviceBusNamespace()
var commonScope = infraScopes.commonScope(env)

var tags = {
  createdBy: 'DevOps'
  env: env
  deploymentDate: deploymentDate
}

var environmentVariables = [
  {
    name: 'AZURE_CLIENT_ID'
    value: identity.properties.clientId
  }
  {
    name: 'APPLICATION_HOME'
    value: '/mnt/${jobName}'
  }
  {
    name: 'APPLICATION_ENVIRONMENT'
    value: env
  }
]

resource containerRegistry 'Microsoft.ContainerRegistry/registries@2025-11-01' existing = {
  scope: az.resourceGroup(acrScope.resourceGroupName)
  name: acrScope.name
}

resource storageAccount 'Microsoft.Storage/storageAccounts@2024-01-01' existing = {
  name: commonScope.storageScope.name
}

resource logAnalytics 'Microsoft.OperationalInsights/workspaces@2022-10-01' existing = {
  scope: az.resourceGroup(commonScope.logAnalyticsScope.resourceGroupName)
  name: commonScope.logAnalyticsScope.name
}

resource containerAppEnvironment 'Microsoft.App/managedEnvironments@2024-10-02-preview' = {
  name: 'cae-${containerName}-${env}'
  location: location
  identity: {
    type: 'UserAssigned'
    userAssignedIdentities: {
      '${identity.id}': {}
    }
  }
  properties: {
    workloadProfiles: [
      {
        name: 'Consumption'
        workloadProfileType: 'Consumption'
      }
    ]
    zoneRedundant: false
    publicNetworkAccess: 'Disabled'

    appLogsConfiguration: {
      destination: 'log-analytics'
      logAnalyticsConfiguration: {
        customerId: logAnalytics.properties.customerId
        sharedKey: logAnalytics.listKeys().primarySharedKey
      }
    }
    appInsightsConfiguration: {
      connectionString: applicationInsights.outputs.connectionString
    }
    openTelemetryConfiguration: {
      tracesConfiguration: {
        destinations: ['appInsights']
      }
      logsConfiguration: {
        destinations: ['appInsights']
      }
    }
  }
}

resource identity 'Microsoft.ManagedIdentity/userAssignedIdentities@2025-01-31-preview' = {
  name: 'id-${containerName}'
  location: location
}

module applicationInsights 'br/private:insights/application-insight:v1' = {
  name: 'applicationInsights'
  params: {
    env: env
    tags: tags
    name: containerName
  }
}

module acrPullRoleAssignment 'br/private:authorization/role-assignments-container-registry:v1' = {
  name: 'containerRegistryPullRoleAssignment'
  scope: resourceGroup(acrScope.resourceGroupName)
  params: {
    principalId: identity.properties.principalId
    roleId: azureRoles.containers.acrPull
    containerRegistryName: acrScope.name
  }
}

module kvRoleAssignment 'br/private:authorization/role-assignments-keyvault:v1' = {
  name: 'keyVaultRoleAssignment'
  scope: az.resourceGroup(commonScope.kvScope.resourceGroupName)
  params: {
    principalId: identity.properties.principalId
    roleId: azureRoles.kv.keyVaultSecretsUser
    keyVaultName: commonScope.kvScope.name
    principalType: 'ServicePrincipal'
  }
}

module stRoleAssignment 'br/private:authorization/role-assignments-storage-account:v1' = {
  scope: az.resourceGroup(commonScope.storageScope.resourceGroupName)
  name: 'storageAccountRoleAssignment'
  params: {
    principalId: identity.properties.principalId
    roleId: azureRoles.storage.storageTableDataContributor
    storageAccountName: storageAccount.name
    principalType: 'ServicePrincipal'
  }
}

module aiFilterRequestsQueue 'br/private:servicebus/servicebus-namespace-queue:v1' = {
  scope: az.resourceGroup(serviceBusNamespaceScope.resourceGroupName)
  name: 'aiFilterRequestsQueue'
  params: {
    serviceBusNamespaceName: serviceBusNamespaceScope.name
    serviceBusQueueName: '${filterRequestsQueue}-${env}'
  }
}

module aiFilterResultsQueue 'br/private:servicebus/servicebus-namespace-queue:v1' = {
  scope: az.resourceGroup(serviceBusNamespaceScope.resourceGroupName)
  name: 'aiFilterResultsQueue'
  params: {
    serviceBusNamespaceName: serviceBusNamespaceScope.name
    serviceBusQueueName: '${filterResultsQueue}-${env}'
  }
}

module sbRequestSenderRoleAssignment 'br/private:authorization/role-assignments-servicebus-namespace-queue:v1' = {
  name: 'sbRequestSenderRoleAssignment'
  scope: az.resourceGroup(serviceBusNamespaceScope.resourceGroupName)
  params: {
    principalId: identity.properties.principalId
    roleId: azureRoles.integration.azureServiceBusDataSender
    serviceBusNamespaceName: serviceBusNamespaceScope.name
    serviceBusQueueName: '${filterRequestsQueue}-${env}'
  }
}

module sbRequestReceiverRoleAssignment 'br/private:authorization/role-assignments-servicebus-namespace-queue:v1' = {
  name: 'sbRequestReceiverRoleAssignment'
  scope: az.resourceGroup(serviceBusNamespaceScope.resourceGroupName)
  params: {
    principalId: identity.properties.principalId
    roleId: azureRoles.integration.azureServiceBusDataReceiver
    serviceBusNamespaceName: serviceBusNamespaceScope.name
    serviceBusQueueName: '${filterRequestsQueue}-${env}'
  }
}

module sbResultSenderRoleAssignment 'br/private:authorization/role-assignments-servicebus-namespace-queue:v1' = {
  name: 'sbResultSenderRoleAssignment'
  scope: az.resourceGroup(serviceBusNamespaceScope.resourceGroupName)
  params: {
    principalId: identity.properties.principalId
    roleId: azureRoles.integration.azureServiceBusDataSender
    serviceBusNamespaceName: serviceBusNamespaceScope.name
    serviceBusQueueName: '${filterResultsQueue}-${env}'
  }
}

module sbResultReceiverRoleAssignment 'br/private:authorization/role-assignments-servicebus-namespace-queue:v1' = {
  name: 'sbResultReceiverRoleAssignment'
  scope: az.resourceGroup(serviceBusNamespaceScope.resourceGroupName)
  params: {
    principalId: identity.properties.principalId
    roleId: azureRoles.integration.azureServiceBusDataReceiver
    serviceBusNamespaceName: serviceBusNamespaceScope.name
    serviceBusQueueName: '${filterResultsQueue}-${env}'
  }
}

module containerApp './../modules/container-app/main.bicep' = {
  name: 'containerApp-${containerName}-${env}'
  params: {
    name: containerName
    env: env
    tags: tags
    containerAppsEnvironmentName: containerAppEnvironment.name
    containerRegistryServer: containerRegistry.properties.loginServer
    environmentVariables: environmentVariables
    userAssignedIdentityId: identity.id
    ingress: null
  }
}
