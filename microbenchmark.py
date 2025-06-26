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


import json
import os
import subprocess
import rich_click as click
import boto3
from fabric import Connection

@click.group()
def cli():
    pass

host_cmdlinearg = click.option('--host', type=str, help='Host of the benchmark machine.')
key_cmdlinearg = click.option('--key', type=str, help='PEM Key file path.')

def get_runner_config(filepath: str):
    with open(filepath, encoding="UTF-8") as benchrunner_config_filehandle:
        benchrunner_config = json.load(benchrunner_config_filehandle)
        return benchrunner_config["host"], benchrunner_config["key"]

@cli.command(name='init')
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


@cli.command(name='build-core')
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

@cli.command()
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


@cli.command(name='retrieve-results')
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


@cli.command("serve-docs")
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


@cli.command("publish-docs")
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

if __name__ == '__main__':
    cli()
