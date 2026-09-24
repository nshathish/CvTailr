module "entra" {
  source = "../../modules/entra"

  api_name = "CvTailr.Api"
  web_name = "CvTailr.Web"

  web_redirect_uris = [
    "https://localhost:7077/signin-oidc"
  ]
}
