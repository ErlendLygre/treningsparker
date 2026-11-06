#!/usr/bin/env bash
# Brings the resources that were created by hand in the Scaleway console under OpenTofu management.
# Safe to re-run: resources already in the state are skipped. Run from infra/ after `tofu init`.
# IDs come from environment variables (GitHub variables in the Infra workflow).
set -euo pipefail
REGION="${TF_VAR_region:-nl-ams}"

import() {
  local address=$1 id=$2
  if tofu state list 2>/dev/null | grep -qx "$address"; then
    echo "skip   $address (already managed)"
  elif [ -z "${id##*/}" ]; then
    echo "::error::Missing ID for $address" && exit 1
  else
    echo "import $address <- $id"
    tofu import -input=false "$address" "$id"
  fi
}

import scaleway_vpc_private_network.main   "$REGION/${SCW_PRIVATE_NETWORK_ID:-}"
import scaleway_rdb_instance.main          "$REGION/${SCW_DB_INSTANCE_ID:-}"
import scaleway_rdb_database.app           "$REGION/${SCW_DB_INSTANCE_ID:-}/treningsparker"
import scaleway_registry_namespace.main    "$REGION/${SCW_REGISTRY_NAMESPACE_ID:-}"
import scaleway_container_namespace.main   "$REGION/${SCW_CONTAINER_NAMESPACE_ID:-}"
import scaleway_container.api              "$REGION/${SCW_API_CONTAINER_ID:-}"
import scaleway_container.web              "$REGION/${SCW_WEB_CONTAINER_ID:-}"
# Only if the domain was added in the console first (set SCW_WEB_DOMAIN_ID to its ID).
if [ -n "${SCW_WEB_DOMAIN_ID:-}" ]; then
  import scaleway_container_domain.web     "$REGION/$SCW_WEB_DOMAIN_ID"
fi
