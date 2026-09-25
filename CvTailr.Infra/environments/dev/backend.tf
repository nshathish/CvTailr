terraform {
  backend "azurerm" {
    use_oidc         = true
    use_azuread_auth = true

    storage_account_name = "stcvtailrtfstate01"
    container_name       = "tfstate"
    key                  = "cvtailr-dev.tfstate"
  }
}
