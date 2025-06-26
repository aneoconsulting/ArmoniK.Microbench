resource "local_file" "s3_benchmark" {
  filename = local.config_file_path
  content = jsonencode(
    {
      "Component"      = "SQS"
      "SQS:ServiceURL" = local.service_url
      "SQS:Prefix"     = var.prefix
      "ResourceMetadata" = {}
    }
  )
}
