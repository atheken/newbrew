FROM mcr.microsoft.com/devcontainers/dotnet:dev-7.0-jammy
RUN apt-get update
RUN apt-get install -y vim bash jq curl
SHELL ["/bin/bash"]