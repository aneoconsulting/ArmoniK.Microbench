# /// script
# requires-python = ">=3.12"
# dependencies = [
#     "boto3",
#     "click",
#     "fabric",
#     "rich-click",
# ]
#
# [dependency-groups]
# docs = [
#     "mkdocs-material",
# ]
# ///

# TODO: Lock the version in study file if "latest"

from datetime import datetime
import json
import os
from pathlib import Path
import subprocess
import rich_click as click
import boto3
from fabric import Connection

host_cmdlinearg = click.option('--host', type=str, help='Host of the benchmark machine.')
key_cmdlinearg = click.option('--key', type=str, help='PEM Key file path.')

@click.group()
def cli():
    pass

@cli.group()
def study():
    """Manage your Microbenchmarking studies."""
    pass 

@cli.group()
def runner():
    """Commands for micro-managing (initializing, running a bench and downloading the results) a specific benchmark runner"""
    pass 

@cli.group()
def dev():
    """Common development commands for this project."""
    pass


def get_studies_dir():
    """Get or create the studies directory"""
    studies_dir = Path("./studies")
    studies_dir.mkdir(exist_ok=True)
    return studies_dir

def load_study(study_name: str):
    """Load a study JSON file"""
    studies_dir = get_studies_dir()
    study_file = studies_dir / f"{study_name}.json"
    
    if not study_file.exists():
        raise click.ClickException(f"Study '{study_name}' not found at {study_file}")
    
    with open(study_file, 'r', encoding='utf-8') as f:
        return json.load(f)

def save_study(study_name: str, study_data: dict):
    """Save a study JSON file"""
    studies_dir = get_studies_dir()
    study_file = studies_dir / f"{study_name}.json"
    
    with open(study_file, 'w', encoding='utf-8') as f:
        json.dump(study_data, f, indent=4, ensure_ascii=False)

def get_runner_config(filepath: str):
    with open(filepath, encoding="UTF-8") as benchrunner_config_filehandle:
        benchrunner_config = json.load(benchrunner_config_filehandle)
        return benchrunner_config["host"], benchrunner_config["key"]

@runner.command(name='init')
@host_cmdlinearg
@key_cmdlinearg
@click.option('--repo-url', type=str,
              help='URL of the Git repository to clone.',
              default="https://github.com/aneoconsulting/ArmoniK.Microbench.git")
@click.option('--repo-branch', type=str, default='main',
              help='Branch of the repository to checkout. Defaults to main.')
def init_remote(host, key, repo_url, repo_branch):
    """Initialize the benchmark instance with the microbenchmark runner."""
    if not host or not key: 
        print("Runner hostname and key were not supplied, looking in the config dir:")
        host, key = get_runner_config("./infrastructure/benchmark_configs/runners/benchmark_runner.json")

    with Connection(
        host=host,
        user="ubuntu",
        connect_kwargs={
            "key_filename": key,
        },
    ) as c:
        # Remove existing benchmonik directory if it exists
        c.run("cd /home/ubuntu && rm -rf ArmoniK.Microbench")
        
        c.run(f"cd /home/ubuntu && "
              f"git clone --recurse-submodules {repo_url} && "
              f"cd ArmoniK.Microbench && "
              f"git checkout {repo_branch} && "
            #   f"git clone https://github.com/aneoconsulting/ArmoniK.Core &&" # Submodules aren't working for some godforsaken reason 
              f"cd benchmark_runner && "
              f"dotnet restore ./BenchmoniK.sln")


@runner.command(name='build-core')
@host_cmdlinearg
@key_cmdlinearg
@click.option('--repo-branch', type=str, default='main',
              help='Branch of ArmoniK.Core to checkout. Defaults to main.')
def build_core(host, key, repo_branch):
    """Build a specific ArmoniK.Core tag in the remote instance"""
    if not host or not key: 
        print("Runner hostname and key were not supplied, looking in the config dir:")
        host, key = get_runner_config("./infrastructure/benchmark_configs/runners/benchmark_runner.json")

    with Connection(
        host=host,
        user="ubuntu",
        connect_kwargs={
            "key_filename": key,
        },
    ) as c:
        
        c.run(f"cd /home/ubuntu/ArmoniK.Microbench/ArmoniK.Core && "
              f"git checkout {repo_branch} && "
              f"dotnet restore ArmoniK.Core.sln && "
              f"dotnet build -c Release")

@runner.command()
@host_cmdlinearg
@key_cmdlinearg
@click.option('--config-file', '-c', type=click.Path(exists=True, file_okay=True, dir_okay=False),
              help='Path to a config file to use for benchmarking.')
