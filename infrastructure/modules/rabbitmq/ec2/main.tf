provider "aws" {
  region = var.region
  profile = var.profile
}

locals {
  config_file_path = coalesce(var.config_file_path, "${path.root}/benchmark_configs/rabbitmq.json")
  username = try(coalesce(var.rabbitmq_username), random_string.username.result)
  password = try(coalesce(var.rabbitmq_password), random_password.password.result)

  tags = merge(var.additional_tags, {
    Module = "RabbitMQ-EC2"
  })

  common_tags = {
    Name       = "RabbitMQ Benchmarks"
    Deployment = "${var.prefix}-armonik-microbench"
  }
}

resource "random_string" "username" {
  length           = 8
  special          = false
  upper            = false
}

resource "random_password" "password" {
  length           = 16
  special          = true
  lower            = true
  upper            = true
  numeric          = true
  override_special = "!@#$%&*()-_+.{}<>?"
}

resource "aws_security_group" "rabbitmq_sg" {
  name        = "${var.prefix}-rabbitmq-sg"
  description = "Security group for RabbitMQ EC2 instance"
  vpc_id      = var.network_config.vpc_id

  ingress {
    description = "TCP from RabbitMQ"
    from_port   = 5672
    to_port     = 5672
    protocol    = "tcp"
    cidr_blocks = ["0.0.0.0/0"]
  }

  ingress {
    description = "Web console for RabbitMQ"
    from_port   = 15672
    to_port     = 15672
    protocol    = "tcp"
    cidr_blocks = ["0.0.0.0/0"]
  }

  egress {
    from_port   = 0
    to_port     = 0
    protocol    = "-1"
    cidr_blocks = ["0.0.0.0/0"]
  }

  tags = local.tags
}

data "aws_ami" "ubuntu_2204" {
  most_recent = true
  owners      = ["099720109477"] # Canonical
  filter {
    name   = "name"
    values = ["ubuntu/images/hvm-*/ubuntu-jammy-22.04-amd64-server-*"]
  }
}

resource "aws_instance" "rabbitmq" {
  ami           = data.aws_ami.ubuntu_2204.id
  instance_type = var.instance_type
  subnet_id     = var.network_config.subnet_id
  vpc_security_group_ids = [aws_security_group.rabbitmq_sg.id]
  associate_public_ip_address = true
 
  user_data = base64encode(templatefile("${path.module}/user_data.sh.tftpl", {
    rabbitmq_username = local.username
    rabbitmq_password = local.password
  }))

  root_block_device {
    volume_type           = "gp3"
    volume_size           = 20
    delete_on_termination = true
    encrypted             = true
    
    tags = local.tags
  }


  # User data replacement requires instance replacement
  user_data_replace_on_change = true

  tags = local.tags
} 


## Optionals :

resource "aws_cloudwatch_log_group" "rabbitmq_logs" {
  count             = var.enable_cloudwatch_logs ? 1 : 0
  name              = "/aws/ec2/${var.prefix}-rabbitmq"
  retention_in_days = 30

  tags = local.tags
}

# Optional: CloudWatch agent configuration for log shipping
resource "aws_ssm_parameter" "cloudwatch_config" {
  count = var.enable_cloudwatch_logs ? 1 : 0
  name  = "/aws/ec2/${var.prefix}/cloudwatch-config"
  type  = "String"
  tags = local.tags
  value = jsonencode({
    logs = {
      logs_collected = {
        files = {
          collect_list = [
            {
              file_path      = "/var/log/rabbitmq-install.log"
              log_group_name = aws_cloudwatch_log_group.rabbitmq_logs[0].name
              log_stream_name = "installation"
            },
            {
              file_path      = "/var/log/rabbitmq/*.log"
              log_group_name = aws_cloudwatch_log_group.rabbitmq_logs[0].name
              log_stream_name = "application"
            }
          ]
        }
      }
    }
  })
}
