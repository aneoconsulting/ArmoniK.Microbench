# Studies

A **study** is the central organizational unit in ArmoniK.Microbench. It groups together everything related to a benchmarking session: which version of ArmoniK.Core was tested, the benchmark configurations used, where results are stored, and metadata about each run.

## Concept

Studies are stored as JSON files in the `studies/` directory (gitignored by default). Each study can contain multiple **runs** -- for example, you might run the same study against different infrastructure configurations or after a code change to compare results.

```
studies/
  release-0.25.1-20260215-143022.json
  redis-scaling-test.json
  queue-comparison.json
```

### Study JSON Structure

```json
{
    "name": "release-0.25.1-20260215-143022",
    "core_version": "0.25.1",
    "benchmark_runner_version": "latest",
    "creation_date": "2026-02-15T14:30:22.123456",
    "shared_private_key_path": "./infrastructure/generated/benchmark_key.pem",
    "runs": [
        {
            "runner_config": "./infrastructure/benchmark_configs/runners/benchmark_runner.json",
            "runner_config_contents": { "host": "...", "key": "..." },
            "date": "2026-02-15T14:35:00.000000",
            "benchmarks": {
                "redis.json": {
                    "source": "{ ... config contents ... }",
                    "results": "s3://armonik-microbench-results/release-0.25.1/.../results.zip",
                    "logs": "s3://armonik-microbench-results/release-0.25.1/.../logs.txt",
                    "status": "success"
                },
                "sqs.json": {
                    "source": "{ ... config contents ... }",
                    "results": "s3://armonik-microbench-results/release-0.25.1/.../results.zip",
                    "logs": "s3://armonik-microbench-results/release-0.25.1/.../logs.txt",
                    "status": "success"
                }
            }
        }
    ],
    "additional_notes": ""
}
```

Key fields:

- **core_version** -- The ArmoniK.Core tag/branch checked out on the runner for this study
- **runs** -- A list of run entries. Each run contains the runner config snapshot and a map of benchmark results
- **benchmarks[name].source** -- A snapshot of the benchmark config file contents at the time of the run (for reproducibility)
- **benchmarks[name].results** -- S3 URI pointing to the zipped BenchmarkDotNet artifacts
- **benchmarks[name].logs** -- S3 URI pointing to the full console output log

## CLI Reference

All study commands are under the `study` subcommand group:

```bash
uv run microbenchmark.py study <command> [options]
```

### `study create`

Create a new study.

```bash
uv run microbenchmark.py study create <STUDY_NAME> [OPTIONS]
```

| Option | Default | Description |
|--------|---------|-------------|
| `--core-version` | `latest` | ArmoniK.Core version/tag to benchmark against |
| `--runner-version` | `latest` | Benchmark runner version |
| `--key-path` | `./infrastructure/generated/benchmark_key.pem` | Path to the SSH private key |

**Example:**

```bash
uv run microbenchmark.py study create "redis-perf-test" --core-version "0.25.1"
```

### `study run`

Run benchmarks within a study. This is the main command that orchestrates the full benchmark lifecycle on the remote runner.

```bash
uv run microbenchmark.py study run <STUDY_NAME> [OPTIONS]
```

| Option | Default | Description |
|--------|---------|-------------|
| `--runner` | `./infrastructure/benchmark_configs/runners/benchmark_runner.json` | Path to the runner config file (contains host and key) |
| `-c`, `--config` | -- | Path to an individual benchmark config file. Can be specified multiple times |
| `--directory` | -- | Path to a directory of benchmark config files (`.json`, `.yaml`, `.yml`) |
| `--s3-bucket` | `armonik-microbench-results` | S3 bucket for storing results |
| `--profile` | `default` (or `$AWS_PROFILE`) | AWS profile to use |
| `--repo-url` | `https://github.com/aneoconsulting/ArmoniK.Microbench.git` | Repository URL to clone on the runner |
| `--repo-branch` | `main` | Branch to checkout |
| `--skip-init` | `false` | Skip the initialization step (clone + restore) |
| `--skip-build` | `false` | Skip the ArmoniK.Core build step |

!!! note
    You must provide at least one of `--config` or `--directory`.

**What `study run` does under the hood:**

1. **Init** (unless `--skip-init`): SSHs into the runner, clones the repo, and runs `dotnet restore`
2. **Build** (unless `--skip-build`): Checks out the ArmoniK.Core version from the study, runs `dotnet build -c Release`
3. **Benchmark**: For each config file, uploads it to the runner, executes BenchmoniK, uploads results and logs to S3
4. **Record**: Saves the run entry (configs, S3 URIs, status) into the study JSON

