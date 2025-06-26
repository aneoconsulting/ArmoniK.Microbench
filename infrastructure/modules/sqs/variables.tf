variable "region" {
  description = "AWS region where the queues will be created"
  type        = string
  default     = "us-east-1"
}

variable "profile" {
  description = "AWS profile to use for authentication"
  type        = string
  default     = "default"
}

variable "prefix" {
  description = "Prefix to use for the SQS queue"
  type        = string
  default     = "benchmonik"
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
