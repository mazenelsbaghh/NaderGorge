# Research: Internal Chat, Workrooms, and Real-Time Notifications

## Technical Decisions

### Decision 1: Real-Time Communication Framework
- **Chosen Alternative**: ASP.NET Core SignalR (WebSockets transport, falling back to SSE or Long Polling).
- **Rationale**: SignalR provides out-of-the-box support for groups (chat rooms), connection management, hub-based RPC routing, and JWT authentication. It integrates seamlessly with our .NET 9 backend.
- **Alternatives Considered**: 
  - **Raw WebSockets in C#**: Rejected due to high development overhead for connection pooling and room-based pub/sub.
  - **Socket.io (Node.js)**: Rejected as it would require running a separate stateful node service and complicating session sharing/authorization with the .NET backend.

### Decision 2: Message & Room Storage Strategy
- **Chosen Alternative**: PostgreSQL tables mapped via Entity Framework Core 9.
- **Rationale**: PostgreSQL handles relational entities (rooms, participants, messages, read states) with foreign keys guaranteeing referential integrity (e.g. deleting a task cascades to its workroom, or deleting a participant cleans up read states). PostgreSQL JSONB is used for flexible message metadata (such as file/image attachment dimensions or audio durations).
- **Alternatives Considered**:
  - **Redis for Chat History**: Rejected. Redis is excellent for caching and real-time state, but lacks long-term persistence guarantees and rich querying capabilities needed for auditing and historical search.

### Decision 3: Hub Connection Authentication
- **Chosen Alternative**: JWT authentication passed via Query String.
- **Rationale**: Standard browser WebSocket APIs do not support custom headers. SignalR supports extracting the token from the query string (configured via JWT bearer options in ASP.NET Core).
- **Alternatives Considered**:
  - **Cookie-Based Authentication**: Rejected because the frontend and backend are hosted on separate domains/subdomains, requiring complex cross-site cookie configurations.

### Decision 4: Task-Based Workroom Lifecycle
- **Chosen Alternative**: MediatR Event Handlers.
- **Rationale**: When an operations task is created (from Phase 3) or updated, the system raises domain events. An event handler in `NaderGorge.Application` listens and automatically inserts a corresponding `ChatRoom` of type `Workroom` linked to the task, adding the creator, assignees, and supervisors as participants.
- **Alternatives Considered**:
  - **Direct Controller Insertion**: Rejected as it couples task creation logic directly with chat infrastructure, violating clean architecture.
