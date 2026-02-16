# Infrastructure

The `infrastructure/` directory contains modular Terraform code for deploying all AWS resources needed for benchmarking. Each ArmoniK adapter has its own Terraform module that provisions the corresponding AWS service and outputs a benchmark configuration JSON file.

## Architecture Overview

```
                        ┌─────────────────────────────────────────────┐
                        │                   VPC                       │
                        │                                             │
                        │  ┌───────────────────────────────────────┐  │
                        │  │          Public Subnets                │  │
                        │  │                                       │  │
┌──────────┐   SSH      │  │  ┌──────────────┐                    │  │
│ CLI /    │───────────>│  │  │  EC2 Runner   │                    │  │
│ CI Runner│            │  │  │  (.NET, AWS)  │                    │  │
└──────────┘            │  │  └──────┬───────┘                    │  │
                        │  │         │                             │  │
                        │  │    ┌────┴────┬──────────┬─────────┐  │  │
                        │  │    │         │          │         │  │  │
                        │  │    v         v          v         v  │  │
                        │  │ ┌──────┐ ┌──────┐ ┌────────┐ ┌────┐│  │
                        │  │ │Redis │ │Amazon│ │RabbitMQ│ │EFS ││  │
                        │  │ │Cache │ │  MQ  │ │  EC2   │ │    ││  │
                        │  │ └──────┘ └──────┘ └────────┘ └────┘│  │
                        │  └───────────────────────────────────────┘  │
                        └─────────────────────────────────────────────┘
                                         │
                         ┌───────────────┼───────────────┐
                         │               │               │
                         v               v               v
                      ┌──────┐      ┌────────┐     ┌─────────┐
                      │  S3  │      │  SQS   │     │ Results │
                      │Bucket│      │ Queues │     │   S3    │
                      └──────┘      └────────┘     └─────────┘
```

All resources are created within a dedicated VPC. The EC2 runner instance communicates with each service over the private network. S3 and SQS are accessed via public AWS endpoints from the runner's IAM role.

## How Modules Work

Each module follows the same pattern:

1. **Provisions AWS resources** (the service itself, security groups, IAM policies)
2. **Outputs a benchmark config JSON file** to `infrastructure/benchmark_configs/` -- this file is consumed by BenchmoniK at runtime
3. **Wires security groups** to allow the runner instance to communicate with the service

Modules are **conditionally deployed** using the `count` meta-argument. Setting a variable to `null` (or omitting it) disables the module entirely:

```hcl
# In parameters.tfvars:
redis_benchmark = { instance_type = "cache.m5.xlarge" }  # Deployed
s3_benchmark    = {}                                      # Deployed with defaults
sqs_benchmark   = null                                    # Not deployed
# efs_benchmark                                           # Omitted = not deployed
```

## Module Reference

### Runner (`modules/runner/`)

The runner module is **always deployed**. It creates the EC2 instance where benchmarks execute.

**Resources created:**

- EC2 instance (Ubuntu 24.04) with .NET SDK 8.0 + 10.0 and AWS CLI pre-installed
- IAM role + instance profile with S3 access to the results bucket
- Security group (SSH ingress, all egress)

**Variables:**

| Variable | Type | Default | Description |
|----------|------|---------|-------------|
| `instance_type` | string | `t2.micro` | EC2 instance type. Use `c7a.8xlarge` or similar for real benchmarks |
| `volume_type` | string | `gp3` | EBS volume type |
| `ssh_key_name` | string | (required) | AWS key pair name for SSH access |
| `benchmark_results_bucket_name` | string | (required) | S3 bucket name for uploading results |
| `efs_mount_target_ip` | string | `""` | EFS mount target IP. If set, the instance mounts EFS at `/mnt/efs` |
| `network_config` | object | (required) | `{ vpc_id, subnet_id }` |

**Config output:** `benchmark_configs/runners/benchmark_runner.json`

```json
{
  "host": "ec2-xx-xx-xx-xx.compute-1.amazonaws.com",
  "key": "/absolute/path/to/generated/benchmark_key.pem",
  "ResourceMetadata": {
    "Arn": "arn:aws:ec2:...",
    "ResourceId": "i-xxxx",
    "NodeType": "c7a.8xlarge",
    "VolumeType": "gp3"
  }
}
```

---

### Redis (`modules/redis/`)

Deploys an AWS ElastiCache Redis cluster for benchmarking the Redis object storage adapter.

**Resources created:**

- ElastiCache Redis cluster (single node)
- ElastiCache subnet group
- Security group (port 6379)

**Variables:**

| Variable | Type | Default | Description |
|----------|------|---------|-------------|
| `node_type` | string | `cache.t3.micro` | ElastiCache node type |
| `engine_version` | string | `7.0` | Redis engine version |
| `param_group_name` | string | `default.redis7` | Parameter group name |
| `network_config` | object | (required) | `{ vpc_id, subnet_id, subnet_ids }` |

