# Operational Data Model

This feature does not add a product-domain table. It defines authoritative
operational records and evidence formats used by deployment and acceptance
automation. Secrets are references only.

## ClusterNode

| Field | Type | Validation |
|---|---|---|
| `id` | enum | `node-1`, `node-2`, `node-3`; immutable |
| `public_address` | IPv4 | one of the three approved server addresses |
| `overlay_address` | IPv4 | unique WireGuard address |
| `ssh_host_key_sha256` | string | pinned, non-empty; change requires explicit re-enrollment |
| `hostname` | string | unique production hostname |
| `maintenance_state` | enum | `serving`, `draining`, `maintenance`, `failed` |
| `release_digest` | string | OCI sha256 digest |
| `readiness` | enum | `ready`, `not_ready`, `unknown` |
| `last_evidence_at` | UTC timestamp | must not be in the future |

Relationships: one node has many role assignments, health observations and
deployment steps.

State transitions:

```text
serving -> draining -> maintenance -> serving
serving|draining|maintenance -> failed -> maintenance
```

A node may return to `serving` only after local and remote readiness checks pass.

## ServiceRoleAssignment

| Field | Type | Validation |
|---|---|---|
| `service` | enum | `postgres`, `redis`, `files`, `ingress`, `app` |
| `node_id` | ClusterNode ID | approved node |
| `role` | enum | service-specific role |
| `term` | integer | monotonically increasing for elected services |
| `healthy` | boolean | derived from authoritative health endpoint |
| `observed_at` | UTC timestamp | evidence timestamp |

Invariants:

- PostgreSQL has at most one `primary` per Patroni term.
- Redis has at most one `master` per Sentinel epoch.
- File data bricks are fixed to node-1/node-2 and arbiter to node-3.
- All three app/ingress roles should be `active` outside maintenance.
- A role observation is evidence, not an alternative source of truth; Patroni,
  Sentinel and Gluster remain authoritative.

## DeploymentRelease

| Field | Type | Validation |
|---|---|---|
| `release_id` | string | Git commit plus UTC build identifier |
| `backend_digest` | sha256 | required |
| `frontend_digest` | sha256 | required |
| `worker_digest` | sha256 | required |
| `migrator_digest` | sha256 | required |
| `migration_target` | string | latest expected EF migration |
| `previous_release_id` | string? | required for rollback after first release |
| `state` | enum | `built`, `audited`, `migrated`, `rolling`, `accepted`, `failed`, `rolled_back` |
| `created_at` | UTC timestamp | immutable |

Release transition:

```text
built -> audited -> migrated -> rolling -> accepted
   \        \          \          \-> failed -> rolled_back
    \--------\----------\------------> failed
```

The same digests must exist on all three nodes before `rolling`.

## DeploymentNodeStep

| Field | Type | Validation |
|---|---|---|
| `release_id` | DeploymentRelease ID | required |
| `node_id` | ClusterNode ID | required |
| `sequence` | integer | node-3, node-2, node-1 unless override documented |
| `drained_at` | UTC timestamp? | required before replacement |
| `ready_at` | UTC timestamp? | required before undrain |
| `smoke_result` | enum | `pending`, `pass`, `fail` |
| `failure_reason` | string? | required on fail |

Unique key: (`release_id`, `node_id`).

## DatabaseSchemaAudit

| Field | Type | Validation |
|---|---|---|
| `audit_id` | UUID | required |
| `release_id` | DeploymentRelease ID | required |
| `migration_history_hash` | sha256 | ordered migration IDs |
| `model_snapshot_hash` | sha256 | required |
| `clean_database` | boolean | must be true for acceptance |
| `pending_migrations` | integer | must be 0 |
| `pending_model_changes` | boolean | must be false |
| `critical_findings` | integer | must be 0 |
| `findings_path` | relative artifact path | no secrets |
| `completed_at` | UTC timestamp | required |

