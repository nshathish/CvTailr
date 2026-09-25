locals {
  api_app_name = "cvtailr-api-dev"
  web_app_name = "cvtailr-web-dev"
}

module "app_service" {
  source = "../../modules/app_service"

  api_app_name        = local.api_app_name
  web_app_name        = local.web_app_name
  resource_group_name = data.azurerm_resource_group.main.name
  location            = data.azurerm_resource_group.main.location

  api_app_settings = {
    "Foundry__Endpoint"                = var.foundry_endpoint
    "Foundry__ApiKey"                  = var.foundry_api_key
    "Foundry__CvParsingDeploymentName" = var.foundry_cv_parsing_deployment_name
    "Foundry__JdParsingDeploymentName" = var.foundry_jd_parsing_deployment_name
    "Foundry__ScoringDeploymentName"   = var.foundry_scoring_deployment_name
    "AzureAd__Instance"                = var.azuread_instance
    "AzureAd__TenantId"                = var.entra_tenant_id
    "AzureAd__ClientId"                = var.entra_api_client_id
    "AzureAd__Audience"                = "api://${var.entra_api_client_id}"
    "Firecrawl__BaseUrl"               = "https://api.firecrawl.dev"
    "Firecrawl__ApiKey"                = var.firecrawl_api_key
    "Cosmos__Endpoint"                 = var.cosmos_endpoint
    "Cosmos__AccountKey"               = var.cosmos_account_key
    "Cosmos__DatabaseName"             = var.cosmos_database_name
    "Cosmos__ContainerName"            = var.cosmos_container_name
    "Cosmos__CvContainerName"          = var.cosmos_cv_container_name
    "Cosmos__JobContainerName"         = var.cosmos_job_container_name
    "Cosmos__TailoredCvContainerName"  = var.cosmos_tailored_cv_container_name
  }

  web_app_settings = {
    "AzureAd__Instance"        = var.azuread_instance
    "AzureAd__TenantId"        = var.entra_tenant_id
    "AzureAd__ClientId"        = var.entra_web_client_id
    "AzureAd__ClientSecret"    = var.entra_web_client_secret
    "AzureAd__CallbackPath"    = "/signin-oidc"
    "DownstreamApi__BaseUrl"   = "https://${module.app_service.api_default_hostname}"
    "DownstreamApi__Scopes__0" = "api://${var.entra_api_client_id}/access_as_user"
  }
}