**Config output:** `benchmark_configs/redis.json`

```json
{
  "Component": "Redis",
  "Redis:EndpointUrl": "xxx.cache.amazonaws.com:6379",
  "Redis:ClientName": "BenchmoniK",
  "Redis:InstanceName": "benchmark",
  "Redis:MaxRetry": 5,
  "Redis:MsAfterRetry": 500,
  "Redis:Timeout": 5000,
  "Redis:Ssl": false,
  "ResourceMetadata": { ... }
}
```

---

### S3 (`modules/s3/`)

Creates an S3 bucket for benchmarking the S3 object storage adapter.

**Resources created:**

- S3 bucket (with random suffix for uniqueness)
- IAM policy granting the runner role access to the bucket

**Variables:**

| Variable | Type | Default | Description |
|----------|------|---------|-------------|
| `benchmark_runner_role_id` | string | (required) | IAM role ID of the runner (for policy attachment) |
| `additional_s3_config` | map | `{}` | Extra key-value pairs merged into the config output |

**Config output:** `benchmark_configs/s3.json`

```json
{
  "Component": "S3",
  "S3:BucketName": "prefix-s3-benchmark-xxxx",
  "S3:EndpointUrl": "https://s3.us-east-1.amazonaws.com",
  "S3:Region": "us-east-1",
  "S3:Profile": "default",
  "S3:MustForcePathStyle": true,
  "ResourceMetadata": { ... }
}
```

---

### LocalStorage - Local FS (`modules/localstorage/localfs/`)

The simplest module. It just generates a config file pointing BenchmoniK at a local filesystem path on the runner. No AWS resources are created.

**Variables:**

| Variable | Type | Default | Description |
|----------|------|---------|-------------|
| `storage_path` | string | (required) | Filesystem path on the runner to use for storage |

**Config output:** `benchmark_configs/localstorage.json`

```json
{
  "Component": "LocalStorage",
  "LocalStorage:Path": "/tmp/localstorage_benchtemp"
}
```

---

### LocalStorage - EFS (`modules/localstorage/efs/`)

Deploys an AWS EFS filesystem for benchmarking the LocalStorage adapter over a network filesystem.

**Resources created:**

- EFS file system (generalPurpose performance mode)
- Security group (NFS port 2049, restricted to the runner SG)
- EFS mount target in the benchmark subnet

**Variables:**

| Variable | Type | Default | Description |
|----------|------|---------|-------------|
| `instance_security_group_id` | string | (required) | Runner's security group ID (for NFS ingress rule) |
| `network_config` | object | (required) | `{ vpc_id, subnet_id }` |

**Config output:** `benchmark_configs/efs.json`

```json
{
  "Component": "LocalStorage",
  "LocalStorage:Path": "/mnt/efs"
}
```

!!! note
    The EFS mount target IP must be passed to the runner module's `efs_mount_target_ip` variable so that the runner's user data script mounts the filesystem at `/mnt/efs`.

---

### SQS (`modules/sqs/`)

Sets up IAM permissions for the runner to create and manage SQS queues. SQS queues are created dynamically by the benchmark itself -- this module only grants the necessary permissions.

**Resources created:**

- IAM policy with SQS permissions (scoped to `prefix*` queue names)
- Policy attachment to the runner IAM role

**Variables:**

| Variable | Type | Default | Description |
|----------|------|---------|-------------|
| `benchmark_runner_role_id` | string | (required) | IAM role ID of the runner |

**Config output:** `benchmark_configs/sqs.json`

```json
{
  "Component": "SQS",
  "SQS:ServiceURL": "https://sqs.us-east-1.amazonaws.com",
  "SQS:Prefix": "prefix"
}
```

---

### AmazonMQ (`modules/amazonmq/`)

Deploys an AWS-managed message broker via AmazonMQ. This module supports **both RabbitMQ and ActiveMQ** -- the engine type is controlled by the `engine_type` variable.

**Resources created:**

- AmazonMQ broker (single-instance deployment)
- Security group with AMQP (5671) and management console (8162) ports
- Security group rules for runner-to-broker communication
- Random credentials (if not overridden)
- ActiveMQ XML configuration (ActiveMQ engine only)

**Variables:**

| Variable | Type | Default | Description |
|----------|------|---------|-------------|
| `engine_type` | string | `RabbitMQ` | `RabbitMQ` or `ActiveMQ` |
| `engine_version` | string | `3.13` | Engine version |
| `host_instance_type` | string | `mq.m5.xlarge` | Broker instance type |
| `mq_username_override` | string | (random) | Override the auto-generated username |
| `mq_password_override` | string | (random) | Override the auto-generated password |
| `benchmark_runner_sg_id` | string | (required) | Runner's security group ID |
| `network_config` | object | (required) | `{ vpc_id, subnet_id }` |

