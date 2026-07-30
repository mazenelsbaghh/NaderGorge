# Admin Content Identity API Contract

All responses use the existing `ApiResponse<T>` envelope. Existing JWT authentication, correlation IDs, and exception mapping remain unchanged.

## Video Type DTO

```json
{
  "id": "uuid",
  "name": "شرح",
  "sortOrder": 10,
  "isActive": true,
  "assignedVideoCount": 12,
  "createdAt": "2026-06-29T10:00:00Z",
  "updatedAt": null
}
```

`NormalizedName` is never returned or accepted.

## List Video Types

`GET /api/admin/video-types?includeInactive=false`

- Authorization: authenticated user with `content.manage`.
- Success: `200`, ordered by `sortOrder`, then `name`.
- `includeInactive=false`: active types only.
- `includeInactive=true`: active and inactive types, needed by edit forms.

## Create Video Type

`POST /api/admin/video-types`

Authorization: built-in `Admin` role.

```json
{
  "name": "حل أسئلة",
  "sortOrder": 50,
  "isActive": true
}
```

- Success: `201` with created DTO.
- Validation: `400` for blank/length/order errors or normalized duplicate.
- Forbidden: `403` for authenticated non-admin.

## Update Video Type

`PUT /api/admin/video-types/{id}`

Authorization: built-in `Admin` role.

```json
{
  "name": "حل تدريبات",
  "sortOrder": 60
}
```

- Success: `200` with updated DTO.
- Not found: `404` when `id` does not exist.
- Validation: `400` for invalid values or normalized duplicate.

## Set Video Type Status

`PATCH /api/admin/video-types/{id}/status`

Authorization: built-in `Admin` role.

```json
{ "isActive": false }
```

- Success: `200` with updated DTO.
- Deactivation preserves assignments.
- Not found: `404`.

## Delete Video Type

`DELETE /api/admin/video-types/{id}`

Authorization: built-in `Admin` role.

- Success: `204` for an unused type.
- Conflict: `409` with code `VIDEO_TYPE_IN_USE` and Arabic guidance to deactivate when assignments exist.
- Not found: `404`.
- A blocked assigned deletion writes an audit attempt without changing catalog state.

## Create Video Contract Change

`POST /api/admin/videos`

Add required request field:

```json
{ "videoTypeId": "uuid" }
```

- Unknown, missing, or inactive type: `400` field validation.
- Response remains the created video GUID in the existing envelope.
- Manual provider and Bunny creation paths apply the same requirement.

## Update Video Contract Change

`PUT /api/admin/videos/{id}`

Add required request field:

```json
{ "videoTypeId": "uuid" }
```

- The current inactive type is accepted when unchanged.
- Any replacement must reference an active type.
- `InternalCode` is not accepted.

## Lesson Cockpit Response Change

`GET /api/admin/lessons/{lessonId}/cockpit`

Add:

```json
{
  "lessonId": "uuid",
  "internalCode": "LES-...",
  "videos": [
    {
      "id": "uuid",
      "internalCode": "VID-...",
      "videoType": {
        "id": "uuid",
        "name": "شرح",
        "isActive": true
      }
    }
  ]
}
```

## Exam Dashboard Response Change

`GET /api/admin/exams/{examId}/dashboard`

Add:

```json
{ "internalCode": "EXM-..." }
```

No student API, playback contract, purchase contract, or exam-attempt contract changes.
