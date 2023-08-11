FROM mcr.microsoft.com/devcontainers/dotnet:dev-7.0-jammy
RUN apt-get update
RUN apt-get install -y vim bash jq curl
## determine if this container is x86 or arm:
RUN curl -qsL https://github.com/tailwindlabs/tailwindcss/releases/download/v3.3.3/tailwindcss-linux-$($([[ $(uname -a) =~ 'x86_64' ]] && echo -n 'x64') || echo -n 'arm64') > /usr/local/bin/tailwindcss
RUN chmod 755 /usr/local/bin/tailwindcss
RUN chown root:root /usr/local/bin/tailwindcss
SHELL ["/bin/bash"]