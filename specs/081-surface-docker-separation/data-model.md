# Data Model: Surface Docker Separation

## RuntimeSurface

Represents one externally addressable application boundary.

**Fields**:
- `name`: one of `landing`, `student`, `admin`, `backend`
- `containerName`: Massar-prefixed Docker container name
- `hostPort`: unique host port
- `containerPort`: internal container port
- `publicOrigin`: browser-accessible origin
- `healthPath`: path used by health checks
- `defaultEntryPath`: path served or rewritten from `/`

**Validation Rules**:
- `hostPort` must be unique among application surfaces.
- `containerName` must start with `massar_`.
- frontend surfaces must include `APP_SURFACE`.
- backend surface must expose `/api/health`.

## PortMap

Documents host port ownership.

**Fields**:
- `landingPort`
- `studentPort`
- `adminPort`
- `backendPort`
- `workerPort`
- `postgresPort`
- `redisPort`

**Validation Rules**:
- application ports cannot duplicate each other.
- default values must be overridable from environment variables.

## SurfaceRouteBoundary

Defines what happens when a request reaches a surface.

**Fields**:
- `surface`: `landing`, `student`, `admin`, or `all`
- `requestPath`
- `action`: `next`, `rewrite`, or `redirect`
- `targetPathOrOrigin`

**State Rules**:
- landing `/` -> next
- student `/` -> rewrite `/student`
- admin `/` -> rewrite `/admin`
- landing `/student*` -> redirect to student origin
- landing `/admin*` -> redirect to admin origin
- student `/admin*` -> redirect to admin origin
- admin `/student*` -> redirect to student origin

## PlatformIdentity

Defines visible and operational naming.

**Fields**:
- `arabicName`: `منصة مسار`
- `englishName`: `Massar Platform`
- `dockerPrefix`: `massar`
- `metadataTitle`: Arabic-first title with English fallback

**Validation Rules**:
- changed visible frontend copy must not use `مسار أكاديمي`, `Massar Academy`, or `Nader George`.
- Docker service/container names for application services must use the `massar` prefix.

## VerificationSuite

Defines checks used before delivery.

**Fields**:
- `composeConfigCheck`
- `uniquePortsCheck`
- `serviceHealthcheckCheck`
- `massarNamingCheck`
- `runtimeHttpCheck`
- `brandStringCheck`

**Validation Rules**:
- static verification must run without the stack being up.
- runtime verification should skip unavailable endpoints only when explicitly requested in static-only mode.
