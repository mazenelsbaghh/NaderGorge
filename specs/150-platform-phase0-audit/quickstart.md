# Quickstart: Platform Phase 0 Audit

## Purpose

Use this guide to verify the Phase 0 audit deliverable before starting any Phase 1 implementation specs.

## Expected Output

```text
docs/platform-phase0-audit-2026-06-27.md
```

## Verification Steps

1. Confirm the active Spec Kit feature is this audit:

   ```bash
   cat .specify/feature.json
   ```

   Expected: `specs/150-platform-phase0-audit`.

2. Confirm the required report sections exist:

   ```bash
   rg -n "^## (Executive Summary|Audit Matrix|High-Risk Items|Conflicts/Overlaps|Recommended Next Specs|Manual QA Status|Verification Notes)" docs/platform-phase0-audit-2026-06-27.md
   ```

3. Confirm all roadmap phases are represented:

   ```bash
   rg -n "Phase 0|Phase 1|Phase 2|Phase 3|Phase 4|Phase 5|Phase 6" docs/platform-phase0-audit-2026-06-27.md
   ```

4. Confirm high-risk areas are visible:

   ```bash
   rg -n "Payment/Finance|Permissions|Data|High" docs/platform-phase0-audit-2026-06-27.md
   ```

5. Confirm no row claims completion without evidence by manually checking `Complete` rows:

   ```bash
   rg -n "Complete" docs/platform-phase0-audit-2026-06-27.md
   ```

6. Confirm Docker configuration was not broken:

   ```bash
   docker compose config -q
   ```

7. Confirm migrations were not required:

   ```bash
   rg -n "make migrate|migration|schema" docs/platform-phase0-audit-2026-06-27.md
   ```

   Expected: report states `make migrate` was skipped because Phase 0 does not change schema.

8. Review changed files:

   ```bash
   git diff --name-only
   ```

   Expected Phase 0 outputs include Spec Kit artifacts and the audit report. Production-code changes are not part of this feature's intended scope.

## Manual Review Checklist

- [ ] The report covers every roadmap phase from Phase 0 through Phase 6.
- [ ] Each major subsection has a status, impact, risk, spec mapping, evidence note, manual QA state, and recommendation.
- [ ] High-risk finance/access/permissions items are not hidden in general notes.
- [ ] `Complete` rows cite implemented evidence beyond a spec path.
- [ ] Manual QA that was not executed is marked `pending` or `blocked`.
- [ ] The first three recommended next specs are easy to identify.
