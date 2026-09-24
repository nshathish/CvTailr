resource "random_uuid" "access_as_user_scope_id" {
}

resource "azuread_application" "api" {
  display_name     = var.api_name
  sign_in_audience = "AzureADMyOrg"

  identifier_uris = [
    "api://${azuread_application.api.client_id}"
  ]

  api {
    requested_access_token_version = 2
  }
}

resource "azuread_application_permission_scope" "access_as_user" {
  application_id = azuread_application.api.id
  scope_id       = random_uuid.access_as_user_scope_id.result

  value = "access_as_user"
  type  = "Admin"

  admin_consent_display_name = "Access CvTailr API"
  admin_consent_description  = "Allows CvTailr.Web to access CvTailr.Api on behalf of the signed-in user."

  user_consent_display_name = "Access CvTailr API"
  user_consent_description  = "Allows access to CvTailr.Api on your behalf."
}

resource "azuread_application" "web" {
  display_name     = var.web_name
  sign_in_audience = "AzureADMyOrg"

  web {
    redirect_uris = var.web_redirect_uris
  }

  required_resource_access {
    resource_app_id = azuread_application.api.client_id

    resource_access {
      id   = azuread_application_permission_scope.access_as_user.scope_id
      type = "Scope"
    }
  }
}

resource "azuread_service_principal" "api" {
  client_id = azuread_application.api.client_id
}

resource "azuread_service_principal" "web" {
  client_id = azuread_application.web.client_id
}

resource "azuread_service_principal_delegated_permission_grant" "web_to_api" {
  service_principal_object_id          = azuread_service_principal.web.object_id
  resource_service_principal_object_id = azuread_service_principal.api.object_id

  claim_values = [
    azuread_application_permission_scope.access_as_user.value
  ]
}
