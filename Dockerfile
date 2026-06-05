FROM mcr.microsoft.com/dotnet/sdk:10.0 AS builder

RUN apt-get update \
    && apt-get install -y --no-install-recommends clang zlib1g-dev \
    && rm -rf /var/lib/apt/lists/*

WORKDIR /workspace

COPY install.sh /workspace/install.sh
COPY scripts /workspace/scripts

ARG COMPILE=false

RUN mkdir -p /out \
    && if [ "$COMPILE" = "true" ]; then \
        bash /workspace/install.sh --compile /out; \
    else \
        bash /workspace/install.sh /out; \
    fi

FROM scratch AS artifacts

COPY --from=builder /out/ /