**Examples:**

```bash
# Run all configs in a directory
uv run microbenchmark.py study run "my-study" \
  --directory "./infrastructure/benchmark_configs"

# Run specific config files
uv run microbenchmark.py study run "my-study" \
  -c "./infrastructure/benchmark_configs/redis.json" \
  -c "./infrastructure/benchmark_configs/sqs.json"

# Re-run without re-initializing (useful for iterating)
uv run microbenchmark.py study run "my-study" \
  --skip-init --skip-build \
  -c "./infrastructure/benchmark_configs/redis.json"
```

### `study sync`

Download study results from S3 to a local directory.

```bash
uv run microbenchmark.py study sync <STUDY_NAME> [OPTIONS]
```

| Option | Default | Description |
|--------|---------|-------------|
| `--output-dir` | `./results` | Local directory to download results into |
| `--profile` | `default` (or `$AWS_PROFILE`) | AWS profile to use |
| `--no-profile` | `false` | Use default credential chain instead of a named profile |
| `--run-index` | all runs | Sync only a specific run by index |

**Output structure:**

```
results/
  my-study/
    run_0_2026-02-15/
      redis/
        config.json      # Snapshot of the benchmark config
        results.zip      # BenchmarkDotNet artifacts
        logs.txt         # Full console output
      sqs/
        config.json
        results.zip
        logs.txt
```

**Examples:**

```bash
# Sync all runs
uv run microbenchmark.py study sync "my-study"

# Sync only the latest run
uv run microbenchmark.py study sync "my-study" --run-index 0

# Use default AWS credential chain (e.g. on EC2 with instance profile)
uv run microbenchmark.py study sync "my-study" --no-profile
```

## Runner Commands

For lower-level control, you can use the `runner` commands directly. These operate on the remote runner instance without the study abstraction.

### `runner init`

Initialize the benchmark runner instance (clone repo, restore .NET dependencies).

```bash
uv run microbenchmark.py runner init [OPTIONS]
```

| Option | Default | Description |
|--------|---------|-------------|
| `--host` | (from runner config) | Hostname/IP of the runner |
| `--key` | (from runner config) | Path to PEM key file |
| `--repo-url` | `https://github.com/aneoconsulting/ArmoniK.Microbench.git` | Repository to clone |
| `--repo-branch` | `main` | Branch to checkout |

If `--host` and `--key` are not provided, the command reads them from `./infrastructure/benchmark_configs/runners/benchmark_runner.json` (generated by Terraform).

### `runner build-core`

Build a specific ArmoniK.Core version on the runner.

```bash
uv run microbenchmark.py runner build-core [OPTIONS]
```

| Option | Default | Description |
|--------|---------|-------------|
| `--host` | (from runner config) | Hostname/IP of the runner |
| `--key` | (from runner config) | Path to PEM key file |
| `--repo-branch` | `main` | ArmoniK.Core tag/branch to checkout and build |

### `runner bench`

Run a benchmark (or set of benchmarks) on the runner.

```bash
uv run microbenchmark.py runner bench [OPTIONS]
```

| Option | Default | Description |
|--------|---------|-------------|
| `--host` | (from runner config) | Hostname/IP of the runner |
| `--key` | (from runner config) | Path to PEM key file |
| `--config-file`, `-c` | -- | Path to a single benchmark config file |
| `--config-dir`, `-d` | -- | Path to a directory of config files |

### `runner retrieve-results`

Retrieve benchmark results from the runner, upload to S3, and download locally.

```bash
uv run microbenchmark.py runner retrieve-results [OPTIONS]
```

| Option | Default | Description |
|--------|---------|-------------|
| `--host` | (from runner config) | Hostname/IP of the runner |
| `--key` | (from runner config) | Path to PEM key file |
| `--s3-bucket` | `armonik-microbench-results` | S3 bucket for results |
| `--s3-key` | `benchmark-artifacts.zip` | S3 key for the uploaded archive |
| `--profile` | `default` (or `$AWS_PROFILE`) | AWS profile |
| `--output-dir` | `.` | Local directory to download results to |

## Dev Commands

Utility commands for development:

### `dev serve-docs`

Serve the MkDocs documentation locally.

```bash
uv run microbenchmark.py dev serve-docs [--port 8000]
```

### `dev publish-docs`

Build and deploy documentation to GitHub Pages.

```bash
uv run microbenchmark.py dev publish-docs [--message "Update docs"] [--force]
```
