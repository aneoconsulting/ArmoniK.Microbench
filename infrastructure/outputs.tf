output "benchmark_private_key_pem" {
  description = "The private key in PEM format"
  value       = tls_private_key.benchmark_key.private_key_pem
  sensitive   = true
}

resource "local_file" "private_key_pem" {
  filename        = "${path.root}/generated/benchmark_key.pem"
  file_permission = "0600"
  content         = tls_private_key.benchmark_key.private_key_pem
}

output "benchmark_instance_ip" {
  description = "Public IP of the EC2 instance"
  value       = module.benchmark_runner.instance_public_ip
}

output "benchmark_instance_public_dns" {
  description = "Public DNS of the EC2 instance"
  value       = module.benchmark_runner.instance_public_dns
}