**Config output:** `benchmark_configs/rabbitmq-amq.json` or `benchmark_configs/activemq.json`

```json
{
  "Component": "RabbitMQ",
  "Amqp:Host": "b-xxxx.mq.us-east-1.amazonaws.com",
  "Amqp:Port": 5671,
  "Amqp:Scheme": "AMQPS",
  "Amqp:User": "rabbitmqbench",
  "Amqp:Password": "rabbitmqbench",
  "Amqp:MaxRetries": 1,
  "ResourceMetadata": { ... }
}
```

In `main.tf`, this module is instantiated twice (once for RabbitMQ, once for ActiveMQ):

```hcl
module "rabbitmq_amq" {
  source      = "./modules/amazonmq"
  engine_type = "RabbitMQ"       # defaults
  # ...
}

module "activemq" {
  source         = "./modules/amazonmq"
  engine_type    = "ActiveMQ"
  engine_version = "5.18"
  # ...
}
```

---

### RabbitMQ EC2 (`modules/rabbitmq/ec2/`)

Deploys RabbitMQ on a standalone EC2 instance. This is an alternative to the AmazonMQ-managed approach, useful when you need more control over the RabbitMQ configuration or want to test unmanaged deployments.

**Resources created:**

- EC2 instance (Ubuntu 22.04) with RabbitMQ 4.x installed via user data script
- Security group (AMQP 5672, management UI 15672)
- Random credentials (if not overridden)
- Optional CloudWatch log group and SSM parameter for log shipping

**Variables:**

| Variable | Type | Default | Description |
|----------|------|---------|-------------|
| `instance_type` | string | `m5.4xlarge` | EC2 instance type |
| `rabbitmq_username` | string | (random) | Override the auto-generated username |
| `rabbitmq_password` | string | (random) | Override the auto-generated password |
| `enable_cloudwatch_logs` | bool | `false` | Enable CloudWatch log shipping |
| `network_config` | object | (required) | `{ vpc_id, subnet_id }` |

**Config output:** `benchmark_configs/rabbitmq-ec2.json`

```json
{
  "Component": "RabbitMQ",
  "Amqp:Host": "10.0.1.xx",
  "Amqp:Port": 5672,
  "Amqp:Scheme": "AMQP",
  "Amqp:User": "xxxx",
  "Amqp:Password": "xxxx",
  "Amqp:MaxRetries": 1,
  "ResourceMetadata": { ... },
  "Management": {
    "Host": "ec2-xx-xx-xx-xx.compute-1.amazonaws.com",
    "IP": "xx.xx.xx.xx",
    "Port": 15672
  }
}
```

## Shared Resources

### VPC

Created in `main.tf` using the `terraform-aws-modules/vpc/aws` community module:

- CIDR: `10.0.0.0/16`
- Two public subnets (`10.0.1.0/24`, `10.0.2.0/24`)
- DNS hostnames and support enabled

### SSH Key Pair

Generated by Terraform using the `tls_private_key` resource (RSA 2048-bit). The private key is written to `infrastructure/generated/benchmark_key.pem` and also available as a Terraform output.

### Common Tags

All resources are tagged with:

```hcl
{
  application          = "Microbenchmarks"
  "deployment version" = "${prefix}-armonik-microbench"
}
```

Plus per-module tags identifying the module name.

## Terraform State

For CI/CD runs, the Terraform state is stored remotely in an S3 bucket (`armonik-microbench-backend-tfstate`) with a run-specific key:

```
s3://armonik-microbench-backend-tfstate/microbench/<run-id>/terraform.tfstate
```

For local development, state defaults to the local filesystem (`terraform.tfstate`).

## Config File Flow

```
terraform apply
    │
    ├── modules/runner/outputs.tf   → benchmark_configs/runners/benchmark_runner.json
    ├── modules/redis/outputs.tf    → benchmark_configs/redis.json
    ├── modules/s3/outputs.tf       → benchmark_configs/s3.json
    ├── modules/sqs/outputs.tf      → benchmark_configs/sqs.json
    ├── modules/amazonmq/outputs.tf → benchmark_configs/rabbitmq-amq.json (or activemq.json)
    ├── modules/rabbitmq/ec2/...    → benchmark_configs/rabbitmq-ec2.json
    ├── modules/localstorage/localfs → benchmark_configs/localstorage.json
    └── modules/localstorage/efs/... → benchmark_configs/efs.json
         │
         v
    microbenchmark.py study run --directory ./infrastructure/benchmark_configs
         │
         v
    BenchmoniK reads "Component" field → dispatches to correct benchmark class
```

The `benchmark_configs/` directory is gitignored since it contains deployment-specific values (endpoints, credentials).
