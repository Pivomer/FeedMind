import * as infraScopes from './../core/infraScopes.bicep'
import { azureRoles } from '../core/azureRoles.bicep'

@description('Deployment environment (e.g., dev, test, prod).')
param env infraScopes.envType

@description('Optional. Cannot be var, should be known during compile time')
param deploymentDate string = sys.utcNow('yyyy-MM-dd')

var location = resourceGroup().location
var containerName = 'feedmind-api'
var jobName = 'feedmind'

var acrScope = infraScopes.acrScope()
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

resource identity 'Microsoft.ManagedIdentity/userAssignedIdentities@2025-01-31-preview' = {
  name: 'id-${containerName}'
  location: location
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

module containerApp './../modules/container-app/main.bicep' = {
  name: 'containerApp-${containerName}-${env}'
  params: {
    name: containerName
    env: env
    tags: tags
    containerAppsEnvironment: commonScope.caeScope
    containerRegistryServer: containerRegistry.properties.loginServer
    environmentVariables: environmentVariables
    userAssignedIdentityId: identity.id
    ingress: null
  }
}