@click.option('--config-dir', '-d', type=click.Path(exists=True, file_okay=False, dir_okay=True),
              help='Path to a directory of config files to use for benchmarking.')
def bench(host, key, config_file, config_dir):
    """Run a microbenchmark or set of microbenchmarks in the benchmark instance"""
    if not host or not key: 
        print("Runner hostname and key were not supplied, looking in the config dir:")
        host, key = get_runner_config("./infrastructure/benchmark_configs/runners/benchmark_runner.json")

    # Ensure at least one of config_file or config_dir is provided
    if not config_file and not config_dir:
        raise click.UsageError("Either --config-file or --config-dir must be provided.")
    
    with Connection(
        host=host,
        user="ubuntu",
        connect_kwargs={
            "key_filename": key,
        },
    ) as c:
        # Process single config file
        if config_file:
            # Upload the config file to the remote machine
            remote_config_path = f"/tmp/{os.path.basename(config_file)}"
            c.put(config_file, remote_config_path)
            
            # Run the benchmark with the config file
            c.run(f"cd /home/ubuntu/ArmoniK.Microbench/benchmark_runner && "
                 f"dotnet run -c RELEASE --project ./BenchmoniK/BenchmoniK.csproj -- "
                 f"-c {remote_config_path} --armonik-core /home/ubuntu/ArmoniK.Microbench/ArmoniK.Core/")
        
        # Process directory of config files
        if config_dir:
            config_files = [os.path.join(config_dir, f) for f in os.listdir(config_dir) 
                           if os.path.isfile(os.path.join(config_dir, f))]
            # TODO: Proper error handling
            for cf in config_files:
                # Upload the config file to the remote machine
                remote_config_path = f"/tmp/{os.path.basename(cf)}"
                c.put(cf, remote_config_path)
                
                # Run the benchmark with the config file
                c.run(f"cd /home/ubuntu/ArmoniK.Microbench/benchmark_runner && "
                     f"dotnet run -c RELEASE --project ./BenchmoniK/BenchmoniK.csproj -- "
                     f"-c {remote_config_path} --armonik-core /home/ubuntu/ArmoniK.Microbench/ArmoniK.Core/")


@runner.command(name='retrieve-results')
@host_cmdlinearg
@key_cmdlinearg
@click.option('--s3-bucket', type=str, default="armonik-microbench-results", help='S3 bucket to upload results to.')
@click.option('--s3-key', type=str, default='benchmark-artifacts.zip', help='S3 key for the uploaded file.')
@click.option('--profile', type=str, envvar='AWS_PROFILE', default='default', help='AWS Profile.')
@click.option('--output-dir', type=str, default='.', help='Local directory to download results to.')
def retrieve_results(host, key, s3_bucket, s3_key, profile, output_dir):
    """Retrieve the microbenchmark results from the benchmark runner"""
    if not host or not key: 
        print("Runner hostname and key were not supplied, looking in the config dir:")
        host, key = get_runner_config("./infrastructure/benchmark_configs/runners/benchmark_runner.json")

    with Connection(
        host=host,
        user="ubuntu",
        connect_kwargs={
            "key_filename": key,
        },
    ) as c:
        # Zip the artifacts directory
        remote_zip_path = "/tmp/benchmark-artifacts.zip"
        c.run(f"cd /home/ubuntu/ArmoniK.Microbench/benchmark_runner && zip -r {remote_zip_path} BenchmarkDotNet.Artifacts")
        
        c.run(f"aws s3 cp {remote_zip_path} s3://{s3_bucket}/{s3_key}")
        
        # Ensure output directory exists
        os.makedirs(output_dir, exist_ok=True)
        
        # Download from S3 to local machine
        session = boto3.Session(profile_name=profile)
        s3 = session.client('s3')
        local_zip_path = os.path.join(output_dir, os.path.basename(s3_key))
        s3.download_file(s3_bucket, s3_key, local_zip_path)
        
        click.echo(f"Results uploaded to S3 bucket {s3_bucket}/{s3_key} and downloaded to {local_zip_path}")


@dev.command("serve-docs")
@click.option(
    "--port",
    "-p",
    default=8000,
    show_default=True,
    help="Port to serve the documentation on.",
    type=int,
)
def serve_docs(port: int):
    """
    Serves the MkDocs documentation locally.
    Requires MkDocs to be installed.
    """
    click.secho(f"Serving MkDocs documentation on http://127.0.0.1:{port}...", fg="cyan")
    command = ["mkdocs", "serve", "--dev-addr", f"127.0.0.1:{port}"]
    try:
        click.secho(f"Running command: {' '.join(command)}", fg="yellow")
        subprocess.run(command, check=True)
    except subprocess.CalledProcessError as e:
        click.secho(f"Error serving documentation: {e}", fg="red")
        raise click.Abort()
    except FileNotFoundError:
        click.secho("Error: mkdocs command not found. Is MkDocs installed and in your PATH?", fg="red")
        raise click.Abort()


