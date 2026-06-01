# ─── Stage 1: Build & Publish ────────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS builder

WORKDIR /src

# Restore dependencies as a separate layer (cache-friendly)
COPY NaderGorge.sln ./
COPY src/NaderGorge.API/NaderGorge.API.csproj                       src/NaderGorge.API/
COPY src/NaderGorge.Application/NaderGorge.Application.csproj       src/NaderGorge.Application/
COPY src/NaderGorge.Domain/NaderGorge.Domain.csproj                 src/NaderGorge.Domain/
COPY src/NaderGorge.Infrastructure/NaderGorge.Infrastructure.csproj src/NaderGorge.Infrastructure/
RUN dotnet restore src/NaderGorge.API/NaderGorge.API.csproj

# Copy source and publish release build
COPY . .
RUN dotnet publish src/NaderGorge.API/NaderGorge.API.csproj \
    -c Release \
    -o /app/publish

# ─── Stage 2: Runtime ────────────────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS runner

WORKDIR /app

# Install curl for healthchecks
RUN apt-get update && apt-get install -y --no-install-recommends curl \
    && rm -rf /var/lib/apt/lists/*

# Copy only the published output (no SDK, no source code)
COPY --from=builder /app/publish .

EXPOSE 5245

ENTRYPOINT ["dotnet", "NaderGorge.API.dll"]
