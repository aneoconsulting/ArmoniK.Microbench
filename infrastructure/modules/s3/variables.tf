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
  description = "Prefix for S3 bucket names"
  type        = string
  default     = "benchmonik"
}

variable "additional_s3_config" {
  description = "Additional S3 configuration options"
  type        = map(string)
  default     = {}
}

variable "benchmark_runner_role_id" {
  description = "Id of the role created in the benchmark runner"
  type        = string
}

# Output
variable "config_file_path" {
  description = "Path to the local directory to use as the configuration path"
  type        = string
  default     = null
}
