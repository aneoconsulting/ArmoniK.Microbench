#!/bin/bash
set -e

# - AWS_PROFILE: AWS profile to use (required)
# - S3_CONFIG_PATH: S3 path to download config from (optional)
# - S3_RESULT_PATH: S3 path to upload results to (required)
# - BENCHMARK_ARGS: Arguments to pass to the benchmark runner (defaults to --all)
# - RUN_ID: Unique identifier for this run (defaults to timestamp)

# Set default RUN_ID if not provided
if [ -z "$RUN_ID" ]; then
  export RUN_ID=$(date +%Y%m%d%H%M%S)
fi

echo "Starting benchmark run $RUN_ID"

# Create results directory
mkdir -p /app/results/$RUN_ID

if [ -n "$S3_CONFIG_PATH" ]; then
  echo "Downloading config from $S3_CONFIG_PATH"
  aws s3 cp $S3_CONFIG_PATH /app/benchmark_configs/ --recursive
fi

export BENCHMARK_CONFIG_DIR=/app/benchmark_configs
export ARMONIK_CORE_PATH=/app/ArmoniK.Core

# Run benchmarks
cd /app/BenchmoniK
echo "Running benchmarks with args: $BENCHMARK_ARGS"
dotnet run -c Release --project ./BenchmoniK/BenchmoniK.csproj -- $BENCHMARK_ARGS

# Upload results to S3
if [ -n "$S3_RESULT_PATH" ]; then
  echo "Uploading results to $S3_RESULT_PATH/$RUN_ID"
  aws s3 cp /app/BenchmoniK/BenchmarkDotNet.Artifacts/* $S3_RESULT_PATH/$RUN_ID --recursive
fi

echo "Benchmark run $RUN_ID completed"