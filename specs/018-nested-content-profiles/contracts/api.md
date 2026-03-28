# API Contract: Nested Content Profiles

## Admin API extensions

### 1. `GET /api/admin/terms/{termId}`
Retrieves basic details about a specific Term. Requires Admin role.

**Response (200 OK):**
```json
{
  "success": true,
  "data": {
    "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
    "title": "الفصل الدراسي الأول",
    "order": 1,
    "packageId": "3fa85f64-5717-4562-b3fc-2c963f66afa6"
  },
  "errors": null
}
```

### 2. `GET /api/admin/sections/{sectionId}`
Retrieves basic details about a specific Section. Requires Admin role.

**Response (200 OK):**
```json
{
  "success": true,
  "data": {
    "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
    "title": "الوحدة الأولى: البناء الذري",
    "order": 1,
    "termId": "3fa85f64-5717-4562-b3fc-2c963f66afa6"
  },
  "errors": null
}
```
