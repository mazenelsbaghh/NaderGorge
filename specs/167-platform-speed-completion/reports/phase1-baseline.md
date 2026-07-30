# Phase 1 Baseline Report

**Date**: 2026-07-29  
**Status**: GO for source implementation; NO-GO for candidate sealing or
production until the remote baseline and all release gates pass.

## Completed scope

- Created a deterministic inventory of every actual tracked change and
  untracked file, with per-file hashes, classification, previous hashes for
  tracked changes, and a complete workspace digest.
- Added fail-closed sensitive-path and secret-content scanning without printing
  secret values.
- Added candidate invalidation for any post-seal path/content delta.
- Recorded the attached Cloudflare LCP/INP/CLS values, the secondary-results
  sample bias, and limitations of the existing ingress-only load evidence.
- Added reproducible frontend production-route/browser and backend
  command-count/latency baseline recorders.

## Evidence

- Workspace manifest:
  `artifacts/performance-167/baseline/workspace-manifest.json`
- Workspace digest:
  `b778e39803862fb657779550b6922a7cec2e78ed89a1df856f493f5c4c5b007f`
- Inventory at capture: 19,347 changed/untracked entries (109 modified, 19,238
  untracked). The high untracked count is dominated by existing production
  evidence under `artifacts/`; it remains classified rather than silently
  omitted.
- RUM/load evidence:
  `artifacts/performance-167/baseline/rum-baseline.json`
- Cloudflare source verification: all three attached PDF pages were rendered
  and visually reviewed locally; the recorded LCP/INP/CLS values and debug
  elements match the attachment. No internet result was used.
- Backend recorder:
  `backend/tests/NaderGorge.Integration.Tests/Performance/PlatformPerformanceBaselineTests.cs`
- Frontend recorders:
  `frontend/scripts/record-route-performance-baseline.mjs` and
  `frontend/tests/e2e/platform-performance-baseline.spec.ts`

## Commands and results

- `python3 -m py_compile deploy/production/scripts/source_manifest.py` — PASS.
- Complete manifest creation — PASS; secret audit passed.
- Complete manifest verification — PASS with identical digest.
- Standard-library isolated Git self-test for actual untracked files,
  deterministic digest, late-delta rejection, and secret redaction — PASS.
- `dotnet build backend/tests/NaderGorge.Integration.Tests/NaderGorge.Integration.Tests.csproj --no-restore`
  — PASS, 0 warnings and 0 errors, using already restored local assets.

## Local no-download constraint

The user prohibited downloading or installing anything on the local device.
Therefore:

- Docker image pulling was stopped and Docker was closed before services began.
- No local `npm install`, `npm ci`, further `dotnet restore`, browser install,
  Docker pull, or SDK/tool install is permitted.
- The fresh production frontend resource baseline, browser baseline, and
  PostgreSQL runtime baseline remain mandatory but must run on the reviewed
  remote builder/current-production release using the exact baseline evidence
  contract. Their tasks remain unchecked until numeric evidence exists.

## Manual QA

No product behavior changed in this phase. Manual QA is not applicable beyond
reviewing that the manifest contains all intended classifications and exposes no
secret value.

## Risks and gate

- The baseline source is protected by hashes but numeric current-route/backend
  measurements still require the remote environment.
- A newly appearing workspace file invalidates any candidate, by design.
- Implementation may proceed from the approved audit and protected source
  inventory. Production release remains NO-GO until T003–T005 and every later
  qualification gate pass remotely without modifying the user's device.
