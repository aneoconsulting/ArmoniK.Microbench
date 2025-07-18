provider "aws" {
  region  = var.region
  profile = var.profile
}

locals {
  network_config = {
    vpc_id    = module.vpc.vpc_id
    subnet_id = module.vpc.public_subnets[0]
  }
  common_tags = {
    application          = "Microbenchmarks"
    "deployment version" = "${var.prefix}-armonik-microbench"
  }
}

# SSH Keys
resource "tls_private_key" "benchmark_key" {
  algorithm = "RSA"
  rsa_bits  = 2048
}

resource "aws_key_pair" "benchmark_key" {
  key_name   = "${var.prefix}-benchmark-key"
  public_key = tls_private_key.benchmark_key.public_key_openssh
}

# VPC
data "aws_availability_zones" "available" {}

module "vpc" {
  source               = "terraform-aws-modules/vpc/aws"
  name                 = "${var.prefix}-benchmark-vpc"
  cidr                 = "10.0.0.0/16"
  azs                  = data.aws_availability_zones.available.names
  public_subnets       = ["10.0.1.0/24", "10.0.2.0/24"]
  enable_dns_hostnames = true
  enable_dns_support   = true
}

# Benchmark Runners

module "benchmark_runner" {
  source                        = "./modules/runner"
  region                        = var.region
  profile                       = var.profile
  additional_tags               = local.common_tags
  network_config                = local.network_config
  benchmark_results_bucket_name = var.results_bucket_name
  ssh_key_name                  = aws_key_pair.benchmark_key.key_name
  instance_type                 = "c7a.8xlarge"
}

# Benchmarks to run

# module "localstorage_benchmark" {
#   source       = "./modules/localstorage/localfs"
#   storage_path = "/localstorage_benchtemp"
# }

# module "redis_benchmark" {
#   source          = "./modules/redis"
#   prefix          = var.prefix
#   region          = var.region
#   profile         = var.profile
#   additional_tags = local.common_tags
#   node_type       = "cache.m5.xlarge"
# }

# module "efs_benchmark" {
#   source                     = "./modules/localstorage/efs"
#   prefix                     = var.prefix
#   region                     = var.region
#   profile                    = var.profile
#   additional_tags            = local.common_tags
#   network_config             = local.network_config
#   instance_security_group_id = module.benchmark_runner.benchmark_runner_sg_id
# }

# module "s3_benchmark" {
#   source                   = "./modules/s3"
#   prefix                   = var.prefix
#   region                   = var.region
#   profile                  = var.profile
#   additional_tags          = local.common_tags
#   benchmark_runner_role_id = module.benchmark_runner.benchmark_runner_role_id
# }

## Queue:

module "sqs_queue" {
  source                   = "./modules/sqs"
  prefix                   = var.prefix
  region                   = var.region
  profile                  = var.profile
  additional_tags          = local.common_tags
  benchmark_runner_role_id = module.benchmark_runner.benchmark_runner_role_id
}

# module "rabbitmq-amq" {
#   source                 = "./modules/amazonmq"
#   prefix                 = var.prefix
#   region                 = var.region
#   profile                = var.profile
#   additional_tags        = local.common_tags
#   mq_username_override   = "sleeprabbitmqbench"
#   mq_password_override   = "sleeprabbitmqbench"
#   network_config         = local.network_config
#   host_instance_type     = "mq.m5.4xlarge"
#   benchmark_runner_sg_id = module.benchmark_runner.benchmark_runner_sg_id
# }

# module "rabbitmq-ec2" {
#   source           = "./modules/rabbitmq/ec2"
#   prefix           = var.prefix # TODO: Group these into an object, add to locals, makes it easier to read.
#   region           = var.region
#   profile          = var.profile
#   additional_tags  = local.common_tags
#   network_config   = local.network_config
#   instance_type    = "m5.4xlarge"
#   config_file_path = "${path.root}/benchmark_configs/rabbitmq-ec2.json"
# }

# module "activemq" {
#   source                 = "./modules/amazonmq"
#   prefix                 = var.prefix
#   region                 = var.region
#   profile                = var.profile
#   additional_tags        = local.common_tags
#   mq_username_override   = "sleepactivemqbench"
#   mq_password_override   = "sleepactivemqbench"
#   host_instance_type     = "mq.m5.4xlarge"
#   engine_type            = "ActiveMQ"
#   engine_version         = "5.18"
#   network_config         = local.network_config
#   benchmark_runner_sg_id = module.benchmark_runner.benchmark_runner_sg_id
# }
