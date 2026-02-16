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
  description = "Prefix for AmazonMQ related resources"
  type        = string
}

variable "additional_tags" {
  description = "Additional tags specific to this module"
  type        = map(string)
  default     = {}
}

variable "mq_username_override" {
  description = "ActiveMQ username"
  type        = string
  default     = null
}

variable "mq_password_override" {
  description = "ActiveMQ password"
  type        = string
  default     = null
}

variable "network_config" {
  type = object({
    vpc_id    = string
    subnet_ids = list(string)
  })
  description = "Network configuration to use for the AmazonMQ (VPC:Subnet)"
}

variable "broker_name" {
  description = "Name to use for the AmazonMQ broker (Not prefixed)"
  type        = string
  default     = "benchbroker"
}

variable "host_instance_type" {
  description = "Instance type to use for the AmazonMQ broker"
  type        = string
  default     = "mq.m5.xlarge"
}

variable "engine_type" {
  description = "Type of the engine to use"
  type        = string
  default     = "RabbitMQ"
}

variable "engine_version" {
  description = "Version of the engine to use"
  type        = string
  default     = "3.13"
}

variable "benchmark_runner_sg_id" {
  description = "ID of the security group used by the benchmark runner"
  type        = string
}
