output "efs_id" {
  description = "ID of the created EFS file system"
  value       = aws_efs_file_system.benchmark_fs.id
}

output "efs_mount_target_ip" {
  description = "IP address of the EFS mount targets"
  value       = aws_efs_mount_target.benchmark_fs_target.ip_address
}

resource "local_file" "efs_benchmark" {
  filename = local.config_file_path
  content = jsonencode({
    "Component"         = "LocalStorage"
    "LocalStorage:Path" = "/mnt/efs"
    "ResourceMetadata" = {
      "Arn"        = aws_efs_file_system.benchmark_fs.arn
      "ResourceId" = aws_efs_file_system.benchmark_fs.id
    }
  })
}
