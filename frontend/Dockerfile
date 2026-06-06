# ─── Stage 1: Build ──────────────────────────────────────────────────────────
FROM node:20-alpine AS builder

WORKDIR /app
ENV NPM_CONFIG_AUDIT=false \
    NPM_CONFIG_FUND=false \
    NPM_CONFIG_LOGLEVEL=error \
    NPM_CONFIG_PROGRESS=false \
    NPM_CONFIG_UPDATE_NOTIFIER=false

# Install dependencies (separate layer for cache efficiency)
COPY package.json package-lock.json ./
RUN npm ci --no-audit --no-fund --loglevel=error

# Copy source and build
COPY . .

# Accept build arguments shared by all Masar frontend runtime surfaces.
ARG NEXT_PUBLIC_API_URL
ARG NEXT_PUBLIC_BACKEND_URL
ENV NEXT_PUBLIC_API_URL=$NEXT_PUBLIC_API_URL
ENV NEXT_PUBLIC_BACKEND_URL=$NEXT_PUBLIC_BACKEND_URL

# next.config.ts has output: 'standalone' — this produces .next/standalone/
RUN npm run build

# ─── Stage 2: Runtime shared by landing, student, and admin surfaces ─────────
FROM node:20-alpine AS runner

WORKDIR /app

ENV NODE_ENV=production
ENV PORT=8738
ENV HOSTNAME=0.0.0.0

# Install curl for docker-compose healthchecks
RUN apk add --no-cache curl

# Copy only the standalone server bundle (no full node_modules)
COPY --from=builder /app/.next/standalone ./
# Copy static assets (CSS, JS chunks served by Next.js)
COPY --from=builder /app/.next/static ./.next/static
# Copy public assets (images, fonts, etc.)
COPY --from=builder /app/public ./public

EXPOSE 8738

# next/standalone produces a self-contained server.js.
CMD ["node", "server.js"]
