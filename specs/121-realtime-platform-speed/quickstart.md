# Quickstart: Real-time Platform Speed & Sync

## local Development Setup

1. **Infra-only Setup** (Database & Redis):
   ```bash
   make up
   ```
   *Verify Docker containers are running with `make ps`.*

2. **Apply Database Migrations**:
   ```bash
   make migrate
   ```

3. **Start backend API**:
   ```bash
   make backend
   ```
   *The Swagger UI is available at `http://localhost:5245/swagger/index.html`.*

4. **Start Next.js frontend**:
   ```bash
   make frontend
   ```
   *The application is running at `http://localhost:8738/`.*

## Verification of Real-time Layer

### Testing SignalR Connection:
1. Log in as a student in Chrome, open DevTools console.
2. Observe the console log showing SignalR connection established to `/hubs/platform`.
3. In Safari or an incognito window, log in as Admin.
4. From the admin panel, trigger an action like creating a notification for the student or updating their balance.
5. Verify that the Chrome student view updates instantly (e.g. balance number changes or toast displays) and console logs show the received SignalR packet.

### Testing HTTP Idempotency:
1. Trigger a POST request to `/api/student/balance/purchase` with a header `Idempotency-Key: test-key-123`.
2. Re-trigger the exact same request within 10 minutes.
3. Confirm that the second request returns immediately with the cached response and does not perform another purchase logic execution on the backend.
