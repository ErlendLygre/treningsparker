# Everything the app runs on in Scaleway. App images are deployed by .github/workflows/deploy.yml;
# this config owns the infrastructure around them (network, database, registry, containers, secrets).

resource "scaleway_vpc_private_network" "main" {
  name = var.private_network_name
}

# --- Database ------------------------------------------------------------------------------------

# Characters that are safe inside an Npgsql connection string.
resource "random_password" "db" {
  length           = 32
  min_upper        = 2
  min_lower        = 2
  min_numeric      = 2
  min_special      = 2
  override_special = "!#%*-_+"
}

resource "scaleway_rdb_instance" "main" {
  name          = var.db_instance_name
  node_type     = var.db_node_type
  engine        = var.db_engine
  is_ha_cluster = false
  user_name     = var.db_user_name
  password      = random_password.db.result

  # Only reachable from the Private Network: no public endpoint (no load_balancer block).
  private_network {
    pn_id       = scaleway_vpc_private_network.main.id
    enable_ipam = true
  }

  lifecycle {
    prevent_destroy = true
  }
}

resource "scaleway_rdb_database" "app" {
  instance_id = scaleway_rdb_instance.main.id
  name        = "treningsparker"

  lifecycle {
    prevent_destroy = true
  }
}

# --- Registry and containers ---------------------------------------------------------------------

resource "scaleway_registry_namespace" "main" {
  name      = var.registry_namespace_name
  is_public = false
}

resource "scaleway_container_namespace" "main" {
  name = var.container_namespace_name
}

resource "random_password" "jwt" {
  length  = 64
  special = false
}

# Only used when the admin user doesn't exist yet (fresh database). An existing admin keeps their password.
resource "random_password" "admin" {
  length  = 24
  special = false
}

locals {
  # public_endpoint may or may not include the scheme; normalise to a full https URL.
  api_url = startswith(scaleway_container.api.public_endpoint, "https://") ? scaleway_container.api.public_endpoint : "https://${scaleway_container.api.public_endpoint}"
  web_url = startswith(scaleway_container.web.public_endpoint, "https://") ? scaleway_container.web.public_endpoint : "https://${scaleway_container.web.public_endpoint}"

  db_endpoint = scaleway_rdb_instance.main.private_network[0]
  connection_string = join(";", [
    "Host=${local.db_endpoint.ip}",
    "Port=${local.db_endpoint.port}",
    "Database=${scaleway_rdb_database.app.name}",
    "Username=${var.db_user_name}",
    "Password=${random_password.db.result}",
    "SSL Mode=Require",
  ])
}

resource "scaleway_container" "api" {
  name                   = "treningsparker-api"
  namespace_id           = scaleway_container_namespace.main.id
  image                  = "${scaleway_registry_namespace.main.endpoint}/treningsparker-api:latest"
  port                   = 8080
  memory_limit_bytes     = var.api_memory_mb * 1024 * 1024
  min_scale              = var.api_min_scale
  max_scale              = var.api_max_scale
  privacy                = "public"
  https_connections_only = true
  private_network_id     = scaleway_vpc_private_network.main.id

  environment_variables = {
    DatabaseProvider = "Postgres"
    Admin__Email     = var.admin_email
  }

  secret_environment_variables = {
    ConnectionStrings__Default = local.connection_string
    Jwt__Key                   = random_password.jwt.result
    Admin__Password            = random_password.admin.result
  }

  lifecycle {
    # The Deploy workflow points the container at each new image tag.
    ignore_changes = [image, registry_image]

    # Without permission to read the private IP (IPAM), the connection string would get an empty host.
    precondition {
      condition     = try(local.db_endpoint.ip, "") != "" && try(local.db_endpoint.port, 0) > 0
      error_message = "Database private IP/port is unknown. Give the OpenTofu key read access to IPAM (IPAMReadOnly)."
    }
  }
}

resource "scaleway_container" "web" {
  name                   = "treningsparker-web"
  namespace_id           = scaleway_container_namespace.main.id
  image                  = "${scaleway_registry_namespace.main.endpoint}/treningsparker-web:latest"
  port                   = 80
  memory_limit_bytes     = var.web_memory_mb * 1024 * 1024
  min_scale              = 0
  max_scale              = var.web_max_scale
  privacy                = "public"
  https_connections_only = true

  environment_variables = {
    API_UPSTREAM = local.api_url
  }

  lifecycle {
    ignore_changes = [image, registry_image]
  }
}

# Custom domain for the website. Needs a CNAME at the DNS provider (Domeneshop) pointing to the web
# container's endpoint; Scaleway then issues a Let's Encrypt certificate for it.
resource "scaleway_container_domain" "web" {
  container_id = scaleway_container.web.id
  hostname     = var.custom_domain
}
