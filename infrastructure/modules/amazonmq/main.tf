locals {
  username = try(coalesce(var.mq_username_override), random_string.username.result)
  password = try(coalesce(var.mq_password_override), random_password.password.result)

  final_broker_name = "${var.prefix}-${var.broker_name}-${var.engine_type}"

  deployment_machine_cidr = "${chomp(data.http.my_ip.body)}/32"

  tags = merge(var.additional_tags, {
    Module = "${lower(var.engine_type)}-AmazonMQ"
  })

}

data "http" "my_ip" {
  url = "http://ipv4.icanhazip.com"
}

resource "aws_security_group" "amq_sg" {
  name        = "${local.final_broker_name}-sg"
  description = "Allow broker access"
  vpc_id      = var.network_config.vpc_id

  ingress {
    description = "tcp from Amazon MQ"
    from_port   = 5671
    to_port     = 5671
    protocol    = "tcp"
    cidr_blocks = ["0.0.0.0/0"]
  }
  ingress {
    description = "Web console for Amazon MQ"
    from_port   = 8162
    to_port     = 8162
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

resource "aws_security_group_rule" "runner_to_amq_ingress_rule" {
  type                     = "ingress"
  to_port                  = 5671
  from_port                = 5671
  protocol                 = "tcp"
  source_security_group_id = var.benchmark_runner_sg_id
  security_group_id        = aws_security_group.amq_sg.id
}

resource "aws_security_group_rule" "runner_to_amq_egress_rule" {
  type                     = "egress"
  to_port                  = 5671
  from_port                = 5671
  protocol                 = "tcp"
  source_security_group_id = var.benchmark_runner_sg_id
  security_group_id        = aws_security_group.amq_sg.id
}

resource "aws_security_group_rule" "runner_to_openwire_ingress_rule" {
  type                     = "ingress"
  to_port                  = 61617
  from_port                = 61617
  protocol                 = "tcp"
  source_security_group_id = var.benchmark_runner_sg_id
  security_group_id        = aws_security_group.amq_sg.id
}

resource "aws_security_group_rule" "runner_to_openwire_egress_rule" {
  type                     = "egress"
  to_port                  = 61617
  from_port                = 61617
  protocol                 = "tcp"
  source_security_group_id = var.benchmark_runner_sg_id
  security_group_id        = aws_security_group.amq_sg.id
}


# Generate username
resource "random_string" "username" {
  length           = 8
  special          = true
  numeric          = false
  override_special = "-._~"
}

# Generate password
resource "random_password" "password" {
  length           = 16
  special          = true
  lower            = true
  upper            = true
  numeric          = true
  override_special = "!@#$%&*()-_+.{}<>?"
}

resource "aws_mq_broker" "mq" {

  broker_name                = local.final_broker_name
  subnet_ids                 = var.network_config.subnet_ids
  security_groups            = [aws_security_group.amq_sg.id]
  engine_type                = var.engine_type
  engine_version             = var.engine_version
  deployment_mode            = "SINGLE_INSTANCE"
  storage_type               = "ebs"
  host_instance_type         = var.host_instance_type
  apply_immediately          = true
  publicly_accessible        = false
  auto_minor_version_upgrade = true

  dynamic "configuration" {
    for_each = var.engine_type == "ActiveMQ" ? [1] : []
    content {
      id       = aws_mq_configuration.mq_configuration[0].id
      revision = aws_mq_configuration.mq_configuration[0].latest_revision
    }
  }

  user {
    username = local.username
    password = local.password
  }

  tags = local.tags
}


resource "aws_mq_configuration" "mq_configuration" {
  count          = var.engine_type == "ActiveMQ" ? 1 : 0
  tags           = local.tags
  description    = "ArmoniK ActiveMQ Configuration"
  name           = var.broker_name
  engine_type    = var.engine_type
  engine_version = var.engine_version
  data           = <<DATA
<?xml version="1.0" encoding="UTF-8" standalone="yes"?>
<broker xmlns="http://activemq.apache.org/schema/core">
  <persistenceAdapter>
    <kahaDB concurrentStoreAndDispatchQueues="false" journalDiskSyncInterval="10000" journalDiskSyncStrategy="periodic" preallocationStrategy="zeros"/>
  </persistenceAdapter>
  <systemUsage>
    <systemUsage sendFailIfNoSpace="true" sendFailIfNoSpaceAfterTimeout="60000">
      <memoryUsage>
        <memoryUsage percentOfJvmHeap="70"/>
      </memoryUsage>
    </systemUsage>
  </systemUsage>
  <destinationPolicy>
    <policyMap>
      <policyEntries>
        <policyEntry prioritizedMessages="true" queue="&gt;"/>
        <policyEntry topic="&gt;">
          <!-- The constantPendingMessageLimitStrategy is used to prevent
            slow topic consumers to block producers and affect other consumers
            by limiting the number of messages that are retained
            For more information, see:
            http://activemq.apache.org/slow-consumer-handling.html
        -->
          <pendingMessageLimitStrategy>
            <constantPendingMessageLimitStrategy limit="100000000"/>
          </pendingMessageLimitStrategy>
        </policyEntry>
      </policyEntries>
    </policyMap>
  </destinationPolicy>
</broker>
DATA
}
