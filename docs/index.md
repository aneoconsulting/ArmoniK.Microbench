# ArmoniK Microbenchmarks

ArmoniK.Microbench is a performance benchmarking framework for [ArmoniK](https://github.com/aneoconsulting/ArmoniK) components. It measures the throughput and latency of the various storage and messaging adapters that ArmoniK supports, using [BenchmarkDotNet](https://benchmarkdotnet.org/) under the hood.

## What does it benchmark?

The framework currently supports benchmarking the following ArmoniK adapters:

### Object Storage Adapters

| Adapter | Infrastructure | Operations Measured |
|---------|---------------|-------------------|
| **Redis** | AWS ElastiCache | Add, Get, Delete, GetSize |
| **S3** | AWS S3 | Add, Get, Delete, GetSize |
| **LocalStorage** | Local filesystem (or EFS) | Add, Get, Delete, GetSize |

### Queue Adapters

| Adapter | Infrastructure | Operations Measured |
|---------|---------------|-------------------|
| **SQS** | AWS SQS | Push, Pull+Ack, Pull+Nack, PushThenPull, PushThenPullPerRunner |
| **RabbitMQ** | AmazonMQ or EC2 | Push, Pull+Ack, Pull+Nack, PushThenPull, PushThenPullPerRunner |
| **ActiveMQ** | AmazonMQ | Push, Pull+Ack, Pull+Nack, PushThenPull, PushThenPullPerRunner |

All benchmarks run with configurable concurrency levels to simulate realistic multi-worker scenarios.

## How it works

The project follows a three-phase workflow:

```
1. Deploy        2. Benchmark       3. Collect
┌──────────┐    ┌──────────────┐    ┌──────────────┐
│ Terraform │───>│ BenchmoniK   │───>│ Results → S3 │
│ (infra)   │    │ (.NET runner)│    │ + study JSON │
└──────────┘    └──────────────┘    └──────────────┘
```

1. **Deploy infrastructure** -- Terraform provisions the AWS resources needed for the benchmarks you want to run (EC2 runner instance, Redis cluster, S3 bucket, SQS queues, etc.). Each module outputs a JSON config file consumed by the benchmark runner.

2. **Run benchmarks** -- The Microbenchmark CLI (or the CI workflow) SSHs into the runner instance, uploads the config files, and executes the BenchmoniK .NET tool. BenchmarkDotNet runs the benchmarks with statistical rigor (multiple iterations, warmup, memory diagnostics).

3. **Collect results** -- Benchmark artifacts (BenchmarkDotNet reports, logs) are uploaded to S3. The CLI's study system tracks metadata, config snapshots, and S3 result locations in a local JSON file.

## Project Components

| Component | Language | Description |
|-----------|----------|-------------|
| **Microbenchmark CLI** (`microbenchmark.py`) | Python | Orchestrates the full workflow: study management, SSH into runners, benchmark execution, result syncing |
| **BenchmoniK** (`benchmark_runner/`) | C# (.NET 10) | The actual benchmark runner. Takes a config file, instantiates the appropriate ArmoniK adapter, and runs BenchmarkDotNet benchmarks |
| **Infrastructure** (`infrastructure/`) | HCL (Terraform) | Modular Terraform code for deploying all AWS resources needed for benchmarking |
| **Visualization** (`microbench-analysis/`) | Python | Analysis and visualization tool for benchmark results (WIP) |

## Quick Links

- [Getting Started](getting-started.md) -- Prerequisites, setup, and running your first benchmark
- [Studies](study.md) -- How the study system works and CLI reference
- [Infrastructure](components/infrastructure.md) -- Terraform modules and configuration reference
