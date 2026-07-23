# Contract: Phase 0 Audit Report

The Phase 0 implementation must create one Markdown report:

```text
docs/platform-phase0-audit-2026-06-27.md
```

## Required Sections

The report must contain these top-level sections in this order:

1. `## Executive Summary`
2. `## Audit Matrix`
3. `## High-Risk Items`
4. `## Conflicts/Overlaps`
5. `## Recommended Next Specs`
6. `## Manual QA Status`
7. `## Verification Notes`

## Audit Matrix Columns

The `Audit Matrix` section must include a Markdown table with these columns:

| Column | Required Content |
|---|---|
| Phase | Roadmap phase label |
| Item | Major subsection or high-risk child item |
| Status | Completion classification |
| Impact | Data, Permissions, Payment/Finance, UI, Worker/Event, Documentation, Needs new spec |
| Risk | High, Medium, Low |
| Related specs | Existing spec paths or `None found` |
| Evidence | File/spec/UI/test evidence or `Not verified` |
| Manual QA | passed, failed, blocked, or pending |
| Recommendation | Extend existing spec, create new spec, defer, or inspect deeper |

## Status Rules

- `Complete`: requires implemented evidence such as code, UI, tests, or another concrete artifact.
- `Partial`: some implemented evidence exists but expected scope is incomplete.
- `Missing`: no spec and no implementation evidence was found.
- `Conflicting`: roadmap, spec, or code evidence points to incompatible behavior or ownership.
- `Spec incomplete`: spec exists but is a placeholder, missing major sections, or does not cover the roadmap item.
- `Spec ready / implementation not verified`: spec appears meaningful but implementation evidence was not found in the audit pass.
- `Needs deeper inspection`: evidence is inconclusive.

## Evidence Rules

- Every row must cite at least one spec path, source path, route path, service file, test path, or explicit `Not found`/`Not verified` observation.
- A spec path alone is not enough for `Complete`.
- Missing manual QA must be shown as `pending` or `blocked`.
- High-risk rows must explain the risk in the recommendation or notes.

## Verification Notes Requirements

The report must list:

- Roadmap source used.
- Specs inspected.
- Representative backend/frontend/mobile/worker source areas inspected.
- Commands run.
- Commands skipped and why.
- Statement confirming no product-code changes were intentionally made for Phase 0.
