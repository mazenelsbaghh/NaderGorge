# Quickstart: Phase 0 — Discovery, Planning, and Product Blueprint

**Branch**: `001-phase0-discovery-blueprint`
**Date**: 2026-03-21

## What is Phase 0?

Phase 0 produces the complete product blueprint for the Nader George Educational Platform. **No code is written.** The output is 10 Markdown documents that lock the product definition, business rules, technical architecture, and UX direction.

## Prerequisites

- Access to the repository on branch `001-phase0-discovery-blueprint`
- Read [plan.md](../../../../plan.md) (root-level) — the primary source of truth for all decisions
- Read [spec.md](./spec.md) — the feature specification with acceptance criteria
- Read [constitution.md](../../.specify/memory/constitution.md) — the project governance principles

## How to Produce Deliverables

### Step 1: Create the deliverables directory

```bash
mkdir -p specs/001-phase0-discovery-blueprint/deliverables
```

### Step 2: Write deliverables in order

Follow the dependency order in [data-model.md](./data-model.md):

1. **01-product-requirements.md** — Start here. Define platform identity, audience, brand.
2. **02-content-blueprint.md** — Content hierarchy (Package → Content Section → Lesson).
3. **03-access-blueprint.md** — Code system rules, types, activation flows.
4. **04-data-blueprint.md** — Student data fields and engagement metrics.
5. **05-user-roles-matrix.md** — Roles, permissions, multi-role model.
6. **06-technical-architecture.md** — Full tech stack, provider abstractions.
7. **07-business-rules.md** — Watch control, exams, homework, gamification, student behavior.
8. **08-ux-direction.md** — UX principles, two-step registration, sitemap.
9. **09-data-model-draft.md** — All entities across 7 domains.
10. **10-system-blueprint.md** — Deployment services, environments, performance targets.

### Step 3: Validate each deliverable

For each deliverable, check:
- [ ] All required sections from [data-model.md](./data-model.md) are present
- [ ] Canonical term "Content Section" is used (not "months")
- [ ] No implementation code or framework-specific details (except in 06 and 10)
- [ ] Cross-references to other deliverables are accurate

### Step 4: Run cross-validation

After all deliverables are written:
- [ ] Every entity in 09 traces to a requirement in another deliverable
- [ ] Every code type in 03 has rules in 07
- [ ] Every role in 05 appears in 08 sitemap
- [ ] Technical Architecture (06) references all constitution-required provider abstractions

### Step 5: Review and approve

Present all deliverables to the project owner (Nader George) for approval against the success criteria in [spec.md](./spec.md).

## How to Validate Phase 0 is Complete

Run through these spec success criteria:

| SC | Check | How to Verify |
|----|-------|---------------|
| SC-001 | All 10 deliverables exist as Markdown files | `ls specs/001-phase0-discovery-blueprint/deliverables/` |
| SC-002 | New team member understands platform in 30 min | Have someone unfamiliar read the deliverables |
| SC-003 | Business rules cover all plan sections | Compare 07 against plan sections 4.1–4.7 |
| SC-004 | Roles matrix has no undefined entries | Review 05 for empty cells |
| SC-005 | Data model covers all 7 domains | Count domains in 09 |
| SC-006 | 5 code scenarios work | Walk through 5 scenarios against 03 |
| SC-007 | Developer can set up repo from 06 | Have a developer try |
| SC-008 | AI scope has in/out lists | Check for explicit lists in 07 or PRD |

## Next Steps

After Phase 0 approval:
1. Run `/speckit-tasks` to generate the task breakdown
2. Begin Phase 1 implementation following the technical architecture from deliverable 06
