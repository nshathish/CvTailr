output "api_client_id" {
  value = var.entra_api_client_id
}

output "api_scope" {
  value = "api://${var.entra_api_client_id}/access_as_user"
}

output "web_client_id" {
  value = var.entra_web_client_id
}
