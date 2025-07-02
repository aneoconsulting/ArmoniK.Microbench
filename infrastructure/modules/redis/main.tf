locals {
  config_file_path = coalesce(var.config_file_path, "${path.root}/benchmark_configs/redis.json")

  common_tags = {
    Name       = "Redis Benchmarks"
    Deployment = "${var.prefix}-armonik-microbench"
  }
}

provider "aws" {
  region  = var.region
  profile = var.profile
}

resource "aws_security_group" "redis_sg" {
  name        = "${var.prefix}-redis-sg"
  description = "Security group for Redis cluster"

  ingress {
    from_port   = 6379 
    to_port     = 6379
    protocol    = "tcp"
    cidr_blocks = ["0.0.0.0/0"]  
  }
  
  tags = local.common_tags
}

resource "aws_elasticache_cluster" "benchmonik_redis" {
  cluster_id           = "${var.prefix}-redis-cluster"  
  engine               = "redis"
  node_type            = var.node_type  
  num_cache_nodes      = 1
  parameter_group_name = var.param_group_name
  engine_version       = var.engine_version         
  apply_immediately    = true
  port                 = 6379

  security_group_ids   = [aws_security_group.redis_sg.id]  

  tags = local.common_tags
}
