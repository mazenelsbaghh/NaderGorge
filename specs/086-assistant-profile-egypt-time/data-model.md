# Data Model & API Contracts: Assistant Profile & Egypt Timezone Localization

This document defines the data models and API endpoints introduced for this feature.

---

## 1. Backend DTOs

### GetUserAuditLogsQuery
- Type: MediatR Query
- Payload: `Guid UserId`

### UserAuditLogDto
- Type: C# record
```csharp
public record UserAuditLogDto(
    Guid Id,
    string Action,
    string EntityType,
    Guid? EntityId,
    string? OldValues,
    string? NewValues,
    string? IpAddress,
    DateTime CreatedAt
);
```

---

## 2. API Contract

### Get User Audit Logs
- **Endpoint**: `GET /api/admin/users/{id}/audit-logs`
- **Authentication**: JWT Required (Admin or Assistant role)
- **Response Format**: `ApiResponse<List<UserAuditLogDto>>`

#### Example Response Body
```json
{
  "success": true,
  "message": "Success",
  "data": [
    {
      "id": "e00b8e6f-cbdf-4a6c-9419-7bfba5bbab9f",
      "action": "AdjustBalance",
      "entityType": "StudentBalance",
      "entityId": "a90df123-bc9a-4c22-92a0-410ba2d00122",
      "oldValues": "{\"Balance\":100}",
      "newValues": "{\"Balance\":150}",
      "ipAddress": "127.0.0.1",
      "createdAt": "2026-06-06T15:30:00Z"
    }
  ]
}
```
