# HTTP API Contracts: Phase 2.5: Admin CMS for Homework and Assistants

## 1. Manage User Roles (Admin Endpoint)
Updates a specific user's roles array.

- **Method**: `PUT`
- **Path**: `/api/v1/admin/users/{userId}/roles`
- **Authentication**: Bearer Token (Roles: Admin)

**Request Payload:**
```json
{
  "roles": ["Student", "Assistant"]
}
```

**Responses:**
- `204 No Content`: Successful update.
- `400 Bad Request`: Cannot downgrade final admin, or invalid custom role.
- `401 Unauthorized`/`403 Forbidden`: Admin privileges required.

---

## 2. Attach Homework to Lesson
Creates or updates a Homework container specifically tied to a LessonId.

- **Method**: `POST`
- **Path**: `/api/v1/admin/content/lessons/{lessonId}/homework`
- **Authentication**: Bearer Token (Roles: Admin)

**Request Payload:**
```json
{
  "title": "Module 1 Review Questions",
  "instructions": "Please write thoroughly.",
  "isMandatory": true,
  "requiredPointsToPass": 5,
  "questions": [
    {
      "text": "What are the primary causes of X?",
      "order": 1,
      "maxPoints": 10
    }
  ]
}
```

**Responses:**
- `201 Created`: Homework successfully attached. Returns the Homework Id object.
- `200 OK`: Existing homework successfully overwritten.
- `404 Not Found`: Lesson does not exist.
