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

variable "additional_common_tags" {
  description = "Additional common tags to take into consideration (ex: github run id)"
  type        = map(string)
  default     = {}
}

variable "benchmark_runner" {
  description = "Parameters for the benchmark runner"
  type = object({
    instance_type = optional(string, "c7a.8xlarge")
  })
  default = {}
}

variable "localstorage_benchmark" {
  description = "Parameters for the local storage benchmark (LocalStorage Adapter)"
  type        = object({
    fs_path     = optional(string, "/tmp/localstorage_benchtemp")  
  })
  default     = null
}

variable "redis_benchmark" {
  description = "Parameters for the Redis benchmark (Redis Adapter)"
  type = object({
    instance_type = optional(string, "cache.m5.xlarge")
  })
  default = null
}

variable "efs_benchmark" {
  description = "Parameters for the EFS benchmark (LocalStorage Adapter)"
  type        = object({})
  default     = null
}

variable "s3_benchmark" {
  description = "Parameters for the S3 benchmark (S3 Adapter)"
  type        = object({})
  default     = null
}

variable "sqs_benchmark" {
  description = "Parameters for the SQS benchmark (SQS Adapter)"
  type        = object({})
  default     = null
}

variable "rabbitmq_amq_benchmark" {
  description = "Parameters for the RabbitMQ[AmazonMQ] benchmark (RabbitMQ Adapter)"
  type = object({
    instance_type     = optional(string, "mq.m5.4xlarge")
    username_override = optional(string, "rabbitmqbench")
    password_override = optional(string, "rabbitmqbench")
  })
  default = null
}

variable "rabbitmq_ec2_benchmark" {
  description = "Parameters for the RabbitMQ[EC2] benchmark (RabbitMQ Adapter)"
  type = object({
    instance_type = optional(string, "m5.4xlarge")
  })
  default = null
}

variable "activemq_benchmark" {
  description = "Parameters for the ActiveMQ[AmazonMQ] benchmark (ActiveMQ Adapter)"
  type = object({
    instance_type     = optional(string, "mq.m5.4xlarge")
    username_override = optional(string, "activemqbench")
    password_override = optional(string, "activemqbench")
  })
  default = null
}
