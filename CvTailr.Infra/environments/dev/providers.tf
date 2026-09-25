provider "azurerm" {
  features {}
  use_oidc        = true
  subscription_id = var.azure_subscription_id
  tenant_id       = var.azure_tenant_id
}

data "azurerm_resource_group" "main" {
  name = "cvtailr-rg"
}
