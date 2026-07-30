# Performance and HA validation

Production acceptance uses the release-bound k6 script and the matrix in
`deploy/production/config/performance-matrix.json`. Never point either at a
Production origin implicitly. Set `MASSAR_LOAD_AUTHORIZED=1`, explicit origins,
the immutable release ID, run ID, evidence path, measured single-node baseline,
and the expected serving nodes.

## Reviewed internal runner

Before Cloudflare publication, run k6 on exactly one inventory-selected control
node. The runner fixes all HTTP origins to `http://127.0.0.1:8088`, uses only
the eight inventory-approved Host headers, and pins the Grafana k6 1.8.0
linux/amd64 image by its platform manifest digest. It does not drain a node,
inject a failure, or change cluster routing.

The plan is an exact JSON object containing `schemaVersion`, `seriesId`,
`releaseId`, `baselineRps`, `profile`, `excludedNode`, `expectedNodes`, and
`stages`. Every stage contains exactly `sequence`, `requestedRps`, `duration`,
and `runId`. Preview locally first; dry-run does not construct an SSH
transport:

```bash
python3 deploy/production/scripts/run_live_load.py \
  --inventory deploy/production/inventory/production.yml \
  --control-node node-2 \
  --plan /protected/operator/capacity-node-1.plan.json \
  --evidence-dir artifacts/production/capacity/node-1 \
  --series-output artifacts/production/capacity/node-1.series.json \
  --dry-run
```

The reviewed live command additionally requires strict known-host and identity
files plus `--yes`. Auth tokens are accepted only as local regular `0600`
files. Token values are copied into the control node's private temporary
directory and supplied through Docker `--env-file`; they are never command
arguments, stdout, or evidence:

Create those two token files from a dedicated disposable account stored in a
separate `0600` JSON file.  The helper prints neither the credentials nor the
token values:

```bash
python3 deploy/production/scripts/prepare_load_test_tokens.py \
  --credentials-file /protected/operator/load-test-account.json \
  --api-origin https://api.massar-academy.net \
  --surface student \
  --websocket-output /protected/operator/ws-token \
  --workflow-output /protected/operator/workflow-token \
  --yes
```

```bash
python3 deploy/production/scripts/run_live_load.py \
  --inventory deploy/production/inventory/production.yml \
  --control-node node-2 \
  --plan /protected/operator/capacity-node-1.plan.json \
  --evidence-dir artifacts/production/capacity/node-1 \
  --series-output artifacts/production/capacity/node-1.series.json \
  --known-hosts /protected/operator/massar_known_hosts \
  --identity /protected/operator/id_ed25519 \
  --websocket-vus 10 \
  --websocket-token-file /protected/operator/ws-token \
  --workflow-rps 1 \
  --workflow-token-file /protected/operator/workflow-token \
  --public-asset-path /known/public/probe.png \
  --protected-asset-path /api/assets/known/probe \
  --upload-probe-path /api/admin/content-images \
  --yes
```

Before running, the runner verifies the immutable release directly through
each of the three overlay ingress endpoints and checks the local HAProxy
landing/API/WebSocket routes. The script is copied into a mode-0700 remote
temporary directory. Docker uses host networking with a read-only root,
non-root user, all capabilities dropped, `no-new-privileges`, a bounded tmpfs,
and read-only script mount. Evidence fetch is atomic and capped at 1 MiB.
The named container, token files, environment file, and remote directory are
removed in `finally` paths. Failure cleanup does not conceal load output.

Run the three-node 30-minute test first. Capacity discovery is a sequence of
separate constant-rate runs, not one ramping k6 run. Every RPS stage gets a
unique run ID and immutable `load.schema.json` evidence file, so its
p95/p99/error/drop/distribution result cannot be hidden inside an aggregate.
Stop the series immediately after its first failed stage. Capture
`collect_capacity.py` evidence before, during each separate stage, and after
the series.

