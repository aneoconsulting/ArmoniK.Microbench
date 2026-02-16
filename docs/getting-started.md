# Getting Started

## Prerequisites

- **Python 3.12+** and [uv](https://docs.astral.sh/uv/) (the CLI is a single-file script with inline deps, run via `uv run`)
- **Terraform >= 1.0** (tested with 1.x)
- **AWS CLI** configured with credentials that can create EC2, ElastiCache, S3, SQS, AmazonMQ, VPC, and IAM resources
- **.NET 10 SDK** (only needed if building the benchmark runner locally; the runner EC2 instance installs it automatically)
- **An AWS account** with sufficient permissions and service quotas for the instance types you plan to use

## Project Structure

The ArmoniK Microbenchmark project is split into 4 main components:

<div class="grid cards" markdown>

-   __Microbenchmark CLI__

    ---

    The main tool you interact with. `microbenchmark.py` is a Python CLI (powered by [Click](https://click.palletsprojects.com/) and [Fabric](https://www.fabfile.org/)) that orchestrates everything: creating studies, SSHing into the runner instance, executing benchmarks, and syncing results from S3.

    [:octicons-terminal-16: CLI Reference](study.md)

-   __Infrastructure__

    ---

    Modular Terraform code that deploys an EC2 benchmark runner and the AWS-managed services (ElastiCache, S3, SQS, AmazonMQ) needed for each benchmark. You choose which modules to enable via a `parameters.tfvars` file.

    [:octicons-server-16: Infrastructure Reference](components/infrastructure.md)

-   __Benchmark Runner (BenchmoniK)__

    ---

    A .NET 10 CLI tool that takes a benchmark configuration JSON file and uses [BenchmarkDotNet](https://benchmarkdotnet.org/) to execute the corresponding microbenchmark against the real ArmoniK adapter code from [ArmoniK.Core](https://github.com/aneoconsulting/ArmoniK.Core).

-   __Visualization Tool__

    ---

    A Streamlit-based analysis tool that takes study results and generates interactive visualizations for comparing adapter performance. (Work in progress)

</div>

## Setup

### 1. Clone the repository

```bash
git clone --recurse-submodules https://github.com/aneoconsulting/ArmoniK.Microbench.git
cd ArmoniK.Microbench
```

The `--recurse-submodules` flag is important -- it pulls in ArmoniK.Core which is referenced as a submodule and is needed for building the benchmark runner.

### 2. Configure AWS credentials

Make sure your AWS credentials are configured. The project supports both named profiles and environment variables:

```bash
# Option A: Named profile
aws configure --profile my-profile

# Option B: Environment variables
export AWS_ACCESS_KEY_ID=...
export AWS_SECRET_ACCESS_KEY=...
export AWS_DEFAULT_REGION=us-east-1
```

### 3. Create a parameters file

Create `infrastructure/parameters.tfvars` to specify which benchmarks to deploy. Set a variable to `{}` (or an object with overrides) to enable it, or leave it out / set it to `null` to skip it.

```hcl
# Required
prefix = "my-bench"

# Runner configuration (always deployed)
benchmark_runner = {
  instance_type = "c7a.8xlarge"
}

# Enable the benchmarks you want (set to null or omit to skip)
redis_benchmark = {
  instance_type = "cache.m5.xlarge"
}

s3_benchmark = {}

localstorage_benchmark = {
  fs_path = "/tmp/localstorage_benchtemp"
}

sqs_benchmark = {}

# RabbitMQ via AmazonMQ managed service
rabbitmq_amq_benchmark = {
  instance_type     = "mq.m5.4xlarge"
  username_override = "rabbitmqbench"
  password_override = "rabbitmqbench"
}

# RabbitMQ on a standalone EC2 instance
rabbitmq_ec2_benchmark = {
  instance_type = "m5.4xlarge"
}

# ActiveMQ via AmazonMQ managed service
activemq_benchmark = {
  instance_type     = "mq.m5.4xlarge"
  username_override = "activemqbench"
  password_override = "activemqbench"
}

# EFS (used as a network filesystem for the LocalStorage adapter)
efs_benchmark = {}
```

### 4. Deploy infrastructure

```bash
cd infrastructure
terraform init
terraform apply -var-file="parameters.tfvars"
```

Terraform will:

- Create a VPC with public subnets
- Launch an EC2 runner instance (with .NET SDK, AWS CLI pre-installed)
- Deploy the enabled services (Redis, S3, SQS, etc.)
- Generate SSH keys and benchmark config JSON files under `infrastructure/benchmark_configs/`

!!! note
    Deployment can take 5-15 minutes depending on which modules you enable. AmazonMQ brokers in particular take several minutes to provision.

### 5. Run your first benchmark

Once infrastructure is deployed, you can either use the **study workflow** (recommended) or the lower-level **runner commands**.

#### Option A: Study workflow (recommended)

The study workflow handles init, build, benchmark execution, and result upload in one command:

```bash
# Create a study
uv run microbenchmark.py study create "my-first-study" \
  --core-version "0.25.1"

# Run benchmarks (this SSHs into the runner, clones the repo, builds Core, and runs benchmarks)
uv run microbenchmark.py study run "my-first-study" \
  --directory "./infrastructure/benchmark_configs" \
  --s3-bucket "armonik-microbench-results" \
  --profile "default"

# Download results locally
uv run microbenchmark.py study sync "my-first-study" \
  --output-dir "./results"
```

See [Studies](study.md) for the full reference.

#### Option B: Runner commands (manual step-by-step)

```bash
# Initialize the runner (clone repo, restore dependencies)
uv run microbenchmark.py runner init

# Build ArmoniK.Core on the runner
uv run microbenchmark.py runner build-core --repo-branch "0.25.1"

# Run a single benchmark
uv run microbenchmark.py runner bench --config-file "./infrastructure/benchmark_configs/redis.json"

# Retrieve results
uv run microbenchmark.py runner retrieve-results \
  --s3-bucket "armonik-microbench-results"
```

### 6. Tear down infrastructure

When you're done benchmarking, destroy the infrastructure to avoid ongoing costs:

```bash
cd infrastructure
terraform destroy -var-file="parameters.tfvars"
```

!!! warning
    Always destroy infrastructure when you're done. The EC2 runner and managed services (especially ElastiCache and AmazonMQ) incur costs while running.

## CI/CD (GitHub Actions)

The repository includes a GitHub Actions workflow (`.github/workflows/run-microbenchmarks.yml`) that automates the full lifecycle:

1. Checkout code and ArmoniK.Core at the specified release tag
2. Deploy infrastructure via Terraform (state stored in S3)
3. Create a study, run benchmarks, upload results
4. Destroy infrastructure (always, even on failure)

The workflow is triggered by:

- **Release publication** on ArmoniK.Core
- **Manual dispatch** with a release tag input
- **Repository dispatch** webhook from external systems

Results are uploaded as GitHub Actions artifacts and stored in S3.

## Serving the docs locally

```bash
uv run --group docs microbenchmark.py dev serve-docs
```

This starts a local MkDocs server at `http://127.0.0.1:8000`.
