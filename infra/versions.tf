terraform {
  required_version = ">= 1.8"

  required_providers {
    scaleway = {
      source  = "scaleway/scaleway"
      version = "~> 2.89"
    }
    random = {
      source  = "hashicorp/random"
      version = "~> 3.6"
    }
  }

  # State lives in a private Scaleway Object Storage bucket. The bucket name is passed at init time:
  #   tofu init -backend-config="bucket=<name>"
  # Credentials come from AWS_ACCESS_KEY_ID / AWS_SECRET_ACCESS_KEY (set to the Scaleway API key).
  backend "s3" {
    key                         = "treningsparker/terraform.tfstate"
    region                      = "nl-ams"
    endpoints                   = { s3 = "https://s3.nl-ams.scw.cloud" }
    skip_credentials_validation = true
    skip_region_validation      = true
    skip_requesting_account_id  = true
    skip_metadata_api_check     = true
    skip_s3_checksum            = true
  }
}

# Credentials: SCW_ACCESS_KEY / SCW_SECRET_KEY environment variables.
provider "scaleway" {
  project_id = var.project_id
  region     = var.region
  zone       = var.zone
}
