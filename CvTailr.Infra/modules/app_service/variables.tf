variable "api_app_name" {
  description = "Name of the Api App Service"
  type        = string
}

variable "web_app_name" {
  description = "Name of the Web App Service"
  type        = string
}

variable "resource_group_name" {
  description = "Resource group to deploy the App Service plan and apps into"
  type        = string
}

variable "location" {
  description = "Azure region for the App Service plan and apps"
  type        = string
}

variable "api_app_settings" {
  description = "Application settings for the Api App Service"
  type        = map(string)
  sensitive   = true
}

variable "web_app_settings" {
  description = "Application settings for the Web App Service"
  type        = map(string)
  sensitive   = true
}
