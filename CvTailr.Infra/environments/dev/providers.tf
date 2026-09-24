provider "azuread" {
  tenant_id = var.entra_tenant_id
  client_id = var.entra_client_id
  use_oidc  = true
}
