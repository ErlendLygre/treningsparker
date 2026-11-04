variable "project_id" {
  description = "Scaleway project ID (GitHub variable SCW_PROJECT_ID)."
  type        = string
}

variable "region" {
  type    = string
  default = "nl-ams"
}

variable "zone" {
  type    = string
  default = "nl-ams-1"
}

variable "admin_email" {
  description = "Email of the app's admin user (GitHub variable ADMIN_EMAIL). Kept out of the public repo."
  type        = string
}

# Names of the existing resources. They must match what's in Scaleway, or they'll be renamed on apply.
variable "private_network_name" {
  type = string
}

variable "registry_namespace_name" {
  type = string
}

variable "container_namespace_name" {
  type = string
}

variable "db_instance_name" {
  type = string
}

# Changing these two replaces the database instance (data loss), which prevent_destroy blocks.
variable "db_engine" {
  type    = string
  default = "PostgreSQL-17"
}

variable "db_user_name" {
  description = "Admin user of the database instance, used by the API."
  type        = string
}

variable "db_node_type" {
  type    = string
  default = "DB-DEV-S"
}

variable "api_memory_mb" {
  description = "API container memory; Scaleway scales CPU with it. 1024 MB gives ~0.56 vCPU."
  type        = number
  default     = 1024
}

variable "api_min_scale" {
  description = "1 keeps one instance warm (no cold starts, costs more); 0 scales to zero when idle."
  type        = number
  default     = 0
}

variable "api_max_scale" {
  type    = number
  default = 2
}

variable "web_memory_mb" {
  type    = number
  default = 128
}

variable "web_max_scale" {
  type    = number
  default = 2
}
