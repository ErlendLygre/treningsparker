output "website_url" {
  value = local.web_url
}

output "api_url" {
  value = local.api_url
}

# Used by the Deploy workflow (GitHub variables SCW_API_CONTAINER_ID / SCW_WEB_CONTAINER_ID).
output "api_container_id" {
  value = scaleway_container.api.id
}

output "web_container_id" {
  value = scaleway_container.web.id
}

output "registry_endpoint" {
  value = scaleway_registry_namespace.main.endpoint
}

# Read with: tofu output -raw admin_password (only matters for a fresh database)
output "admin_password" {
  value     = random_password.admin.result
  sensitive = true
}
