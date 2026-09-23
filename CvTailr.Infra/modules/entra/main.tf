resource "azuread_application" "api" {
  display_name     = var.api_name
  sign_in_audience = "AzureADMyOrg"

  api {
    requested_access_token_version = 2
  }
}
