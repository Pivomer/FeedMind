@export()
type envType = 'dev'

type resourceScope = {
  name: string
  resourceGroupName: string
}

type commonScopeType = {
  kvScope: resourceScope
  storageScope: resourceScope
}

var containerRegistryScope resourceScope = {
  name: 'pvmacr'
  resourceGroupName: 'rg-pvm-shared'
}

@export()
func commonScope(env envType) commonScopeType => createCommonScope(env)

@export()
func acrScope() resourceScope => containerRegistryScope

func commonResourceGroupName(env envType) string => 'rg-pvm-common-${env}'

func createCommonScope(env envType) commonScopeType => {
  kvScope: keyVaultScope(env)
  storageScope: storageScope(env)
}

func keyVaultScope(env envType) resourceScope => {
  name: 'kv-pvm-${env}'
  resourceGroupName: commonResourceGroupName(env)
}

func storageScope(env envType) resourceScope => {
  name: 'stpvm${env}'
  resourceGroupName: commonResourceGroupName(env)
}
