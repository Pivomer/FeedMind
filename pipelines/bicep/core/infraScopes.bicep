@export()
type envType = 'dev'

type resourceScope = {
  name: string
  resourceGroupName: string
}

type commonScopeType = {
  kvScope: resourceScope
  storageScope: resourceScope
  logAnalyticsScope: resourceScope
}

var containerRegistryScope resourceScope = {
  name: 'pvmacr'
  resourceGroupName: 'rg-pvm-shared'
}

var serviceBusNamespaceScope resourceScope = {
  name: 'sbns-pvm-shared'
  resourceGroupName: 'rg-pvm-shared'
}

var openAiScope resourceScope = {
  name: 'oai-pvm-shared'
  resourceGroupName: 'rg-pvm-shared'
}

@export()
func commonScope(env envType) commonScopeType => createCommonScope(env)

@export()
func acrScope() resourceScope => containerRegistryScope

@export()
func serviceBusNamespace() resourceScope => serviceBusNamespaceScope

@export()
func openAi() resourceScope => openAiScope

func commonResourceGroupName(env envType) string => 'rg-pvm-common-${env}'

func createCommonScope(env envType) commonScopeType => {
  kvScope: keyVaultScope(env)
  storageScope: storageScope(env)
  logAnalyticsScope: logAnalyticsScope(env)
}

func logAnalyticsScope(env envType) resourceScope => {
  name: 'la-pvm-${env}'
  resourceGroupName: commonResourceGroupName(env)
}

func keyVaultScope(env envType) resourceScope => {
  name: 'kv-pvm-${env}'
  resourceGroupName: commonResourceGroupName(env)
}

func storageScope(env envType) resourceScope => {
  name: 'stpvm${env}'
  resourceGroupName: commonResourceGroupName(env)
}
