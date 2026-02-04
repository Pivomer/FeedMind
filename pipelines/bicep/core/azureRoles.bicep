@description('Built-in roles https://learn.microsoft.com/en-us/azure/role-based-access-control/built-in-roles')
@export()
var azureRoles = {
  containers: {
    acrPull: '7f951dda-4ed3-4680-a7ca-43fe172d538d'
  }
  kv: {
    keyVaultSecretsUser: '4633458b-17de-408a-b874-0445c86b69e6'
  }
  storage: {
    storageTableDataContributor: '0a9a7e1f-b9d0-4cc4-a60d-0319b160aaa3'
  }
}
