variable "region" {
  description = "The AWS region to deploy the resources in"
  type        = string
  default     = "us-east-1"
}

variable "profile" {
  description = "AWS profile to use for authentication"
  type        = string
  default     = "default"
}

variable "prefix" {
  description = "Prefix for resource names"
  type        = string
  default     = "benchmark"
}

variable "additional_tags" {
  description = "Additional tags specific to this module"
  type        = map(string)
  default     = {}
}

variable "instance_type" {
  description = "EC2 instance type for the benchmark host"
  type        = string
  default     = "t2.micro"
}

variable "volume_type" {
  description = "Volume type to use for the benchmark host"
  type        = string
  default     = "gp3"
}

variable "ssh_key_name" {
  description = "Name of the SSH key pair to use for the instance"
  type        = string
}

variable "efs_mount_target_ip" {
  description = "IP address of the EFS filesystem to mount, if any"
  type        = string
  default     = ""
}

variable "benchmark_results_bucket_name" {
  description = "Name of the bucket to use for benchmark results"
  type        = string
}

variable "network_config" {
  type = object({
    vpc_id    = string
    subnet_id = string
  })
  description = "Network configuration to use for the EFS (VPC:Subnet + Security Group)"
}

# Output
variable "config_file_path" {
  description = "Path to the local directory to use as the configuration path"
  type        = string
  default     = null
}
