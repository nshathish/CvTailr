# F1 (Free) Linux App Service plans support multiple apps per plan (the free-tier limit is on
# compute minutes/storage quota and on plan count per region, not on app count per plan), so one
# shared plan hosts both apps here rather than one plan each.
resource "azurerm_service_plan" "main" {
  name                = "cvtailr-plan-dev"
  resource_group_name = var.resource_group_name
  location            = var.location
  os_type             = "Linux"
  sku_name            = "F1"
}

resource "azurerm_linux_web_app" "api" {
  name                = var.api_app_name
  resource_group_name = var.resource_group_name
  location            = var.location
  service_plan_id     = azurerm_service_plan.main.id
  https_only          = true

  site_config {
    always_on = false
    application_stack {
      dotnet_version = "10.0"
    }
  }

  app_settings = var.api_app_settings
}

resource "azurerm_linux_web_app" "web" {
  name                = var.web_app_name
  resource_group_name = var.resource_group_name
  location            = var.location
  service_plan_id     = azurerm_service_plan.main.id
  https_only          = true

  client_affinity_enabled = true # ARR sticky sessions, needed for Blazor circuits once you scale out

  site_config {
    always_on          = false
    websockets_enabled = true
    application_stack {
      dotnet_version = "10.0"
    }
  }

  app_settings = var.web_app_settings
}
