locals {

  config_file_path = coalesce(var.config_file_path, "${path.root}/benchmark_configs/runners/benchmark_runner.json")

  common_tags = {
    Name       = "Benchmark Runner"
    Deployment = "${var.prefix}-armonik-microbench"
  }

  tags = merge(var.additional_tags, {
    Module = "BenchmarkRunner"
  })

  base_userdata = templatefile("${path.module}/user_data.sh.tftpl", {})

  inner_efs_config = <<-EOF
    ## EFS SPECIFIC 
    # Install NFS client for EFS
    sudo apt-get install -y nfs-common

    # Create EFS mount directory
    sudo mkdir -p /mnt/efs

    # Mount EFS filesystem
    sudo mount -t nfs4 -o nfsvers=4.1,rsize=1048576,wsize=1048576,hard,timeo=600,retrans=2,noresvport ${var.efs_mount_target_ip}:/ /mnt/efs

    # Add to fstab for persistence across reboots
    echo "${var.efs_mount_target_ip}:/ /mnt/efs nfs4 nfsvers=4.1,rsize=1048576,wsize=1048576,hard,timeo=600,retrans=2,noresvport 0 0" | sudo tee -a /etc/fstab

    # Set proper permissions
    sudo chown ubuntu:ubuntu /mnt/efs
  EOF

  efs_config = var.efs_mount_target_ip != "" ? local.inner_efs_config : ""
  userdata   = "${local.base_userdata}${local.efs_config}"

}


resource "aws_iam_role" "benchmark_role" {
  name = "${var.prefix}-benchmark-role"

  assume_role_policy = jsonencode({
    Version = "2012-10-17"
    Statement = [
      {
        Action = "sts:AssumeRole"
        Effect = "Allow"
        Principal = {
          Service = "ec2.amazonaws.com"
        }
      }
    ]
  })
}

resource "aws_iam_instance_profile" "benchmark_profile" {
  name = "${var.prefix}-benchmark-profile"
  role = aws_iam_role.benchmark_role.name
}


resource "aws_iam_role_policy" "s3_benchmark_results_bucket_access" {
  name = "${var.prefix}-benchmark-output-bucket-policy"
  role = aws_iam_role.benchmark_role.id

  policy = jsonencode({
    Version = "2012-10-17"
    Statement = [
      {
        Action = [
          "s3:*"
        ]
        Effect = "Allow"
        Resource = [
          "arn:aws:s3:::${var.benchmark_results_bucket_name}",
          "arn:aws:s3:::${var.benchmark_results_bucket_name}/*",
        ]
      }
    ]
  })
}

# Security group for EC2 instance
resource "aws_security_group" "benchmark_sg" {
  name        = "${var.prefix}-benchmark-sg"
  description = "Security group for benchmark EC2 instance"
  vpc_id      = var.network_config.vpc_id

  # SSH access
  ingress {
    from_port   = 22
    to_port     = 22
    protocol    = "tcp"
    cidr_blocks = ["0.0.0.0/0"]
  }

  # Outbound internet access
  egress {
    from_port   = 0
    to_port     = 0
    protocol    = "-1"
    cidr_blocks = ["0.0.0.0/0"]
  }

  tags = local.tags
}

# Ubuntu 24.04 LTS AMI
data "aws_ami" "ubuntu_2204" {
  most_recent = true
  owners      = ["099720109477"] # Canonical
  filter {
    name   = "name"
    values = ["ubuntu/images/hvm-*/ubuntu-noble-24.04-amd64-server-*"]
  }
}

resource "aws_instance" "benchmark_instance" {
  ami           = data.aws_ami.ubuntu_2204.id
  instance_type = var.instance_type
  key_name      = var.ssh_key_name

  subnet_id                   = var.network_config.subnet_ids[0]
  vpc_security_group_ids      = [aws_security_group.benchmark_sg.id]
  iam_instance_profile        = aws_iam_instance_profile.benchmark_profile.name
  associate_public_ip_address = true

  user_data = base64encode(local.userdata)

  root_block_device {
    volume_size = 40 # More space for benchmarks and artifacts
    volume_type = var.volume_type
    tags = local.tags
  }

  tags = local.tags
}
