output "redis_endpoint" {
  description = "Endpoint for Redis connection"
  value       = aws_elasticache_cluster.benchmonik_redis.cache_nodes[0].address
}

output "redis_port" {
  description = "Port for Redis connection"
  value       = aws_elasticache_cluster.benchmonik_redis.port
}

resource "local_file" "redis_benchmark" {
  filename = local.config_file_path
  content = jsonencode({
    "Component"          = "Redis"
    "Redis:EndpointUrl"  = "${aws_elasticache_cluster.benchmonik_redis.cache_nodes[0].address}:${aws_elasticache_cluster.benchmonik_redis.port}"
    "Redis:ClientName"   = "BenchmoniK"
    "Redis:InstanceName" = "benchmark"
    "Redis:MaxRetry"     = 5
    "Redis:MsAfterRetry" = 500
    "Redis:Timeout"      = 5000
    "Redis:Ssl"          = false
    "ResourceMetadata" = {
      "Arn"            = aws_elasticache_cluster.benchmonik_redis.arn
      "ResourceId"     = aws_elasticache_cluster.benchmonik_redis.id
      "NodeType"       = var.node_type
      "EngineVersion"  = var.engine_version
      "ParamGroupName" = var.param_group_name
    }
  })
}
