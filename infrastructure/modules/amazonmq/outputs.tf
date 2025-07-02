resource "local_file" "amq_benchmark" {
  filename = "${path.root}/benchmark_configs/${lower(var.engine_type)}.json"
  content = jsonencode({
    "Component"       = "${var.engine_type}"
    "Amqp:Endpoint"   = aws_mq_broker.mq.instances[0].endpoints[0]
    "Amqp:Host"       = trim(split(":", aws_mq_broker.mq.instances[0].endpoints[0])[1], "//")
    "Amqp:Port"       = tonumber(split(":", aws_mq_broker.mq.instances[0].endpoints[0])[2])
    "Amqp:Scheme"     = "AMQPS"
    "Amqp:User"       = local.username
    "Amqp:Password"   = local.password
    "Amqp:MaxRetries" = 1
    "ResourceMetadata" = {
      "Arn" = aws_mq_broker.mq.arn
      "ResourceId" = aws_mq_broker.mq.id 
      "EngineType" = var.engine_type 
      "EngineVersion" = var.engine_version
      "NodeType" = var.host_instance_type
      "StorageType" = "ebs"
    }
  })
}
