FROM mcr.microsoft.com/dotnet/sdk:8.0

RUN apt-get update && \
    apt-get install -y curl unzip && \
    curl "https://awscli.amazonaws.com/awscli-exe-linux-x86_64.zip" -o "awscliv2.zip" && \
    unzip awscliv2.zip && \
    ./aws/install && \
    rm -rf awscliv2.zip ./aws && \
    apt-get clean

WORKDIR /app

# Copy solution files and restore dependencies
COPY BenchmoniK/*.sln ./
COPY BenchmoniK/.gitignore ./
COPY BenchmoniK/.idea ./.idea
COPY BenchmoniK/BenchmoniK ./BenchmoniK
COPY ArmoniK.Core ./ArmoniK.Core

RUN dotnet restore ./BenchmoniK.sln
RUN dotnet build ./BenchmoniK/BenchmoniK.csproj -c Release

# Create benchmark config directory
WORKDIR /app/benchmark_configs
RUN mkdir -p /app/benchmark_configs

# Create entrypoint script
WORKDIR /app
COPY ./docker-entrypoint.sh /app/docker-entrypoint.sh
RUN chmod +x /app/docker-entrypoint.sh

ENTRYPOINT ["/app/docker-entrypoint.sh"]