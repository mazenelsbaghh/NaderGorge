# Production Environment Contract

## Canonical public origins

```text
LANDING_PUBLIC_ORIGIN=https://massar-academy.net
STUDENT_PUBLIC_ORIGIN=https://app.massar-academy.net
ADMIN_PUBLIC_ORIGIN=https://admin.massar-academy.net
TEACHER_PUBLIC_ORIGIN=https://teacher.massar-academy.net
ASSISTANT_PUBLIC_ORIGIN=https://staff.massar-academy.net
NEXT_PUBLIC_API_URL=https://api.massar-academy.net/api
NEXT_PUBLIC_BACKEND_URL=https://api.massar-academy.net
NEXT_PUBLIC_WS_URL=https://ws.massar-academy.net
NEXT_PUBLIC_APP_DOMAIN=massar-academy.net
COOKIE_DOMAIN=.massar-academy.net
CORS_ALLOWED_ORIGINS=https://massar-academy.net,https://app.massar-academy.net,https://admin.massar-academy.net,https://teacher.massar-academy.net,https://staff.massar-academy.net
```

Public image builds use these canonical values. Runtime internal calls use
WireGuard/service names and never public Cloudflare hostnames.

## Stable internal endpoints

```text
POSTGRES_HOST=host-gateway
POSTGRES_PORT=6432          # local HAProxy -> current Patroni primary
REDIS_SENTINEL_MASTER=massar-redis
REDIS_SENTINELS=node-1.cluster.internal:26379,node-2.cluster.internal:26379,node-3.cluster.internal:26379
MASSAR_STORAGE_ROOT=/srv/massar-shared
MASSAR_PUBLIC_ASSETS_ROOT=/srv/massar-shared/public
MASSAR_PROTECTED_ROOT=/srv/massar-shared/protected
MASSAR_PRIVATE_ROOT=/srv/massar-shared/private
```

Applications are forbidden from using a fixed PostgreSQL/Redis leader node.

## Required secret references

- PostgreSQL application, replication and Patroni administration credentials
- Redis data and Sentinel credentials
- JWT, callback, AI/worker and parent-report signing secrets
- WireGuard private keys
- Cloudflare tunnel token/credential
- internal Garage bucket key, RPC/admin tokens, TLS key, Restic password, and pgBackRest encryption passphrase
- external provider credentials already required by the application

Values live in root-only files/systemd credentials outside the repository.
Templates contain names and references only. The preflight reports
present/missing/version and never value or hash-derived fingerprints that aid
guessing.

## Node-specific non-secret fields

```text
MASSAR_NODE_ID=node-1|node-2|node-3
MASSAR_NODE_OVERLAY_IP=<inventory value>
MASSAR_RELEASE_ID=<immutable release>
MASSAR_BUILD_DIGEST=<sha256>
```

## Port exposure

Public interface after cutover:

- SSH only from approved operator source or Cloudflare Access path.
- No public application port is required when Tunnel is active.
- PostgreSQL, Patroni, etcd, Redis, Sentinel, GlusterFS, Docker API, worker
  admin and HAProxy stats are always denied publicly.

WireGuard interface:

- explicit per-service ports only between the three cluster nodes;
- management endpoints require authentication where supported;
- application origins accept only cluster peers.

## Secret rotation gate

Production acceptance fails if:

- any known exposed root/test password still authenticates;
- root/password routine SSH remains enabled;
- tracked files contain production secret values;
- default/demo credentials can authenticate;
- secret file permissions are broader than the owning service/operator.
