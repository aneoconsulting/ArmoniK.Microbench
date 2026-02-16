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
  description = "Prefix for EFS related resources"
  type        = string
}

variable "additional_tags" {
  description = "Additional tags specific to this module"
  type        = map(string)
  default     = {}
}

variable "network_config" {
  type = object({
    vpc_id    = string
    subnet_ids = list(string)
  })
  description = "Network configuration to use for the EFS (VPC:Subnet)"
}

variable "instance_security_group_id" {
  description = "Security group ID of the EC2 instance(s) that will access the EFS"
  type        = string
}

variable "config_file_path" {
  description = "Path to the local directory to use as the configuration path"
  type        = string
  default     = null
}
