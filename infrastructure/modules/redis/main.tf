locals {
  config_file_path = coalesce(var.config_file_path, "${path.root}/benchmark_configs/redis.json")

  common_tags = {
    Name       = "Redis Benchmarks"
    Deployment = "${var.prefix}-armonik-microbench"
  }

  tags = merge(var.additional_tags, {
    Module = "Redis"
  })
}

resource "aws_security_group" "redis_sg" {
  name        = "${var.prefix}-redis-sg"
  description = "Security group for Redis cluster"
  vpc_id      = var.network_config.vpc_id

  ingress {
    from_port   = 6379 
    to_port     = 6379
    protocol    = "tcp"
    cidr_blocks = ["0.0.0.0/0"]  
  }
  
  tags = local.tags
}

resource "aws_elasticache_subnet_group" "redis_subnet" {
  name       = "${var.prefix}-redis-subnet"
  subnet_ids = var.network_config.subnet_ids
}

resource "aws_elasticache_cluster" "benchmonik_redis" {
  cluster_id           = "${var.prefix}-redis-cluster"  
  subnet_group_name    = aws_elasticache_subnet_group.redis_subnet.name
  engine               = "redis"
  node_type            = var.node_type  
  num_cache_nodes      = 1
  parameter_group_name = var.param_group_name
  engine_version       = var.engine_version         
  apply_immediately    = true
  port                 = 6379

  security_group_ids   = [aws_security_group.redis_sg.id]  

  tags = local.tags
}
