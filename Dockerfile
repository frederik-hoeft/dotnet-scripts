FROM mcr.microsoft.com/dotnet/sdk:10.0 AS builder

RUN apt-get update \
    && apt-get install -y --no-install-recommends clang zlib1g-dev \
    && rm -rf /var/lib/apt/lists/*

WORKDIR /workspace

COPY install.sh /workspace/install.sh
COPY scripts /workspace/scripts

RUN mkdir -p /out \
    && bash /workspace/install.sh --compile /out

FROM scratch AS artifacts

COPY --from=builder /out/ /
