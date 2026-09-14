# Production Cluster Architecture

## Traffic and application topology

```mermaid
flowchart TD
    CF["Cloudflare proxy and one Tunnel"] --> C1["Connector replica: node-1"]
    CF --> C2["Connector replica: node-2"]
    CF --> C3["Connector replica: node-3"]

    C1 --> H1["Local HAProxy"]
    C2 --> H2["Local HAProxy"]
    C3 --> H3["Local HAProxy"]

    H1 --> A1["Full app stack: node-1"]
    H1 --> A2["Full app stack: node-2"]
    H1 --> A3["Full app stack: node-3"]
    H2 --> A1
    H2 --> A2
    H2 --> A3
    H3 --> A1
    H3 --> A2
    H3 --> A3
```

Each node runs the backend, worker, landing, student, Admin, teacher, staff and
gateway services. A connector always enters through its node-local HAProxy, and
that HAProxy distributes ready requests across all three gateways. The approved
hosts are:

| Host | Surface |
|---|---|
| `massar-academy.net` | Public landing |
| `app.massar-academy.net` | Student application |
| `admin.massar-academy.net` | Admin application |
| `teacher.massar-academy.net` | Teacher application |
| `staff.massar-academy.net` | Staff application |
| `api.massar-academy.net` | HTTP API |
| `ws.massar-academy.net` | WebSocket/SignalR |
| `assets.massar-academy.net` | Public and authorized assets |

Unknown hosts are rejected. Database, Redis, etcd, Patroni, Gluster and local
HAProxy ports are not public.

## Stateful topology

```mermaid
flowchart LR
    Apps["Apps on all three nodes"] --> DBEP["Local PostgreSQL writer endpoint"]
    DBEP --> PG["Patroni: one writer and two replicas"]
    PG --> ETCD["Three-member etcd quorum"]

    Apps --> SENT["Redis Sentinel discovery"]
    SENT --> REDIS["One Redis master and two replicas"]

    Apps --> FS["One Gluster mount"]
    FS --> D1["Full data brick: node-1"]
    FS --> D2["Full data brick: node-2"]
    FS --> ARB["Arbiter: node-3"]
```

PostgreSQL is the only authoritative relational store. Patroni may move the
writer role, but applications keep using the stable local writer endpoint.
Redis coordinates cache, SignalR and BullMQ; it is not the source of truth.
Gluster exposes one logical filesystem with two full byte copies and a third
arbiter vote.

## Role and failure matrix

| Failure | Expected behavior | Safety boundary |
|---|---|---|
| One app/gateway | Removed from readiness pool; two nodes continue | No session state may live only in a process |
| One tunnel connector | Cloudflare uses the other connectors | All replicas use the same tunnel |
| PostgreSQL writer | Patroni elects one safe replacement | Quorum loss refuses unsafe writes |
| One PostgreSQL replica | Writer plus one replica continue | Repair before another maintenance event |
| Redis master | Sentinel promotes one replica | Durable truth remains in PostgreSQL |
| One Gluster data brick | Other full copy serves acknowledged files | No-quorum writes must fail |
| One whole server | Remaining two keep quorum and service | Never take a second member down |
| Two servers | Availability is not promised | Prefer explicit refusal over split brain |
| Internal three-node backup repository | New backups fail and alert | Production cutover remains blocked if stale or fewer than three Garage members are healthy |

## Release invariant

All nodes must run the same immutable image digests. Schema migration runs once
under an advisory lock before the rolling application update. One node is
drained at a time, and a failed readiness gate stops the rollout.
