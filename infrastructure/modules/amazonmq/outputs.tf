locals {
  # Find the AMQP+SSL endpoint for ActiveMQ
  selected_endpoint = var.engine_type == "ActiveMQ" ? [
    for endpoint in aws_mq_broker.mq.instances[0].endpoints : endpoint
    if length(regexall(":5671$", endpoint)) > 0
  ][0] : aws_mq_broker.mq.instances[0].endpoints[0]
}

resource "local_file" "amq_benchmark" {
  filename = "${path.root}/benchmark_configs/${lower(var.engine_type)}.json"
  content = jsonencode({
    "Component"       = "${var.engine_type}"
    "Amqp:Endpoint"   = local.selected_endpoint
    "Amqp:Host"       = trim(split(":", local.selected_endpoint)[1], "//")
    "Amqp:Port"       = tonumber(split(":", local.selected_endpoint)[2])
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