@dev.command("publish-docs")
@click.option(
    "--message",
    "-m",
    help="Commit message for publishing the documentation.",
)
@click.option(
    "--force",
    is_flag=True,
    help="Force push the documentation. Use with caution.",
)
def publish_docs(message: str | None, force: bool):
    """
    Builds and deploys the MkDocs documentation, typically to GitHub Pages.
    This command uses `mkdocs gh-deploy`.
    """
    click.secho("Publishing MkDocs documentation...", fg="cyan")
    command = ["mkdocs", "gh-deploy"]
    if message:
        command.extend(["--message", message])
    if force:
        command.append("--force")

    try:
        click.secho(f"Running command: {' '.join(command)}", fg="yellow")
        subprocess.run(command, check=True)
        click.secho("Documentation published successfully.", fg="green")
    except subprocess.CalledProcessError as e:
        click.secho(f"Error publishing documentation: {e}", fg="red")
        raise click.Abort()
    except FileNotFoundError:
        click.secho("Error: mkdocs command not found. Is MkDocs installed and in your PATH?", fg="red")
        raise click.Abort()


@study.command("create")
@click.argument('study_name')
@click.option('--core-version', default='latest', help='Version of ArmoniK.Core to use')
@click.option('--runner-version', default='latest', help='Version of benchmark runner to use')
@click.option('--key-path', default='./infrastructure/generated/benchmark_key.pem', 
              help='Path to shared private key file')
def create_study(study_name: str, core_version: str, runner_version: str, key_path: str):
    """Create a new microbenchmarking study"""
    studies_dir = get_studies_dir()
    study_file = studies_dir / f"{study_name}.json"
    
    if study_file.exists():
        raise click.ClickException(f"Study '{study_name}' already exists at {study_file}")
    
    # Create study structure
    study_data = {
        "name": study_name,
        "core_version": core_version,
        "benchmark_runner_version": runner_version,
        "creation_date": datetime.now().isoformat(),
        "shared_private_key_path": key_path,
        "runs": [],
        "additional_notes": ""
    }
    
    save_study(study_name, study_data)
    click.echo(f"Created study '{study_name}' at {study_file}")



@study.command("run")
@click.argument('study_name')
@click.option('--runner', 'runner_config', 
              default='./infrastructure/benchmark_configs/runners/benchmark_runner.json',
              help='Path to runner config file')
@click.option('-c','--config', 'config_files', multiple=True, 
              type=click.Path(exists=True, file_okay=True, dir_okay=False),
              help='Individual benchmark config files to run')
@click.option('--directory', 'config_dir', 
              type=click.Path(exists=True, file_okay=False, dir_okay=True),
              help='Directory containing benchmark config files')
@click.option('--s3-bucket', default='armonik-microbench-results',
              help='S3 bucket to store results')
@click.option('--profile', envvar='AWS_PROFILE', default='default',
              help='AWS profile to use')
@click.option('--repo-url', type=str,
              help='URL of the Git repository to clone.',
              default="https://github.com/aneoconsulting/ArmoniK.Microbench.git")
@click.option('--repo-branch', type=str, default='main',
              help='Branch of the repository to checkout. Defaults to main.')
@click.option('--skip-init', is_flag=True, 
              help='Skip initialization step (assume environment is already set up)')
@click.option('--skip-build', is_flag=True,
              help='Skip core build step (assume core is already built)')
