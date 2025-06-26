resource "local_file" "s3_benchmark" {
  filename = local.config_file_path
  content = jsonencode(
    merge({
      "Component"      = "S3"
      "S3:BucketName"  = aws_s3_bucket.s3_benchmark.bucket
      "S3:EndpointUrl" = "https://s3.${aws_s3_bucket.s3_benchmark.region}.amazonaws.com"
      "S3:Region"      = var.region
      "S3:Profile"     = var.profile
      "S3:MustForcePathStyle" = true
      "ResourceMetadata" = {
        "Arn"                  = aws_s3_bucket.s3_benchmark.arn
        "ResourceId"           = aws_s3_bucket.s3_benchmark.id
       }
    }, var.additional_s3_config)
  )
}
