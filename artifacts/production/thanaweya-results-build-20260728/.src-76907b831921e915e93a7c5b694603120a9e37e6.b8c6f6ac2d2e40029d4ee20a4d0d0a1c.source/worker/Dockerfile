# ─── Stage 1: Install & Build ────────────────────────────────────────────────
FROM node:20.18.3-slim AS builder

WORKDIR /app
ENV NPM_CONFIG_AUDIT=false \
    NPM_CONFIG_FUND=false \
    NPM_CONFIG_LOGLEVEL=error \
    NPM_CONFIG_PROGRESS=false \
    NPM_CONFIG_UPDATE_NOTIFIER=false

# Install media and native-module build dependencies.
RUN DEBIAN_FRONTEND=noninteractive apt-get update \
    && DEBIAN_FRONTEND=noninteractive apt-get install -y --no-install-recommends ffmpeg python3 build-essential \
    && rm -rf /var/lib/apt/lists/*

# Install ALL dependencies (including devDependencies for TypeScript compile)
COPY package.json package-lock.json ./
RUN npm ci --no-audit --no-fund --loglevel=error

# Copy source and compile TypeScript → dist/
COPY . .
RUN npm run build

# ─── Stage 2: Runtime ────────────────────────────────────────────────────────
FROM node:20.18.3-slim AS runner

WORKDIR /app

ENV NODE_ENV=production
ENV NPM_CONFIG_AUDIT=false \
    NPM_CONFIG_FUND=false \
    NPM_CONFIG_LOGLEVEL=error \
    NPM_CONFIG_PROGRESS=false \
    NPM_CONFIG_UPDATE_NOTIFIER=false

# Install ffmpeg and python3 in the runtime image too, curl for healthchecks,
# and gosu so the entrypoint can prepare root-owned secrets before dropping privileges.
RUN DEBIAN_FRONTEND=noninteractive apt-get update \
    && DEBIAN_FRONTEND=noninteractive apt-get install -y --no-install-recommends ffmpeg python3 curl gosu \
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

RUN groupadd --system --gid 10001 worker \
    && useradd --system --uid 10001 --gid worker --home-dir /app --shell /usr/sbin/nologin worker \
    && mkdir -p /app/.tmp /app/.secrets \
    && chown -R worker:worker /app

COPY docker-entrypoint.sh /usr/local/bin/worker-entrypoint.sh
RUN chmod +x /usr/local/bin/worker-entrypoint.sh

EXPOSE 3001

ENTRYPOINT ["worker-entrypoint.sh"]
CMD ["node", "dist/index.js"]
