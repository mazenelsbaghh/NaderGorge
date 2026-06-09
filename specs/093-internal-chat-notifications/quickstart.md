# Quickstart: Internal Chat, Workrooms, and Real-Time Notifications

## Setup & Running Locally

### 1. Database Migrations
Run the entity framework migrations on the backend to create the database tables:
```bash
make migrate
# Or run manually in backend folder:
dotnet ef database update --project src/NaderGorge.Infrastructure --startup-project src/NaderGorge.API
```

### 2. Run the Services
Run the backend web API, frontend dashboard, and background worker:
```bash
# Start Docker services
make up

# Backend Hub URL:
# http://localhost:5245/hubs/chat

# Frontend Chat Routing:
# Admin: /admin/chat
# Assistant: /assistant/chat
# Teacher: /teacher/chat
```

## How to Verify Real-Time Communication

1. **Verify WebSocket Handshake**:
   Use a WebSocket test client (e.g. Postman WebSocket request or `wscat`) to connect to:
   `ws://localhost:5245/hubs/chat?access_token=YOUR_JWT_TOKEN`
   Ensure you receive a successful connection and protocol handshake message:
   `{"protocol":"json","version":1}`

2. **Integration / Smoke Test**:
   Execute the automated test suite verifying room access, messaging limits, and notification events:
   ```bash
   dotnet test --filter "FullyQualifiedName~InternalChat"
   ```

3. **Verify Worker Notifications**:
   Run the BullMQ notifications worker to process chat mentions:
   ```bash
   cd worker && npm run dev
   ```
   Ensure mention job messages are printed in the worker log console.
