resource "local_file" "s3_benchmark" {
  filename = local.config_file_path
  content = jsonencode({
    "Component"        = "LocalStorage"
    "LocalStorage:Path" = var.storage_path
  })
}
