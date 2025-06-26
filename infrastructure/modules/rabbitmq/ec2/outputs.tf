output "rabbitmq_instance_id" {
  value = aws_instance.rabbitmq.id
}

output "rabbitmq_private_ip" {
  value = aws_instance.rabbitmq.private_ip
}

output "rabbitmq_public_ip" {
  value = aws_instance.rabbitmq.public_ip
}

output "rabbitmq_username" {
  value = local.username
}

output "rabbitmq_password" {
  value = local.password
}

output "rabbitmq_management_port" {
  value = 15672
}

output "rabbitmq_amqp_port" {
  value = 5672
}

resource "local_file" "rabbitmq_benchmark" {
  filename = local.config_file_path
  content = jsonencode({
    "Component"        = "RabbitMQ"
    "Amqp:Host"        = aws_instance.rabbitmq.private_ip
    "Amqp:Port"        = 5672
    "Amqp:Scheme"      = "AMQP"
    "Amqp:User"        = local.username
    "Amqp:Password"    = local.password
    "Amqp:MaxRetries"  = 1
    "ResourceMetadata" = {
      "Arn" = aws_instance.rabbitmq.arn 
      "ResourceId" = aws_instance.rabbitmq.id 
      "EngineVersion" = "NotFixed" #TODO Set RabbitMQ version + Maybe root block device
      "NodeType" = var.instance_type 
    }
    "Management" = {
      "Host" = aws_instance.rabbitmq.public_dns
      "IP" = aws_instance.rabbitmq.public_ip
      "Port" = 15672
    }
  })
} 
