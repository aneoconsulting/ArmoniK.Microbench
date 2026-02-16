provider "aws" {
  region  = var.region
  # profile = var.profile
}

locals {
  network_config = {
    vpc_id     = module.vpc.vpc_id
    subnet_id  = module.vpc.public_subnets[0]
    subnet_ids = module.vpc.public_subnets 
  }
  common_tags = merge(
    {
      application          = "Microbenchmarks"
      "deployment version" = "${var.prefix}-armonik-microbench"
    },
    var.additional_common_tags
  )
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
  prefix                        = var.prefix
  additional_tags               = local.common_tags
  network_config                = local.network_config
  benchmark_results_bucket_name = var.results_bucket_name
  ssh_key_name                  = aws_key_pair.benchmark_key.key_name
  instance_type                 = var.benchmark_runner != null ? var.benchmark_runner.instance_type : "c7a.8xlarge"
  providers = {
    aws = aws
  }
}

# Storage Benchmarks

module "localstorage_benchmark" {
  count        = var.localstorage_benchmark != null ? 1 : 0
  source       = "./modules/localstorage/localfs"
  storage_path = var.localstorage_benchmark.fs_path
}

module "redis_benchmark" {
  count           = var.redis_benchmark != null ? 1 : 0
  source          = "./modules/redis"
  prefix          = var.prefix
  region          = var.region
  profile         = var.profile
  additional_tags = local.common_tags
  node_type       = var.redis_benchmark.instance_type
  network_config  = local.network_config
  providers = {
    aws = aws
  }
}

module "efs_benchmark" {
  count                      = var.efs_benchmark != null ? 1 : 0
  source                     = "./modules/localstorage/efs"
  prefix                     = var.prefix
  region                     = var.region
  profile                    = var.profile
  additional_tags            = local.common_tags
  network_config             = local.network_config
  instance_security_group_id = module.benchmark_runner.benchmark_runner_sg_id
  providers = {
    aws = aws
  }
}

module "s3_benchmark" {
  count                    = var.s3_benchmark != null ? 1 : 0
  source                   = "./modules/s3"
  prefix                   = var.prefix
  region                   = var.region
  profile                  = var.profile
  additional_tags          = local.common_tags
  benchmark_runner_role_id = module.benchmark_runner.benchmark_runner_role_id
  providers = {
    aws = aws
  }
}

# Queue Benchmarks

module "sqs_queue" {
  count                    = var.sqs_benchmark != null ? 1 : 0
  source                   = "./modules/sqs"
  prefix                   = var.prefix
  region                   = var.region
  profile                  = var.profile
  additional_tags          = local.common_tags
  benchmark_runner_role_id = module.benchmark_runner.benchmark_runner_role_id
  providers = {
    aws = aws
  }
}

module "rabbitmq_amq" {
  count                  = var.rabbitmq_amq_benchmark != null ? 1 : 0
  source                 = "./modules/amazonmq"
  prefix                 = var.prefix
  region                 = var.region
  profile                = var.profile
  additional_tags        = local.common_tags
  mq_username_override   = var.rabbitmq_amq_benchmark.username_override
  mq_password_override   = var.rabbitmq_amq_benchmark.password_override
  network_config         = local.network_config
  host_instance_type     = var.rabbitmq_amq_benchmark.instance_type
  benchmark_runner_sg_id = module.benchmark_runner.benchmark_runner_sg_id
  providers = {
    aws = aws
  }
}

module "rabbitmq_ec2" {
  count            = var.rabbitmq_ec2_benchmark != null ? 1 : 0
  source           = "./modules/rabbitmq/ec2"
  prefix           = var.prefix
  region           = var.region
  profile          = var.profile
  additional_tags  = local.common_tags
  network_config   = local.network_config
  instance_type    = var.rabbitmq_ec2_benchmark.instance_type
  config_file_path = "${path.root}/benchmark_configs/rabbitmq-ec2.json"
  providers = {
    aws = aws
  }
}

module "activemq" {
  count                  = var.activemq_benchmark != null ? 1 : 0
  source                 = "./modules/amazonmq"
  prefix                 = var.prefix
  region                 = var.region
  profile                = var.profile
  additional_tags        = local.common_tags
  mq_username_override   = var.activemq_benchmark.username_override
  mq_password_override   = var.activemq_benchmark.password_override
  host_instance_type     = var.activemq_benchmark.instance_type
  engine_type            = "ActiveMQ"
  engine_version         = "5.18"
  network_config         = local.network_config
  benchmark_runner_sg_id = module.benchmark_runner.benchmark_runner_sg_id
  providers = {
    aws = aws
  }
}
