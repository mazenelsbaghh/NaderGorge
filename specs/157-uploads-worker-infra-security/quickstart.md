# Quickstart: Uploads, Assets, Worker, and Infrastructure Security

## Focused Verification Commands

```bash
dotnet build backend/src/NaderGorge.API/NaderGorge.API.csproj
```

```bash
dotnet test backend/tests/NaderGorge.Application.Tests/NaderGorge.Application.Tests.csproj --filter "FullyQualifiedName~UploadsAndAssetsSecurityTests|FullyQualifiedName~ContentImageStorageTests|FullyQualifiedName~UploadContentImageCommandTests"
```

```bash
cd worker && npm run build
```

```bash
cd worker && npm test
```

```bash
docker compose config -q
```

If frontend files are changed:

```bash
cd frontend && npm run lint && npm run build
```

## Manual QA

1. Start production-like stack with required secrets.
2. As admin, upload a valid PDF/image resource and confirm it is accepted.
3. As admin, upload an HTML/script payload renamed to `.pdf` or `.png`; confirm it is rejected.
4. Try to open the protected resource path directly from a browser without a valid signed token; confirm no private bytes are returned.
5. Download the same resource through the signed resource route; confirm it downloads as attachment.
6. Open worker `/ui` in production-like configuration without a token; confirm it is disabled or denied.
7. Simulate or create a stale pending Redis stream message, restart worker, and confirm it is claimed once without duplicate BullMQ jobs.

## Docker/Infra Checks

1. Confirm `docker compose config -q` passes.
2. Confirm worker healthcheck targets `/ready`.
3. Confirm worker image runs as non-root.
4. Confirm Redis auth/persistence/maxmemory settings are present.
5. Confirm Nginx `/secured-assets/` is `internal` and does not use wildcard CORS.
6. Confirm TLS termination responsibility is documented or configured.
