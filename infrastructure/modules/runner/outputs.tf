output "instance_id" {
  description = "ID of the EC2 instance"
  value       = aws_instance.benchmark_instance.id
}

output "instance_public_ip" {
  description = "Public IP of the EC2 instance"
  value       = aws_instance.benchmark_instance.public_ip
}

output "instance_public_dns" {
  description = "Public DNS of the EC2 instance"
  value       = aws_instance.benchmark_instance.public_dns
}

output "benchmark_runner_role_id" {
  description = "Id of the role created for the benchmark runner"
  value       = aws_iam_role.benchmark_role.id
}

# TODO: Remove this, no need for this when we can just allow all. This is garbanzo
output "benchmark_runner_sg_id" {
  description = "ID of the benchmark runner's security group"
  value = aws_security_group.benchmark_sg.id
}

resource "local_file" "runner_config" {
  filename = local.config_file_path
  content = jsonencode({
    "host" = aws_instance.benchmark_instance.public_dns
    "key"  = "${abspath(path.root)}/generated/benchmark_key.pem"
    "ResourceMetadata" = {
      "Arn"        = aws_instance.benchmark_instance.arn
      "ResourceId" = aws_instance.benchmark_instance.id
      "NodeType"   = var.instance_type
      "VolumeType" = var.volume_type
    }
  })
}
