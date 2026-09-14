# Data Model: Platform Phase 0 Audit

This feature does not add application database entities. The following are documentation models used to structure the Markdown audit report.

## RoadmapAuditReport

Represents the final report under `docs/platform-phase0-audit-2026-06-27.md`.

### Fields

- `title`: Report title.
- `date`: Report date.
- `sourceRoadmap`: `docs/platform-change-roadmap.md`.
- `executiveSummary`: Short current-state summary.
- `auditItems`: Collection of `RoadmapAuditItem`.
- `highRiskItems`: Filtered view of audit items touching finance, access, permissions, parent data, audit logs, or migrations.
- `conflicts`: Collection of `ConflictFinding`.
- `recommendedNextSpecs`: Ordered collection of `RecommendedNextSpec`.
- `manualQaStatus`: Collection of `ManualQaEvidence`.
- `verificationNotes`: Files inspected, commands run, and known uncertainty.

## RoadmapAuditItem

Represents one phase, major subsection, or expanded high-risk child item from the roadmap.

### Fields

- `phase`: Phase label, such as `Phase 1`.
- `item`: Roadmap item name.
- `scopeLevel`: `phase`, `subsection`, or `expanded-child`.
- `status`: One of:
  - `Complete`
  - `Partial`
  - `Missing`
  - `Conflicting`
  - `Spec incomplete`
  - `Spec ready / implementation not verified`
  - `Needs deeper inspection`
- `impactAreas`: One or more of:
  - `Data`
  - `Permissions`
  - `Payment/Finance`
  - `UI`
  - `Worker/Event`
  - `Documentation`
  - `Needs new spec`
- `risk`: `High`, `Medium`, or `Low`.
- `relatedSpecs`: List of spec directory paths or `None found`.
- `implementedEvidence`: File path, route, service, test, or `Not verified`.
- `manualQa`: `passed`, `failed`, `blocked`, or `pending`.
- `notes`: Short evidence-based observation.
- `recommendation`: Extend existing spec, create new spec, defer, or inspect deeper.

### Validation Rules

- `Complete` requires `implementedEvidence` that is not `Not verified`.
- Spec path alone cannot set `status` to `Complete`.
- High-risk impact areas must include a risk note.
- Missing manual QA must be visible as `pending` or `blocked`.

## ConflictFinding

Represents a mismatch or overlap discovered during audit.

### Fields

- `topic`: Roadmap topic.
- `sourceA`: Roadmap, spec, code file, or docs path.
- `sourceB`: Roadmap, spec, code file, or docs path.
- `conflict`: Short description.
- `severity`: `High`, `Medium`, or `Low`.
- `recommendedResolution`: Product decision, spec merge, new spec, or deeper inspection.

## RecommendedNextSpec

Represents an ordered recommendation after Phase 0.

### Fields

- `rank`: Numeric order.
- `name`: Proposed feature/spec name.
- `reason`: Why it should be next.
- `dependsOn`: Required prior evidence or specs.
- `suggestedScope`: Short boundary statement.
- `risk`: `High`, `Medium`, or `Low`.

## ManualQaEvidence

Represents manual evidence status for owner-facing validation.

### Fields

- `flow`: Admin, teacher, student, parent, assistant, purchase/code, permission, or finance flow.
- `status`: `passed`, `failed`, `blocked`, or `pending`.
- `reason`: Why it has that status.
- `evidence`: Screenshot path, note, command, or `Not executed`.
