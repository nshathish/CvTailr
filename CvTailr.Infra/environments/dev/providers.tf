provider "azuread" {
  tenant_id = var.entra_tenant_id
  client_id = var.entra_client_id
  use_oidc  = true
}

provider "azurerm" {
  features {}
  use_oidc        = true
  subscription_id = var.azure_subscription_id
  tenant_id       = var.azure_tenant_id
}

data "azurerm_resource_group" "main" {
  name = "cvtailr-rg"
}