Findings classify table/column type, nullability, default, foreign key,
check/unique constraint, index, extension, ownership, and unapproved seed data.

## BackupSet

| Field | Type | Validation |
|---|---|---|
| `backup_id` | string | repository identifier |
| `kind` | enum | `db-full`, `db-wal`, `files-hourly` |
| `repository` | secret reference | internal three-node bucket identifier, no credentials |
| `started_at` / `completed_at` | UTC timestamp | completed >= started |
| `checksum_manifest` | sha256/path | required |
| `encrypted` | boolean | must be true |
| `expires_at` | UTC timestamp | rolling 30-day policy |
| `status` | enum | `running`, `complete`, `failed`, `expired` |

Database acceptance requires effective WAL archive age <= 5 minutes. File
acceptance requires effective latest successful snapshot age <= 60 minutes;
both ages include time elapsed since the evidence was captured.

## RestoreEvidence

| Field | Type | Validation |
|---|---|---|
| `restore_id` | UUID | required |
| `backup_id` | BackupSet ID | required |
| `target_time` | UTC timestamp? | required for PITR test |
| `isolated_namespace` | string | must not be production |
| `started_at` / `completed_at` | UTC timestamp | duration <= 60 minutes target |
| `migration_state_ok` | boolean | must be true |
| `integrity_ok` | boolean | must be true |
| `login_smoke_ok` | boolean | must be true |
| `file_sample_ok` | boolean | required for file restore |
| `destroyed_at` | UTC timestamp? | required after evidence capture |

## FailoverEvent

| Field | Type | Validation |
|---|---|---|
| `event_id` | UUID | required |
| `service` | enum | `ingress`, `app`, `postgres`, `redis`, `files` |
| `former_node` | ClusterNode ID | required |
| `new_node` | ClusterNode ID? | null only when quorum correctly refuses service |
| `started_at` / `recovered_at` | UTC timestamp | RTO derived |
| `quorum_evidence` | object/path | required for elected data services |
| `acknowledged_loss_count` | integer | must be 0 for one-node failure |
| `split_brain_detected` | boolean | must be false |
| `result` | enum | `pass`, `fail`, `safe_refusal` |

## StoredFileEvidence

| Field | Type | Validation |
|---|---|---|
| `logical_path` | normalized relative path | no traversal |
| `classification` | enum | `public`, `protected`, `private` |
| `size_bytes` | integer | >= 0 |
| `sha256` | string | 64 lowercase hex |
| `primary_copy` | enum | `healthy`, `missing`, `stale` |
| `standby_copy` | enum | `healthy`, `missing`, `stale` |
| `arbiter_metadata` | enum | `healthy`, `missing`, `conflict` |
| `visible` | boolean | true only after atomic commit |

Invariant: an API upload response may be successful only when the Gluster write
has completed with quorum. Temporary names are never routable by `assets`.

## OperationalSecretReference

| Field | Type | Validation |
|---|---|---|
| `name` | string | purpose, never value |
| `provider` | enum | `root-file`, `systemd-credential`, `cloudflare`, `s3` |
| `reference` | string | path or external identifier; no secret material |
| `version` | string | rotatable identifier |
| `rotated_at` | UTC timestamp | required before production acceptance |

Secret values are prohibited from inventory, evidence, command arguments,
tracked `.env` files, logs and admin bootstrap artifacts.

## NodeHealthEvidence

| Field | Type | Validation |
|---|---|---|
| `node_id` | ClusterNode ID | required |
| `release_id` | DeploymentRelease ID | required |
| `live` | boolean | process responds |
| `ready` | boolean | safe to receive traffic |
| `dependencies` | map | DB writer, Redis master, Gluster quorum |
| `traffic_count` | integer | >= 0 |
| `observed_at` | UTC timestamp | required |
| `reason` | string? | required when not ready |

Readiness must become false before drain/maintenance and whenever a mandatory
write dependency cannot meet its safety contract.
