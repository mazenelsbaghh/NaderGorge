# ─── Stage 1: Build ──────────────────────────────────────────────────────────
FROM node:22.13.0-alpine AS builder

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

# Accept build arguments shared by all Massar frontend runtime surfaces.
ARG NEXT_PUBLIC_API_URL
ARG NEXT_PUBLIC_BACKEND_URL
ARG NEXT_PUBLIC_WS_URL
ARG NEXT_PUBLIC_APP_DOMAIN
ARG NEXT_PUBLIC_APP_URL
ARG NEXT_PUBLIC_RELEASE_ID
ENV NEXT_PUBLIC_API_URL=$NEXT_PUBLIC_API_URL
ENV NEXT_PUBLIC_BACKEND_URL=$NEXT_PUBLIC_BACKEND_URL
ENV NEXT_PUBLIC_WS_URL=$NEXT_PUBLIC_WS_URL
ENV NEXT_PUBLIC_APP_DOMAIN=$NEXT_PUBLIC_APP_DOMAIN
ENV NEXT_PUBLIC_APP_URL=$NEXT_PUBLIC_APP_URL
ENV NEXT_PUBLIC_RELEASE_ID=$NEXT_PUBLIC_RELEASE_ID

# next.config.ts has output: 'standalone' — this produces .next/standalone/
RUN npm run build

# ─── Stage 2: Runtime shared by landing, student, and admin surfaces ─────────
FROM node:22.13.0-alpine AS runner

WORKDIR /app

ENV NODE_ENV=production
ENV PORT=8738
ENV HOSTNAME=0.0.0.0

# Install timezone data as well as curl for docker-compose healthchecks. TZ alone is ignored by Alpine
# when the IANA zoneinfo database is absent.
RUN apk add --no-cache curl tzdata

# Copy only the standalone server bundle (no full node_modules)
COPY --from=builder /app/.next/standalone ./
# Copy static assets (CSS, JS chunks served by Next.js)
COPY --from=builder /app/.next/static ./.next/static
# Copy public assets (images, fonts, etc.)
COPY --from=builder /app/public ./public

# Startup validation script
COPY --from=builder /app/docker-entrypoint.sh ./
RUN chmod +x docker-entrypoint.sh

EXPOSE 8738

# Validate APP_SURFACE before starting Next.js
ENTRYPOINT ["./docker-entrypoint.sh"]
CMD ["node", "server.js"]
