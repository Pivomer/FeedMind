@description('Location for all resources')
param location string = 'westeurope'

@description('Name of the Storage Account')
@minLength(3)
@maxLength(24)
param storageAccountName string

@description('SKU of the Storage Account')
param storageSku string = 'Standard_LRS'

@description('Kind of the Storage Account')
param storageKind string = 'StorageV2'

resource storageAccount 'Microsoft.Storage/storageAccounts@2023-01-01' = {
  name: storageAccountName
  location: location
  kind: storageKind
  sku: {
    name: storageSku
  }
  properties: {
    allowBlobPublicAccess: false
    minimumTlsVersion: 'TLS1_2'
    supportsHttpsTrafficOnly: true
  }
}

output storageAccountId string = storageAccount.id
