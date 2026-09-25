variable "entra_tenant_id" {
  type = string
}

variable "entra_api_client_id" {
  type = string
}

variable "entra_web_client_id" {
  type = string
}

variable "entra_web_client_secret" {
  type      = string
  sensitive = true
}

variable "azure_subscription_id" {
  type = string
}

variable "azuread_instance" {
  type = string
}

variable "foundry_endpoint" {
  type = string
}

variable "foundry_api_key" {
  type      = string
  sensitive = true
}

variable "foundry_cv_parsing_deployment_name" {
  type = string
}

variable "foundry_jd_parsing_deployment_name" {
  type = string
}

variable "foundry_scoring_deployment_name" {
  type = string
}

variable "firecrawl_api_key" {
  type      = string
  sensitive = true
}

variable "cosmos_endpoint" {
  type = string
}

variable "cosmos_account_key" {
  type      = string
  sensitive = true
}

variable "cosmos_database_name" {
  type = string
}

variable "cosmos_container_name" {
  type = string
}

variable "cosmos_cv_container_name" {
  type = string
}

variable "cosmos_job_container_name" {
  type = string
}

variable "cosmos_tailored_cv_container_name" {
  type = string
}

variable "azure_tenant_id" {
  type = string
}
