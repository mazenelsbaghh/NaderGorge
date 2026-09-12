# syntax=docker/dockerfile:1
# ─── Stage 1: Build & Publish ────────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/sdk:9.0.201 AS builder

WORKDIR /src

# Restore dependencies as a separate layer (cache-friendly)
COPY NaderGorge.sln ./
COPY src/NaderGorge.API/NaderGorge.API.csproj                       src/NaderGorge.API/
COPY src/NaderGorge.Application/NaderGorge.Application.csproj       src/NaderGorge.Application/
COPY src/NaderGorge.Domain/NaderGorge.Domain.csproj                 src/NaderGorge.Domain/
COPY src/NaderGorge.Infrastructure/NaderGorge.Infrastructure.csproj src/NaderGorge.Infrastructure/
COPY src/NaderGorge.Migrator/NaderGorge.Migrator.csproj src/NaderGorge.Migrator/
RUN --mount=type=cache,id=massar-nuget,target=/root/.nuget/packages,sharing=locked \
    dotnet restore src/NaderGorge.API/NaderGorge.API.csproj \
    && dotnet restore src/NaderGorge.Migrator/NaderGorge.Migrator.csproj

# Copy source and publish release build
COPY . .
RUN --mount=type=cache,id=massar-nuget,target=/root/.nuget/packages,sharing=locked \
    dotnet publish src/NaderGorge.API/NaderGorge.API.csproj \
    -c Release --no-restore -o /app/publish \
    && dotnet publish src/NaderGorge.Migrator/NaderGorge.Migrator.csproj \
    -c Release --no-restore -o /app/migrator

# Both runtime targets consume the same compiled stage and shared project outputs.
FROM mcr.microsoft.com/dotnet/aspnet:9.0.6 AS migrator
WORKDIR /app
COPY --from=builder /app/migrator ./
USER 65532:65532
ENTRYPOINT ["dotnet", "NaderGorge.Migrator.dll"]

# ─── Stage 2: Runtime ────────────────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/aspnet:9.0.2 AS backend

WORKDIR /app

# Install runtime health-check, media-conversion, and timezone dependencies.
RUN DEBIAN_FRONTEND=noninteractive apt-get update \
    && DEBIAN_FRONTEND=noninteractive apt-get install -y --no-install-recommends curl ffmpeg tzdata \
    && rm -rf /var/lib/apt/lists/*

# Copy only the published output (no SDK, no source code)
COPY --from=builder /app/publish .

ENV TZ=Africa/Cairo

EXPOSE 5245

ENTRYPOINT ["dotnet", "NaderGorge.API.dll"]
