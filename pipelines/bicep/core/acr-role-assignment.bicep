param principalId string
param roleId string
param containerRegistryName string

resource containerRegistry 'Microsoft.ContainerRegistry/registries@2025-11-01' existing = {
  name: containerRegistryName
}

resource roleAssignment 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  scope: containerRegistry
  name: guid(containerRegistry.id, principalId, roleId)
  properties: {
    roleDefinitionId: resourceId('Microsoft.Authorization/roleDefinitions', roleId)
    principalId: principalId
    principalType: 'ServicePrincipal'
  }
}

output roleAssignmentName string = guid(containerRegistry.id, principalId, roleId)
