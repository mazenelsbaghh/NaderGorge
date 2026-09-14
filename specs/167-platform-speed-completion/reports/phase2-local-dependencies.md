# Phase 2 Local Dependency Boundary

The user forbids downloads or installations on the local device.

## Present and usable locally

- Node/Next executable already exists under `frontend/node_modules`.
- Worker TypeScript executable already exists under `worker/node_modules`.
- .NET 9 SDK and the currently restored project assets exist.
- Python 3 standard library and Git exist.

## Intentionally not added

- `@tanstack/react-query` is not installed locally and was not downloaded.
- The platform query layer is implemented from the repository's existing
  `query-keys`, `query-contracts`, cache invalidation, Axios, and React
  dependencies.

## Prohibited local commands

- `npm install`, `npm ci`, package-manager updates.
- `dotnet restore`.
- Playwright browser installers.
- Docker pulls or builds requiring missing base images.
- SDK/tool installers.

Missing-material gates must execute on the reviewed remote builder against the
sealed source and remain required before production.
