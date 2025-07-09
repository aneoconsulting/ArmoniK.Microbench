locals {
  config_file_path = coalesce(var.config_file_path, "${path.root}/benchmark_configs/s3.json")
  common_tags = {
    Name       = "S3 Benchmarks"
    Deployment = "${var.prefix}-armonik-microbench"
  }

  tags = merge(var.additional_tags, {
    Module = "S3"
  })
}

provider "aws" {
  region  = var.region
  profile = var.profile
}

# Random string to ensure unique bucket names
resource "random_string" "bucket_suffix" {
  length  = 8
  special = false
  upper   = false
}

# Create buckets for different benchmark types
resource "aws_s3_bucket" "s3_benchmark" {
  bucket = "${var.prefix}-general-${random_string.bucket_suffix.result}"
  force_destroy = true
  tags = local.tags
}


resource "aws_iam_role_policy" "benchmark_policy" {
  name = "${var.prefix}-s3-benchmark-bucket-policy"
  role = var.benchmark_runner_role_id

  policy = jsonencode({
    Version = "2012-10-17"
    Statement = [
      {
        Action = [
          "s3:*"
        ]
        Effect = "Allow"
        Resource = [
          "arn:aws:s3:::${aws_s3_bucket.s3_benchmark.bucket}",
          "arn:aws:s3:::${aws_s3_bucket.s3_benchmark.bucket}/*",
        ]
      }
    ]
  })
}