def run_study(study_name: str, runner_config: str, config_files: tuple, config_dir: str, 
              s3_bucket: str, profile: str, skip_init: bool, skip_build: bool, repo_url: str, repo_branch: str):
    """Run benchmarks for a study"""
    # Load existing study
    study_data = load_study(study_name)
    
    # Validate inputs
    if not config_files and not config_dir:
        raise click.UsageError("Either --config (-c) or --directory must be provided.")
    
    # Get runner connection details
    host, key = get_runner_config(runner_config)
    
    # Load runner config contents
    with open(runner_config, 'r', encoding='utf-8') as f:
        runner_config_contents = json.load(f)
    
    # Collect all config files to run
    benchmark_configs = []
    if config_files:
        benchmark_configs.extend(config_files)
    
    if config_dir:
        config_dir_path = Path(config_dir)
        benchmark_configs.extend([
            str(f) for f in config_dir_path.iterdir() 
            if f.is_file() and f.suffix.lower() in ['.json', '.yaml', '.yml']
        ])
    
    if not benchmark_configs:
        raise click.ClickException("No benchmark configuration files found")
    
    # Create new run entry
    run_timestamp = datetime.now().isoformat()
    run_entry = {
        "runner_config": runner_config,
        "runner_config_contents": runner_config_contents,
        "date": run_timestamp,
        "benchmarks": {}
    }
    
    click.echo(f"Running {len(benchmark_configs)} benchmark(s) for study '{study_name}'")
    
    with Connection(
        host=host,
        user="ubuntu",
        connect_kwargs={"key_filename": key},
    ) as c:

        # Step 1: Initialize the benchmark environment (like runner init)
        if not skip_init:
            click.echo("Step 1/3: Initializing benchmark environment...")
            try:
                # Remove existing directory if it exists
                c.run("cd /home/ubuntu && rm -rf ArmoniK.Microbench", warn=True)
                
                # Clone and setup the repository
                c.run(f"cd /home/ubuntu && "
                      f"git clone --recurse-submodules {repo_url} && "
                      f"cd ArmoniK.Microbench && "
                      f"git checkout {repo_branch} && "
                      f"cd benchmark_runner && "
                      f"dotnet restore ./BenchmoniK.sln")
                
                click.echo("Environment initialization completed successfully")
            except Exception as e:
                raise click.ClickException(f"Failed to initialize environment: {e}")
        else:
            click.echo("Step 1/3: Skipping initialization (--skip-init)")
        
        # Step 2: Build ArmoniK.Core (like runner build-core)
        if not skip_build:
            click.echo(f"Step 2/3: Building ArmoniK.Core version '{study_data['core_version']}'...")
            try:
                core_version = study_data["core_version"]
                if core_version == "latest":
                    core_version = "main"
                
                c.run(f"cd /home/ubuntu/ArmoniK.Microbench/ArmoniK.Core && "
                      f"git checkout {core_version} && "
                      f"dotnet restore ArmoniK.Core.sln && "
                      f"dotnet build -c Release")
                
                click.echo("Core build completed successfully")
            except Exception as e:
                raise click.ClickException(f"Failed to build ArmoniK.Core: {e}")
        else:
            click.echo("Step 2/3: Skipping core build (--skip-build)")

        for config_file in benchmark_configs:
            config_path = Path(config_file)
            config_name = config_path.stem
            
            click.echo(f"Running benchmark: {config_name}")
            
            # Read config file contents
            with open(config_file, 'r', encoding='utf-8') as f:
                config_contents = f.read()
            
            # Upload config file to remote machine
            remote_config_path = f"/tmp/{config_path.name}"
            c.put(config_file, remote_config_path)
            
            # Generate unique result paths
            timestamp_str = datetime.now().strftime("%Y%m%d_%H%M%S")
            results_key = f"{study_name}/{run_timestamp.split('T')[0]}/{config_name}_{timestamp_str}_results.zip"
            logs_key = f"{study_name}/{run_timestamp.split('T')[0]}/{config_name}_{timestamp_str}_logs.txt"
            
            try:
                # Run the benchmark
                result = c.run(
                    f"cd /home/ubuntu/ArmoniK.Microbench/benchmark_runner && "
                    f"dotnet run -c RELEASE --project ./BenchmoniK/BenchmoniK.csproj -- "
                    f"-c {remote_config_path} --armonik-core /home/ubuntu/ArmoniK.Microbench/ArmoniK.Core/ "
                    f"2>&1 | tee /tmp/{config_name}_logs.txt",
                    warn=True
                )
                
                # Upload logs to S3
                c.run(f"aws s3 cp /tmp/{config_name}_logs.txt s3://{s3_bucket}/{logs_key}")
                
                # TODO: No more zipping, just send it as is with some nice clean relevant renaming.
                # Zip and upload results with unique naming
                remote_zip_path = f"/tmp/{config_name}_{timestamp_str}_results.zip"
                c.run(f"cd /home/ubuntu/ArmoniK.Microbench/benchmark_runner && "
                     f"zip -r {remote_zip_path} BenchmarkDotNet.Artifacts")
                
                c.run(f"aws s3 cp {remote_zip_path} s3://{s3_bucket}/{results_key}")
                
                # Clean up remote files
                c.run(f"rm -f {remote_config_path} {remote_zip_path} /tmp/{config_name}_logs.txt")
                c.run("cd /home/ubuntu/ArmoniK.Microbench/benchmark_runner && rm -rf BenchmarkDotNet.Artifacts")
                
                # Store benchmark info
                run_entry["benchmarks"][config_path.name] = {
                    "source": config_contents,
                    "results": f"s3://{s3_bucket}/{results_key}",
                    "logs": f"s3://{s3_bucket}/{logs_key}",
                    "status": "success" if result.return_code == 0 else "failed"
                }
                
                click.echo(f"Completed benchmark: {config_name}")
                
            except Exception as e:
                click.echo(f"Failed to run benchmark {config_name}: {e}")
                run_entry["benchmarks"][config_path.name] = {
                    "source": config_contents,
                    "results": "",
                    "logs": f"s3://{s3_bucket}/{logs_key}" if 'logs_key' in locals() else "",
                    "status": "failed",
                    "error": str(e)
                }
    
    # Add run to study and save
    study_data["runs"].append(run_entry)
    save_study(study_name, study_data)
    
    successful_runs = sum(1 for b in run_entry["benchmarks"].values() if b.get("status") == "success")
    total_runs = len(run_entry["benchmarks"])
    click.echo(f"Study run completed: {successful_runs}/{total_runs} benchmarks successful")


