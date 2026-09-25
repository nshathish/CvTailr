output "api_client_id" {
  value = azuread_application.api.client_id
}

output "api_scope" {
  value = "api://${azuread_application.api.client_id}/${azuread_application_permission_scope.access_as_user.value}"
}

output "web_client_id" {
  value = azuread_application.web.client_id
}

output "web_client_secret" {
  value     = azuread_application_password.web.value
  sensitive = true
}
