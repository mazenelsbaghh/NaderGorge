# Quickstart Developer Guide: Assistant Workspace

## Local Setup

1. Make sure your local hosts file (`/etc/hosts`) maps the assistant surface subdomain:
   ```text
   127.0.0.1 staff.localhost
   ```

2. Run the platform containers:
   ```bash
   make up
   ```

3. Open the Assistant workspace:
   URL: `http://staff.localhost:8742/assistant/dashboard`

## Testing the API

To verify task ownership checks manually, you can use Curl requests against the API endpoints:

1. **Get My Tasks**:
   ```bash
   curl -H "Authorization: Bearer <assistant-token>" http://localhost:5245/api/v1/assistant/tasks/my
   ```

2. **Get Task Details**:
   ```bash
   curl -H "Authorization: Bearer <assistant-token>" http://localhost:5245/api/v1/assistant/tasks/my/{taskId}
   ```

3. **Update Status (As Assignee)**:
   ```bash
   curl -X POST -H "Content-Type: application/json" -H "Authorization: Bearer <assistant-token>" -d '{"status": 1}' http://localhost:5245/api/v1/assistant/tasks/my/{taskId}/status
   ```
