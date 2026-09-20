# Build from repository root. Supply reviewed immutable image references and an explicit CLI version.
ARG NODE_IMAGE
ARG DOTNET_IMAGE
FROM ${NODE_IMAGE} AS node
FROM ${DOTNET_IMAGE}
ARG CODEX_VERSION
COPY --from=node /usr/local/bin/node /usr/local/bin/node
COPY --from=node /usr/local/lib/node_modules /usr/local/lib/node_modules
RUN ln -s /usr/local/lib/node_modules/npm/bin/npm-cli.js /usr/local/bin/npm \
    && ln -s /usr/local/lib/node_modules/npm/bin/npx-cli.js /usr/local/bin/npx \
    && test -n "$CODEX_VERSION" \
    && npm install --global "@openai/codex@$CODEX_VERSION" \
    && command -v git
COPY deploy/auto-repair/review-schema.json /opt/repair/review-schema.json
COPY frontend/package.json frontend/package-lock.json /opt/bootstrap/frontend/
COPY worker/package.json worker/package-lock.json /opt/bootstrap/worker/
RUN npm --prefix /opt/bootstrap/frontend ci --ignore-scripts --cache /opt/cache/npm \
    && npm --prefix /opt/bootstrap/worker ci --ignore-scripts --cache /opt/cache/npm
ENV PLAYWRIGHT_BROWSERS_PATH=/opt/playwright
RUN cd /opt/bootstrap/frontend && ./node_modules/.bin/playwright install --with-deps chromium \
    && apt-get update && apt-get install -y --no-install-recommends ffmpeg \
    && rm -rf /var/lib/apt/lists/* && chmod -R a+rX /opt/playwright
COPY deploy/auto-repair/fonts.mjs /opt/repair/fonts.mjs
RUN node /opt/repair/fonts.mjs --prepare
# Use a filtered source context: never include appsettings secrets, environment files, or local credentials.
COPY backend /opt/bootstrap/backend
RUN dotnet restore /opt/bootstrap/backend/NaderGorge.sln --packages /opt/cache/nuget \
    && chmod -R a+rX /opt/cache \
    && rm -rf /opt/bootstrap/backend
ENV PATH="/usr/local/bin:/usr/share/dotnet:$PATH" NUGET_PACKAGES=/opt/cache/nuget