The bounded matrix tests 0.5× through 4× baseline in 0.5× increments. If 4×
still passes, capacity remains a lower bound: review and explicitly extend the
matrix rather than inventing a ceiling. `MASSAR_LOAD_STAGES_JSON` is rejected
to prevent accidental aggregate evidence.

Each stage also collects all three nodes immediately before, every 30 seconds
during, and immediately after load. `capacity-thresholds.json` is the reviewed
default contract and may be replaced only by an exact explicit thresholds
file. A stage fails if any sample violates CPU busy/iowait/steal, memory or
disk headroom, PostgreSQL connection utilization/locks/replication lag, Redis
memory/blocked clients/AOF delay, Patroni state/single-primary, or BullMQ
waiting/failed/stalled backlog. Missing nodes, missing queues, insufficient
during samples, and stale before/during/after samples also fail closed.

The runner writes both `<runId>.load.json` and
`<runId>.capacity.json`. Its series output contains both `evidencePath` and
`capacityEvidencePath` for every completed stage. Combine the three N-1 series
objects with the common `schemaVersion`, `releaseId`, and `baselineRps` to
produce the assembler manifest.

Run N-1 three times, draining one approved application node at a time. Set
`MASSAR_LOAD_PROFILE=n-minus-one`, `MASSAR_EXCLUDED_NODE`, and exactly the
other two `MASSAR_EXPECTED_NODES`. An excluded-node hit, imbalance beyond the
configured tolerance, second failed node, or unhealthy post-state fails the
run. Restore and verify the cluster fully before the next node.

Capacity acceptance requires a separate N-1 series for each excluded node.
Create a manifest containing exactly `schemaVersion`, `releaseId`,
`baselineRps`, and the three series. Each stage entry contains only
`sequence`, `requestedRps`, and `evidencePath`. Then assemble the result:

```bash
python3 deploy/production/scripts/assemble_capacity_ceiling.py \
  --manifest artifacts/production/capacity/series-manifest.json \
  --output artifacts/production/capacity/capacity-ceiling.json
```

The assembler validates each source against `load.schema.json`, binds it to
the same immutable release, baseline, excluded node, expected node pair, and
requested RPS, and validates the paired resource evidence against
`capacity-stage.schema.json`. It recomputes resource violations and rejects
missing, stale, unbound, or forged capacity results, duplicate run IDs, gaps,
stages after failure, missing passing stages, or missing first failures. A
stage passes only when both load and capacity pass. For each N-1 series it
records the highest contiguous passing RPS and first failing RPS. The cluster
bottleneck is the minimum of the three highest passing values, and the safe
operating ceiling is exactly `0.60 × bottleneck`. The result conforms to
`capacity-ceiling.schema.json` and includes SHA-256 bindings to both evidence
files for every stage.

Redis `blocked_clients` is evaluated as a cluster total, not per node. BullMQ
workers legitimately keep blocking queue consumers open, and those connections
move with the Redis master during failover. Three repeated idle captures for
the v8 release measured a stable cluster total of 18. The reviewed gate
therefore allows at most 24 blocked clients in total and at most 5 more than
the stage's exact `before` sample. This preserves failover transparency while
still failing on an absolute over-budget state or load-correlated growth.
Missing or non-numeric values fail closed.

For rolling-deploy and failover continuity, keep k6 running while the reviewed
one-node operation executes. Enable authenticated SignalR plus the low-rate
workflow probes. These probes read one known public asset, read one known
protected asset, and submit an intentionally invalid multipart image that must
be rejected without publishing a file. The WebSocket gate detects early
disconnects as well as failed upgrades/handshakes.

The probes do not replace manual QA. Credentials and known asset URLs are
required for authenticated probes. Valid uploads with cleanup, role-specific
navigation, cross-node application events, acknowledged writes/jobs, and file
quorum refusal remain controlled manual or integration checks. Chaos remains
one scenario at a time and is never authorized by the load script.
