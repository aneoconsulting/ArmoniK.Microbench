# ArmoniK.Microbench

Performance benchmarking framework for [ArmoniK](https://github.com/aneoconsulting/ArmoniK) storage and messaging adapters. Uses [BenchmarkDotNet](https://benchmarkdotnet.org/) to measure throughput and latency of Redis, S3, LocalStorage, SQS, RabbitMQ, and ActiveMQ adapters under configurable concurrency.

## Supported Adapters

| Category | Adapter | AWS Service |
|----------|---------|-------------|
| Object Storage | Redis | ElastiCache |
| Object Storage | S3 | S3 |
| Object Storage | LocalStorage | Local FS / EFS |
| Queue | SQS | SQS |
| Queue | RabbitMQ | AmazonMQ / EC2 |
| Queue | ActiveMQ | AmazonMQ |

## Quick Start

**Prerequisites:** Python 3.12+, [uv](https://docs.astral.sh/uv/), Terraform >= 1.0, AWS credentials

```bash
# Clone with submodules (ArmoniK.Core is needed for building benchmarks)
git clone --recurse-submodules https://github.com/aneoconsulting/ArmoniK.Microbench.git
cd ArmoniK.Microbench

# Deploy infrastructure (edit parameters.tfvars to choose which adapters to benchmark)
cd infrastructure
terraform init && terraform apply -var-file="parameters.tfvars"
cd ..

# Create a study, run benchmarks, sync results
uv run microbenchmark.py study create "my-study" --core-version "0.25.1"
uv run microbenchmark.py study run "my-study" --directory "./infrastructure/benchmark_configs"
uv run microbenchmark.py study sync "my-study" --output-dir "./results"

# Tear down when done
cd infrastructure && terraform destroy -var-file="parameters.tfvars"
```

## Project Structure

```
.
├── microbenchmark.py              # Python CLI (orchestration, study management, SSH)
├── benchmark_runner/              # C# (.NET 10) benchmark runner using BenchmarkDotNet
│   └── BenchmoniK/
│       ├── Benchmarks/
│       │   ├── ObjectStorage/     # Redis, S3, LocalStorage benchmarks
│       │   └── Queue/             # SQS, RabbitMQ, ActiveMQ benchmarks
│       └── Program.cs             # Entry point (config dispatch)
├── infrastructure/                # Terraform modules
│   ├── main.tf                    # Root: VPC, runner, conditional module deployment
│   ├── variables.tf               # All benchmark toggle variables
│   └── modules/
│       ├── runner/                # EC2 benchmark host
│       ├── redis/                 # ElastiCache cluster
│       ├── s3/                    # S3 bucket + IAM
│       ├── sqs/                   # SQS IAM permissions
│       ├── amazonmq/              # AmazonMQ (RabbitMQ or ActiveMQ)
│       ├── rabbitmq/ec2/          # Self-hosted RabbitMQ on EC2
│       └── localstorage/          # localfs + EFS
├── docs/                          # MkDocs documentation
├── microbench-analysis/           # Result visualization tool (WIP)
└── ArmoniK.Core/                  # Git submodule
```

## Documentation

Full documentation is available at [aneoconsulting.github.io/ArmoniK.Microbench](https://aneoconsulting.github.io/ArmoniK.Microbench) or can be served locally:

```bash
uv run --group docs microbenchmark.py dev serve-docs
```

## Common Results Bucket

Shared benchmark results are stored in: `armonik-microbench-results`
