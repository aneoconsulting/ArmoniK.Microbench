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
  description = "Prefix to use for the AWS resouces"
  type        = string
}

# TODO: If this isn't supplied, we can create the bucket and then store it in a results_config.json file.
variable "results_bucket_name" {
  description = "Name of the AWS S3 bucket to store the benchmark results in"
  type      = string
  default = "armonik-microbench-results"
}