@study.command("sync")
@click.argument('study_name')
@click.option('--output-dir', default='./results', help='Local directory to download results to')
@click.option('--profile', envvar='AWS_PROFILE', default='default', help='AWS profile to use')
@click.option("--no-profile", "no_profile", is_flag=True, default=False)
@click.option('--run-index', type=int, help='Specific run index to sync (default: all runs)')
def sync_study(study_name: str, output_dir: str, profile: str, no_profile:bool, run_index: int):
    """Download study run results from S3"""
    study_data = load_study(study_name)
    
    if not study_data["runs"]:
        raise click.ClickException(f"No runs found for study '{study_name}'")
    
    # Determine which runs to sync
    runs_to_sync = []
    if run_index is not None:
        if run_index >= len(study_data["runs"]):
            raise click.ClickException(f"Run index {run_index} not found (max: {len(study_data['runs'])-1})")
        runs_to_sync = [study_data["runs"][run_index]]
    else:
        runs_to_sync = study_data["runs"]
    
    # Create output directory structure
    output_path = Path(output_dir) / study_name
    output_path.mkdir(parents=True, exist_ok=True)
    
    # Initialize S3 client
    if no_profile:
        session = boto3.Session()
    else:
        session = boto3.Session(profile_name=profile)
    s3 = session.client('s3')
    
    click.echo(f"Syncing {len(runs_to_sync)} run(s) for study '{study_name}'")
    
    for i, run in enumerate(runs_to_sync):
        run_dir = output_path / f"run_{i if run_index is None else run_index}_{run['date'].split('T')[0]}"
        run_dir.mkdir(exist_ok=True)
        
        click.echo(f"Syncing run {i if run_index is None else run_index} to {run_dir}")
        
        for benchmark_name, benchmark_data in run["benchmarks"].items():
            benchmark_dir = run_dir / Path(benchmark_name).stem
            benchmark_dir.mkdir(exist_ok=True)
            
            # Save benchmark source
            source_file = benchmark_dir / "config.json"
            with open(source_file, 'w', encoding='utf-8') as f:
                f.write(benchmark_data["source"])
            
            # Download results if available
            if benchmark_data.get("results") and benchmark_data["results"].startswith("s3://"):
                try:
                    s3_url = benchmark_data["results"]
                    bucket, key = s3_url.replace("s3://", "").split("/", 1)
                    results_file = benchmark_dir / "results.zip"
                    s3.download_file(bucket, key, str(results_file))
                    click.echo(f"Downloaded results for {benchmark_name}")
                except Exception as e:
                    click.echo(f"Failed to download results for {benchmark_name}: {e}")
            
            # Download logs if available
            if benchmark_data.get("logs") and benchmark_data["logs"].startswith("s3://"):
                try:
                    s3_url = benchmark_data["logs"]
                    bucket, key = s3_url.replace("s3://", "").split("/", 1)
                    logs_file = benchmark_dir / "logs.txt"
                    s3.download_file(bucket, key, str(logs_file))
                    click.echo(f"Downloaded logs for {benchmark_name}")
                except Exception as e:
                    click.echo(f"Failed to download logs for {benchmark_name}: {e}")
    
    click.echo(f"Sync completed. Results available in: {output_path}")

if __name__ == '__main__':
    cli()
