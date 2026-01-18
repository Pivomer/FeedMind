import * as sharedServices from './../core/shared-services.bicep'
import { azureRoles } from '../core/azureRoles.bicep'

@description('Deployment environment (e.g., dev, test, prod).')
param env string

var location = resourceGroup().location
var containerName = 'feedmind-api'

var acrScope = sharedServices.acrScope()

resource containerRegistry 'Microsoft.ContainerRegistry/registries@2025-11-01' existing = {
  scope: az.resourceGroup(acrScope.subscriptionId, acrScope.resourceGroupName)
  name: 'crSharedBuilder'
}

resource containerAppsEnvironment 'Microsoft.App/managedEnvironments@2025-10-02-preview' existing = {
  scope: az.resourceGroup(acrScope.subscriptionId, acrScope.resourceGroupName)
  name: 'cae-data-${env}'
}

resource identity 'Microsoft.ManagedIdentity/userAssignedIdentities@2025-01-31-preview' = {
  name: 'id-${containerName}'
  location: location
}

module acrRoleAssignment './../core/acr-role-assignment.bicep' = {
  name: 'acrRoleAssignment'
  scope: az.resourceGroup(acrScope.subscriptionId, acrScope.resourceGroupName)
  params: {
    containerRegistryName: containerRegistry.name
    roleId: azureRoles.containers.acrPull
    principalId: identity.properties.principalId
  }
}

module containerApp './../modules/container-app/main.bicep' = {
  params: {
    name: containerName
    env: env
    containerAppsEnvironmentId: containerAppsEnvironment.id
    containerRegistryServer: containerRegistry.properties.loginServer
    userAssignedIdentityId: identity.id
    ingress: null
  }
  dependsOn: [
    acrRoleAssignment
  ]
}
