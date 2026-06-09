# Contract: Frontend-Driven Endpoint Inventory

## Command

```bash
node scripts/generate-endpoint-inventory.mjs
```

## Generated JSON Shape

```json
{
  "generatedBy": "scripts/generate-endpoint-inventory.mjs",
  "controllerDirectory": "backend/src/NaderGorge.API/Controllers",
  "frontendDirectories": [
    "frontend/src/services",
    "frontend/src/app/api",
    "frontend/src/components",
    "frontend/src/packages"
  ],
  "endpointCount": 0,
  "frontendCallCount": 0,
  "missingFrontendRouteCount": 0,
  "digest": "sha256",
  "endpoints": [],
  "frontendCalls": [],
  "routeFindings": []
}
```

## Required Behavior

- `endpointCount` must equal `endpoints.length`.
- `frontendCallCount` must equal `frontendCalls.length`.
- `missingFrontendRouteCount` must equal the number of `routeFindings` where `kind` is `missing-backend-route`.
- `routeFindings` must be empty of missing backend routes before the feature is complete.
- `--check` must fail if `tests/endpoint_inventory.json` or `tests/endpoint_inventory.md` is stale.

## Markdown Report

The Markdown report must include:

- Backend endpoint inventory grouped by controller.
- Frontend backend-call inventory grouped by source file.
- Route findings section that prints "No missing frontend-called backend routes." when clean.
