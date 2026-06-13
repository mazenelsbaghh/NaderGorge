# ─── Stage 1: Install & Build ────────────────────────────────────────────────
FROM node:20-slim AS builder

WORKDIR /app
ENV NPM_CONFIG_AUDIT=false \
    NPM_CONFIG_FUND=false \
    NPM_CONFIG_LOGLEVEL=error \
    NPM_CONFIG_PROGRESS=false \
    NPM_CONFIG_UPDATE_NOTIFIER=false

# Install ffmpeg and python3 (required by fluent-ffmpeg and youtube-dl-exec)
RUN DEBIAN_FRONTEND=noninteractive apt-get update \
    && DEBIAN_FRONTEND=noninteractive apt-get install -y --no-install-recommends ffmpeg python3 \
    && rm -rf /var/lib/apt/lists/*

# Install ALL dependencies (including devDependencies for TypeScript compile)
COPY package.json package-lock.json ./
RUN npm ci --no-audit --no-fund --loglevel=error

# Copy source and compile TypeScript → dist/
COPY . .
RUN npm run build

# ─── Stage 2: Runtime ────────────────────────────────────────────────────────
FROM node:20-slim AS runner

WORKDIR /app

ENV NODE_ENV=production
ENV NPM_CONFIG_AUDIT=false \
    NPM_CONFIG_FUND=false \
    NPM_CONFIG_LOGLEVEL=error \
    NPM_CONFIG_PROGRESS=false \
    NPM_CONFIG_UPDATE_NOTIFIER=false

# Install ffmpeg and python3 in the runtime image too, and curl for healthchecks
RUN DEBIAN_FRONTEND=noninteractive apt-get update \
    && DEBIAN_FRONTEND=noninteractive apt-get install -y --no-install-recommends ffmpeg python3 curl \
    && rm -rf /var/lib/apt/lists/*

# Install only production dependencies
COPY package.json package-lock.json ./
RUN DEBIAN_FRONTEND=noninteractive apt-get update \
    && DEBIAN_FRONTEND=noninteractive apt-get install -y --no-install-recommends build-essential \
    && npm ci --omit=dev --no-audit --no-fund --loglevel=error \
    && DEBIAN_FRONTEND=noninteractive apt-get purge -y --auto-remove build-essential \
    && rm -rf /var/lib/apt/lists/*

# Copy compiled output from builder
COPY --from=builder /app/dist ./dist

EXPOSE 3001

CMD ["node", "dist/index.js"]
