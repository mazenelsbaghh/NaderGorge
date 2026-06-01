# ─── Stage 1: Install & Build ────────────────────────────────────────────────
FROM node:20-slim AS builder

WORKDIR /app

# Install ffmpeg and python3 (required by fluent-ffmpeg and youtube-dl-exec)
RUN apt-get update && apt-get install -y --no-install-recommends ffmpeg python3 \
    && rm -rf /var/lib/apt/lists/*

# Install ALL dependencies (including devDependencies for TypeScript compile)
COPY package.json package-lock.json ./
RUN npm ci

# Copy source and compile TypeScript → dist/
COPY . .
RUN npm run build

# ─── Stage 2: Runtime ────────────────────────────────────────────────────────
FROM node:20-slim AS runner

WORKDIR /app

ENV NODE_ENV=production

# Install ffmpeg and python3 in the runtime image too, and curl for healthchecks
RUN apt-get update && apt-get install -y --no-install-recommends ffmpeg python3 curl \
    && rm -rf /var/lib/apt/lists/*

# Install only production dependencies
COPY package.json package-lock.json ./
RUN npm ci --omit=dev

# Copy compiled output from builder
COPY --from=builder /app/dist ./dist

EXPOSE 3001

CMD ["node", "dist/index.js"]
