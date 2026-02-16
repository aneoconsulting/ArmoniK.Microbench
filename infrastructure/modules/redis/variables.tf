variable "region" {
  description = "AWS region where buckets will be created"
  type        = string
  default     = "us-east-1"
}

variable "profile" {
  description = "AWS profile to use for authentication"
  type        = string
  default     = "default"
}

variable "prefix" {
  description = "Prefix to use for Redis related resources"
  type        = string
}

variable "additional_tags" {
  description = "Additional tags specific to this module"
  type        = map(string)
  default     = {}
}

# Redis variables
variable "node_type" {
  description = "Node type to use for the Redis cluster"
  type        = string
  default     = "cache.t3.micro"
}

variable "engine_version" {
  description = "Redis version to use for the cluster"
  type        = string
  default     = "7.0"
}

variable "param_group_name" {
  description = "Parameter Group name to use for the Redis cluster"
  type        = string
  default     = "default.redis7"
}

variable "network_config" {
  type = object({
    vpc_id    = string
    subnet_id = string
    subnet_ids = list(string)
  })
  description = "Network configuration to use for the EFS (VPC:Subnet)"
}

# Output
variable "config_file_path" {
  description = "Path to the local directory to use as the configuration path"
  type        = string
  default     = null
}
