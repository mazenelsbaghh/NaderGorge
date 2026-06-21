# Quickstart: Live Support Command Center

## Prerequisites

- Docker and Docker Compose.
- Project environment files/secrets already required by the current stack.
- At least one admin, two employee users with `EmployeeProfile`, one student, and one guest browser profile.
- No WhatsApp/OTP dependency is used.

## Configuration

The feature is disabled by default. Enable it from `/admin/live-support`, then configure support employees, capacities, and Cairo schedule windows.

Current fixed safety limits are:

```text
Guest cookie name: massar_live_support
Guest session lifetime: 30 days
Disconnect grace: 120 seconds
Presence heartbeat: 30 seconds
Maximum text message: 4000 characters
```

Production requires HTTPS for the guest cookie. Do not log cookie values, guest security stamps, passwords, or action payload secrets.

## Build and migration

```bash
docker compose config -q
dotnet build backend/NaderGorge.sln
npm --prefix frontend run lint
npm --prefix frontend exec tsc -- --noEmit
npm --prefix frontend run build
make migrate
make up
make ps
```

Expected:

- Migration `AddLiveSupportCommandCenter` applies once.
- Backend, Redis, PostgreSQL, Nginx, landing, student, admin and assistant services are healthy.
- No new worker/service/container is introduced.

## Automated tests

### Backend application tests

```bash
dotnet test backend/tests/NaderGorge.Application.Tests/NaderGorge.Application.Tests.csproj --filter LiveSupport
```

Must cover:

- student and guest privacy/session boundaries;
- attendance/config/presence eligibility;
- least-load and tie rotation;
- FIFO and capacity changes;
- two-minute disconnect grace;
- manual transfer and close;
- student linking/replacement;
- action ownership, validation, stale confirmation, audit redaction and idempotency;
- closed/read-only and one rating per conversation;
- rating attribution to every assignment owner;
- admin metrics/timeline authorization.

### PostgreSQL concurrency tests

Run against the Docker database, never EF InMemory:

```bash
ConnectionStrings__DefaultConnection="Host=localhost;Port=5435;Database=massar_platform_test;Username=postgres;Password=postgres" dotnet test backend/tests/NaderGorge.Integration.Tests/NaderGorge.Integration.Tests.csproj --filter LiveSupportAssignmentConcurrency
```

Must issue simultaneous create/close/transfer/checkout/capacity requests and prove:

- active assignment count never exceeds configured capacity;
- a conversation never has two active assignments;
- oldest active queue entry wins;
- one idempotency key creates one business effect;
- lock retries do not lose conversations.

### Frontend/E2E

```bash
npm --prefix frontend exec playwright test tests/e2e/live-support.spec.ts --project=chromium
npm --prefix frontend exec playwright test tests/e2e/live-support.spec.ts --project=webkit
```

WebKit coverage must include iPhone-sized participant widget, guest cookie reconnect, unavailable schedule, queue, closed/read-only, new conversation, and rating.

## E2E seed requirements

Extend the current E2E seed to create:

- `Support Staff A`: enabled, capacity 1, checked in, online after browser connects.
- `Support Staff B`: enabled, capacity 2, checked in, online after browser connects.
- `Support Staff C`: enabled but checked out, proving login alone is ineligible.
- Admin with live-support configuration/intervention access.
- Student with packages, balance, device, watch state, note, CRM history and an action-test target.
- Weekly schedule containing a deterministic next availability.

## Manual QA sequence

### 1. Availability and participant privacy

1. Ensure no support employee is checked in.
2. Open landing and student widgets; confirm new conversation is blocked and next schedule is shown.
3. Check in Staff A; confirm conversation start becomes available.
4. As guest, enter another student's exact phone; confirm no account information is exposed or linked.

### 2. Assignment, capacity and FIFO

1. Check in/connect Staff A and B.
2. Start three conversations and verify least-load then fair tie ordering.
3. Fill both capacities; start two more and verify positions 1 and 2.
4. Close one Staff A conversation; verify queue position 1 is assigned automatically.
5. Lower Staff B capacity below current load; verify no eviction and no additional assignment.

### 3. Disconnect and attendance

1. Disconnect Staff A for less than 120 seconds; verify ownership stays.
2. Reconnect and verify no duplicate messages/assignments.
3. Disconnect for more than 120 seconds; verify requeue/reassignment.
4. Clock out Staff B; verify immediate ineligibility and safe redistribution.

### 4. Guest linking and actions

1. Manually search/link the seeded student from a guest conversation.
2. Replace with another student and verify old data disappears while history remains.
3. Execute one representative action from every catalog category.
4. Repeat balance/package action with the same idempotency key; verify one effect.
5. Change relevant state between confirmation and execution; verify stale confirmation rejection.
6. Verify admin timeline and central audit show safe before/after values and conversation correlation.

### 5. Closure and rating

1. Close a conversation handled by two transferred staff members.
2. Confirm sending is disabled and “بدء محادثة جديدة” creates a distinct conversation.
3. Submit 4 stars once; verify a second submission fails.
4. Confirm both owners receive the same rating in performance metrics.

### 6. Accessibility/responsive

1. Complete participant flow at 320px and iPhone WebKit without horizontal overflow.
2. Complete staff select/send/open-context/confirm action using keyboard only.
3. Verify queue, assignment, reconnect and action results are announced without reading the whole transcript.
4. Enable reduced motion and confirm no critical state relies on animation.

## Health and smoke checks

```bash
curl -f http://localhost:5245/api/health
curl -f http://localhost:8738
curl -f http://localhost:8739
curl -f http://localhost:8740
curl -f http://localhost:8742
docker compose logs --tail=200 backend
```

No logs may contain guest cookie values, plaintext passwords, full tokens, or unredacted sensitive action requests.

## Rollout

1. Keep `LiveSupportEnabled=false` after migration.
2. Configure staff capacity and weekly schedules in admin.
3. Verify admin and assistant command centers.
4. Enable for authenticated students.
5. Enable landing guest entry only after privacy and rate-limit checks pass.
6. If operational issues occur, disable the platform setting; do not delete support/audit history or roll back a populated migration.
