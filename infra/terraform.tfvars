# Non-secret settings, committed. Names must match the existing Scaleway resources exactly.
# TODO: replace the <...> values with the names shown in the Scaleway console before the first import/plan.
private_network_name     = "<private network name>"
registry_namespace_name  = "<registry namespace name>"
container_namespace_name = "<container namespace name>"
db_instance_name         = "<database instance name>"
db_user_name             = "<database admin user name>"

db_engine     = "PostgreSQL-17"
db_node_type  = "DB-DEV-S"
api_memory_mb = 1024
api_min_scale = 0
