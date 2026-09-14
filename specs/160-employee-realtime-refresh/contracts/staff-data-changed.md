# StaffDataChanged Contract

The existing SignalR event name and `Role_Staff` target remain. Payloads use JSON:

```json
{
  "schemaVersion": "2",
  "eventId": "uuid",
  "occurredAt": "2026-07-12T00:00:00Z",
  "actorUserId": "uuid",
  "scopes": ["users", "hr"],
  "entityType": "EmployeeProfile",
  "entityIds": ["uuid"],
  "operation": "updated",
  "version": 12
}
```

Clients must accept the legacy `{ "scopes": [...] }` shape during rollout, treating missing metadata as non-deduplicable and reconciling active queries safely. New backend events must include the full envelope.
