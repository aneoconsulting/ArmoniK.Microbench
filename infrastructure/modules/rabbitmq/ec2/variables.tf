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
  description = "Prefix for resource naming"
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
    subnet_id = string
  })
  description = "Network configuration to use for the EFS (VPC:Subnet)"
}

variable "instance_type" {
  description = "EC2 instance type for RabbitMQ"
  type        = string
  default     = "t3.medium"
}

variable "rabbitmq_username" {
  description = "RabbitMQ admin username (optional, will be generated if not provided)"
  type        = string
  default     = null
}

variable "rabbitmq_password" {
  description = "RabbitMQ admin password (optional, will be generated if not provided)"
  type        = string
  default     = null
  sensitive   = true
}

variable "associate_public_ip" {
  description = "Whether to associate a public IP address"
  type        = bool
  default     = true
}

variable "ssh_key_name" {
  description = "Name of existing AWS key pair for SSH access (optional)"
  type        = string
  default     = null
}

variable "ssh_public_key" {
  description = "Public SSH key content (required if ssh_key_name is not provided)"
  type        = string
  default     = ""
}

variable "enable_cloudwatch_logs" {
  description = "Enable CloudWatch logs for RabbitMQ"
  type        = bool
  default     = false
}

variable "enable_detailed_monitoring" {
  description = "Enable detailed CloudWatch monitoring"
  type        = bool
  default     = false
}

variable "backup_retention_days" {
  description = "Number of days to retain automated backups"
  type        = number
  default     = 7
}

# Output
variable "config_file_path" {
  description = "Path to the local directory to use as the configuration path"
  type        = string
  default     = null
}
