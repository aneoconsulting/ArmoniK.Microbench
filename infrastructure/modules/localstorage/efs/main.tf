locals {
  config_file_path = coalesce(var.config_file_path, "${path.root}/benchmark_configs/efs.json")
  # common_tags = {
  #   Name       = "EFS Benchmarks"
  #   Deployment = "${var.prefix}-armonik-microbench"
  # }

  tags = merge(var.additional_tags, {
    Module = "EFS"
  })

}

provider "aws" {
  region  = var.region
  profile = var.profile
}

resource "aws_efs_file_system" "benchmark_fs" {
  performance_mode = "generalPurpose"
  tags             = local.tags
}

resource "aws_security_group" "efs_sg" {
  name        = "${var.prefix}-efs-sg"
  description = "Allow NFS traffic for EFS"
  vpc_id      = var.network_config.vpc_id

  ingress {
    from_port       = 2049 # NFS port
    to_port         = 2049
    protocol        = "tcp"
    security_groups = [var.instance_security_group_id]
  }

  egress {
    from_port   = 0
    to_port     = 0
    protocol    = "-1"
    cidr_blocks = ["0.0.0.0/0"]
  }

  tags = local.tags
}

resource "aws_efs_mount_target" "benchmark_fs_target" {
  file_system_id  = aws_efs_file_system.benchmark_fs.id
  subnet_id       = var.network_config.subnet_id
  security_groups = [aws_security_group.efs_sg.id]
}
