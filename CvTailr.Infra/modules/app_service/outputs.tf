output "api_default_hostname" {
  value = azurerm_linux_web_app.api.default_hostname
}

output "web_default_hostname" {
  value = azurerm_linux_web_app.web.default_hostname
}
