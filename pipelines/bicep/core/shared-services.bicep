var subscription = {
  development: {
    id: '1b203896-402d-47f0-9ade-798581af864b'
  }
}

type resourceScope = {
  name: string
  resourceGroupName: string
  subscriptionId: string
}

@export()
func acrScope() resourceScope => containerRegistryScope

var containerRegistryScope resourceScope = {
  name: 'crSharedBuilder'
  resourceGroupName: 'rg-shared-infrastructure'
  subscriptionId: subscription.development.id
}
