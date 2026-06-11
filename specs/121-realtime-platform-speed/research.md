# Research and Decisions: Real-time Platform Speed & Sync

## SignalR Platform Hub Architecture

### Decision: Dedicated PlatformHub
We will implement a separate hub `/hubs/platform` (`PlatformHub.cs`) instead of reusing `ChatHub`. 

**Rationale:**
- ChatHub has distinct domain rules, security checks, and message routing. A platform-wide event routing hub must remain simple, lightweight, and performant.
- Separating them adheres to the Single Responsibility Principle and prevents chat connection drops from disrupting platform sync notifications.

### Group Management Design
- On connection, connections are automatically joined to:
  - `User_{UserId}` group.
  - `Role_{RoleName}` group.
- Dynamic subscription methods inside `PlatformHub`:
  - `JoinPackage(string packageId)`: Backend queries DB or caches to confirm the connection's authenticated user has active access to the package before calling `Groups.AddToGroupAsync`.
  - `LeavePackage(string packageId)`: Client calls this when navigating away from the package details.
  - `JoinLesson(string lessonId)`: Confirm student has access before joining `Lesson_{lessonId}` group.
  - `LeaveLesson(string lessonId)`: Client calls this when leaving focus mode/lesson view.

---

## Outbox Pattern for Real-time Events

### Decision: Relational Outbox Processing
We will use a table `OutboxEvents` in PostgreSQL to capture all events during DbContext save transactions, processed asynchronously by an ASP.NET Core HostedService.

**Rationale:**
- Ensures transactional consistency: an event is written if and only if the underlying database updates succeed (e.g., lesson is published, code is activated).
- Decouples API endpoints from immediate SignalR network issues; if a client or Hub is transiently offline or overloaded, outbox messages will be queued and retried.

**Alternatives Considered:**
- **In-Memory MediatR Notifications:** Rejected because events are lost if the web process crashes during execution, leading to UI/Backend state desynchronization.
- **Direct SignalR publish in Handlers:** Rejected because SignalR network failures or delays would slow down or fail database transactions.

---

## Idempotency and Deduplication

### Decision: Redis-backed Idempotency Filter
We will implement an ASP.NET Core `ActionFilter` checking the `Idempotency-Key` header, caching responses in Redis for 10 minutes.

**Rationale:**
- Redis provides sub-millisecond lookups and atomic checks via `SET NX` or Lua scripting, making it ideal for concurrency control.
- Caching the response status code and body ensures that if a user retries due to a timeout, they get the exact same response instantly.

---

## Secure Media Delivery (X-Accel-Redirect)

### Decision: Nginx X-Accel-Redirect Headers
For file downloads from local Docker volumes, the .NET backend will authenticate the request and return an `X-Accel-Redirect` header specifying the internal URI, which Nginx will serve directly.

**Rationale:**
- Eliminates memory copying and buffer overhead in .NET, letting Nginx stream files efficiently via kernel-level `sendfile`.
- Keeps the C# thread pool clean and available for API requests under high load.
