# Non-secret settings, committed. Names must match the existing Scaleway resources exactly.
private_network_name     = "treningsparker-vpc"
registry_namespace_name  = "treningsparker"
container_namespace_name = "erlendcorp-treningsparker"
db_instance_name         = "treningsparker"
db_user_name             = "adminparktrening"

custom_domain = "treningsparker.lygre.it"

db_engine     = "PostgreSQL-17"
db_node_type  = "DB-DEV-S"
api_memory_mb = 1024
api_min_scale = 0
api_max_scale = 5
web_max_scale = 5
